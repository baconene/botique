<?php

namespace App\Services\Pos;

use App\Models\Pos\Product;
use Illuminate\Support\Facades\DB;
use RuntimeException;

/**
 * Stock and numbering rules shared with the Windows app:
 *  - quantities change relatively (quantity = quantity + delta) so registers never overwrite each other;
 *  - every change is logged in stock_movements;
 *  - item and sale numbers come from the `counters` table under a row lock.
 */
class Inventory
{
    public function __construct(private PosSettings $settings) {}

    public function nextNumber(string $counter): int
    {
        return DB::transaction(function () use ($counter) {
            $row = DB::table('counters')->where('name', $counter)->lockForUpdate()->first();
            if (! $row) {
                throw new RuntimeException("Counter '{$counter}' is missing from the database.");
            }
            $next = (int) $row->value + 1;
            DB::table('counters')->where('name', $counter)->update(['value' => $next]);

            return $next;
        });
    }

    /** Next free barcode like BTQ000123 (prefix from shared settings). */
    public function nextBarcode(): string
    {
        $prefix = strtoupper(trim($this->settings->get('BarcodePrefix')));
        do {
            $code = $prefix.str_pad((string) $this->nextNumber('item'), 6, '0', STR_PAD_LEFT);
        } while (Product::where('barcode', $code)->exists());

        return $code;
    }

    /** Applies a relative change and returns the new quantity on hand. Call inside a transaction for atomicity with other writes. */
    public function change(string $productId, int $delta, string $reason, string $reference, string $terminal): ?int
    {
        if ($delta !== 0) {
            Product::whereKey($productId)->increment('quantity', $delta, ['updated' => now()]);
            $this->log($productId, $delta, $reason, $reference, $terminal);
        }
        $q = Product::whereKey($productId)->value('quantity');

        return $q === null ? null : (int) $q;
    }

    public function log(string $productId, int $delta, string $reason, string $reference, string $terminal): void
    {
        if ($delta === 0) {
            return;
        }
        DB::table('stock_movements')->insert([
            'product_id' => $productId,
            'change_qty' => $delta,
            'reason' => substr($reason, 0, 40),
            'reference' => substr($reference, 0, 40),
            'created' => now(),
            'terminal' => substr($terminal, 0, 64),
        ]);
    }
}
