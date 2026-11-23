<?php

namespace App\Services\Pos;

use App\Models\Pos\Product;
use App\Models\Pos\Staff;
use Illuminate\Support\Facades\DB;
use Illuminate\Validation\Rule;

/**
 * Create / edit / adjust / delete items. Shared by the POS API (registers) and the web back office,
 * so both apply exactly the same rules as the Windows app.
 */
class ProductService
{
    private const BARCODE = ['string', 'max:64', 'regex:/^[\x20-\x7E]+$/'];   // Code 128B: printable ASCII

    public function __construct(private Inventory $inventory) {}

    /** Validation rules. $product null = create. */
    public static function rules(?Product $product): array
    {
        $creating = $product === null;
        $req = $creating ? 'required' : 'sometimes';
        $rules = [
            'name' => [$req, 'string', 'max:200'],
            'category' => ['sometimes', 'nullable', 'string', 'max:100'],
            'brand' => ['sometimes', 'nullable', 'string', 'max:100'],
            'size' => ['sometimes', 'nullable', 'string', 'max:40'],
            'color' => ['sometimes', 'nullable', 'string', 'max:60'],
            'price' => [$req, 'numeric', 'decimal:0,2', 'min:0', 'max:9999999999'],
            'cost' => ['sometimes', 'nullable', 'numeric', 'decimal:0,2', 'min:0', 'max:9999999999'],
            'reorder_level' => ['sometimes', 'integer', 'min:0', 'max:9999'],
            'notes' => ['sometimes', 'nullable', 'string', 'max:2000'],
        ];
        if ($creating) {
            return $rules + [
                'id' => ['nullable', 'regex:/^[0-9a-f]{32}$/', 'unique:products,id'],
                'barcode' => array_merge(['nullable'], self::BARCODE, ['unique:products,barcode']),
                'quantity' => ['nullable', 'integer', 'min:-99999', 'max:999999'],
            ];
        }

        return $rules + [
            'barcode' => array_merge(['sometimes'], self::BARCODE, [Rule::unique('products', 'barcode')->ignore($product->id)]),
            'quantity_delta' => ['nullable', 'integer', 'min:-99999', 'max:99999'],
        ];
    }

    public static function adjustRules(): array
    {
        return [
            'delta' => ['required', 'integer', 'min:-99999', 'max:99999', 'not_in:0'],
            'reason' => ['nullable', 'string', 'max:40'],
        ];
    }

    /** POS columns are NOT NULL with '' / 0 defaults, so blank optional fields become '' / 0. */
    public static function clean(array $v): array
    {
        foreach (['category', 'brand', 'size', 'color', 'notes'] as $k) {
            if (array_key_exists($k, $v)) {
                $v[$k] = trim((string) ($v[$k] ?? ''));
            }
        }
        if (array_key_exists('cost', $v) && $v['cost'] === null) {
            $v['cost'] = 0;
        }

        return $v;
    }

    public function create(array $v, Staff $staff): Product
    {
        $v = self::clean($v);

        return DB::transaction(function () use ($v, $staff) {
            $p = new Product($v);
            $p->id = $v['id'] ?? Product::newId();
            $p->barcode = $v['barcode'] ?? $this->inventory->nextBarcode();
            $p->quantity = $v['quantity'] ?? 0;
            $p->save();
            $this->inventory->log($p->id, $p->quantity, 'new item', '', $staff->terminalLabel());

            return $p->fresh();
        });
    }

    /** Stock changes only through quantity_delta (relative), so other registers' sales are never overwritten. */
    public function update(Product $product, array $v, Staff $staff): Product
    {
        $v = self::clean($v);
        DB::transaction(function () use ($v, $product, $staff) {
            $product->fill(collect($v)->except(['quantity_delta', 'id', 'quantity'])->all())->save();
            if (! empty($v['quantity_delta'])) {
                $this->inventory->change($product->id, (int) $v['quantity_delta'], 'edit', '', $staff->terminalLabel());
            }
        });

        return $product->fresh();
    }

    public function adjust(Product $product, int $delta, ?string $reason, Staff $staff): Product
    {
        DB::transaction(fn () => $this->inventory->change($product->id, $delta, $reason ?: 'adjustment', '', $staff->terminalLabel()));

        return $product->fresh();
    }

    public function delete(Product $product): void
    {
        DB::transaction(function () use ($product) {
            DB::table('pos_deleted_products')->insert([
                'product_id' => $product->id, 'barcode' => $product->barcode, 'deleted_at' => now(),
            ]);
            $product->delete();   // past sale lines keep their own copy of name/barcode/price
        });
    }
}
