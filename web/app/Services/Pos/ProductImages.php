<?php

namespace App\Services\Pos;

use App\Models\Pos\Product;
use App\Models\Pos\ProductImage;
use Illuminate\Http\UploadedFile;
use Illuminate\Support\Collection;
use Illuminate\Support\Facades\DB;
use Illuminate\Support\Facades\Storage;

/**
 * Product photos. A photo can belong to one item, to every size of a colour, or to the whole style
 * (same name + brand), because shoppers see one picture per colour/style, not per size.
 * Uploads are stored on the public disk under products/ and served by the media.product route.
 */
class ProductImages
{
    public const DISK = 'public';

    /** Form fields handled here (strip them before passing data to ProductService). */
    public const FIELDS = ['photo', 'photo_url', 'remove_photo', 'photo_scope'];

    public static function rules(): array
    {
        return [
            'photo' => ['nullable', 'image', 'mimes:jpg,jpeg,png,webp', 'max:5120'],
            'photo_url' => ['nullable', 'url:https', 'max:500'],
            'remove_photo' => ['nullable', 'boolean'],
            'photo_scope' => ['nullable', 'in:item,color,style'],
        ];
    }

    /**
     * Applies the photo part of the item form. Returns a short description of what changed, or null.
     *
     * @param  array{photo_url?:?string, remove_photo?:mixed, photo_scope?:?string}  $v
     */
    public function applyForm(Product $product, array $v, ?UploadedFile $file): ?string
    {
        $scope = $v['photo_scope'] ?? 'color';
        if ($file) {
            $path = $file->store('products', self::DISK);
            $n = $this->assign($product, $path, $scope);

            return 'photo uploaded'.$this->scopeNote($n);
        }
        if (! empty($v['photo_url']) && $v['photo_url'] !== $product->image?->path) {
            $n = $this->assign($product, $v['photo_url'], $scope);

            return 'photo link saved'.$this->scopeNote($n);
        }
        if (! empty($v['remove_photo'])) {
            $n = $this->remove($product, $scope);

            return 'photo removed'.$this->scopeNote($n);
        }

        return null;
    }

    /** Sets the photo on the item(s) in scope and returns how many items now use it. */
    public function assign(Product $product, string $path, string $scope = 'item'): int
    {
        $targets = $this->targets($product, $scope);
        $old = ProductImage::whereIn('product_id', $targets)->pluck('path');
        DB::transaction(function () use ($targets, $path) {
            foreach ($targets as $id) {
                ProductImage::updateOrCreate(['product_id' => $id], ['path' => $path]);
            }
        });
        $this->deleteUnusedFiles($old);

        return count($targets);
    }

    public function remove(Product $product, string $scope = 'item'): int
    {
        $targets = $this->targets($product, $scope);
        $old = ProductImage::whereIn('product_id', $targets)->pluck('path');
        ProductImage::whereIn('product_id', $targets)->delete();
        $this->deleteUnusedFiles($old);

        return count($targets);
    }

    /** Called when an item is deleted. */
    public function forget(string $productId): void
    {
        $old = ProductImage::where('product_id', $productId)->pluck('path');
        ProductImage::where('product_id', $productId)->delete();
        $this->deleteUnusedFiles($old);
    }

    /** @return string[] product ids */
    public function targets(Product $product, string $scope): array
    {
        if ($scope === 'item') {
            return [$product->id];
        }
        $q = Product::where('name', $product->name)->where('brand', $product->brand);
        if ($scope === 'color') {
            $q->where('color', $product->color);
        }

        return $q->pluck('id')->push($product->id)->unique()->values()->all();
    }

    /** Uploaded files that no item points to any more are deleted from the disk. */
    private function deleteUnusedFiles(Collection $paths): void
    {
        foreach ($paths->unique() as $path) {
            if (preg_match('#^https?://#i', $path) || ProductImage::where('path', $path)->exists()) {
                continue;
            }
            Storage::disk(self::DISK)->delete($path);
        }
    }

    private function scopeNote(int $n): string
    {
        return $n > 1 ? " for {$n} items" : '';
    }
}
