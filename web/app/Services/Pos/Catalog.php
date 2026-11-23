<?php

namespace App\Services\Pos;

use App\Models\Pos\Product;
use Illuminate\Support\Collection;

/**
 * Turns POS inventory rows (one row per size/color) into storefront "styles":
 * one card per name + brand, with the colors, sizes and stock across its variants.
 */
class Catalog
{
    private const SIZE_ORDER = ['XXS', 'XS', 'S', 'M', 'L', 'XL', 'XXL', '1X', '2X', '3X'];

    public function styles(?string $category = null, ?string $search = null, string $sort = 'featured'): Collection
    {
        $query = Product::query();
        if ($category) {
            $query->where('category', $category);
        }
        foreach (preg_split('/\s+/', trim((string) $search), -1, PREG_SPLIT_NO_EMPTY) as $token) {
            $like = '%'.str_replace(['%', '_'], ['\%', '\_'], $token).'%';
            $query->where(fn ($w) => $w->where('name', 'like', $like)->orWhere('brand', 'like', $like)
                ->orWhere('category', 'like', $like)->orWhere('color', 'like', $like));
        }

        $styles = $query->get()
            ->groupBy(fn (Product $p) => mb_strtolower(trim($p->name)).'|'.mb_strtolower(trim($p->brand)))
            ->map(fn (Collection $variants) => $this->style($variants))
            ->values();

        return match ($sort) {
            'price_asc' => $styles->sortBy('price_min')->values(),
            'price_desc' => $styles->sortByDesc('price_max')->values(),
            'newest' => $styles->sortByDesc('newest')->values(),
            default => $styles->sortBy([['in_stock', 'desc'], ['newest', 'desc']])->values(),
        };
    }

    /** All variants of the style a product belongs to. */
    public function styleFor(Product $product): array
    {
        $variants = Product::where('name', $product->name)->where('brand', $product->brand)->get();

        return $this->style($variants);
    }

    /** @return Collection<int, array{name:string, count:int}> categories with at least one item in stock */
    public function categories(): Collection
    {
        return Product::query()->where('quantity', '>', 0)->where('category', '!=', '')
            ->selectRaw('category, COUNT(DISTINCT name) as styles')->groupBy('category')->orderBy('category')->get()
            ->map(fn ($r) => ['name' => $r->category, 'count' => (int) $r->styles]);
    }

    private function style(Collection $variants): array
    {
        $first = $variants->sortByDesc('quantity')->first();
        $prices = $variants->map(fn (Product $p) => (float) $p->price);
        $inStock = $variants->filter(fn (Product $p) => $p->quantity > 0);

        return [
            'id' => $first->id,
            'name' => $first->name,
            'brand' => $first->brand,
            'category' => $first->category,
            'color' => $first->color,
            'price_min' => $prices->min(),
            'price_max' => $prices->max(),
            'colors' => $variants->pluck('color')->filter()->unique(fn ($c) => mb_strtolower($c))->values()->all(),
            'sizes' => $this->sortSizes($variants->pluck('size')->filter()->unique()->values()->all()),
            'sizes_in_stock' => $inStock->pluck('size')->filter()->unique()->values()->all(),
            'in_stock' => $inStock->sum('quantity'),
            'low' => $inStock->sum('quantity') > 0 && $inStock->sum('quantity') <= 3,
            'newest' => $variants->max('created'),
            'variants' => $variants->sortBy(fn (Product $p) => $this->sizeRank($p->size).'|'.$p->color)->values(),
        ];
    }

    public function sortSizes(array $sizes): array
    {
        usort($sizes, fn ($a, $b) => strcmp($this->sizeRank($a), $this->sizeRank($b)));

        return $sizes;
    }

    private function sizeRank(?string $size): string
    {
        $s = strtoupper(trim((string) $size));
        $i = array_search($s, self::SIZE_ORDER, true);
        if ($i !== false) {
            return '1'.str_pad((string) $i, 3, '0', STR_PAD_LEFT);
        }
        if (is_numeric($s)) {
            return '2'.str_pad((string) (int) ($s * 10), 6, '0', STR_PAD_LEFT);
        }

        return '3'.$s;   // "One Size" and anything else last
    }
}
