<?php

namespace App\Services\Pos;

use App\Models\Pos\Product;
use App\Models\Pos\Staff;
use App\Models\Pos\Sale;
use Illuminate\Database\QueryException;
use Illuminate\Support\Carbon;
use Illuminate\Support\Facades\DB;
use Symfony\Component\HttpKernel\Exception\AccessDeniedHttpException;
use Symfony\Component\HttpKernel\Exception\ConflictHttpException;
use Symfony\Component\HttpKernel\Exception\UnprocessableEntityHttpException;

class SaleService
{
    public function __construct(private Inventory $inventory, private PosSettings $settings) {}

    /**
     * Records a sale: totals are recomputed on the server, stock is decremented, and the
     * client_ref makes retries safe (same ref => same sale, no double charge or double stock change).
     *
     * @return array{sale: Sale, quantities: array<string,int>, replayed: bool}
     */
    public function create(Staff $staff, array $data): array
    {
        if ($existing = $this->replay($data['client_ref'])) {
            return ['sale' => $existing, 'quantities' => [], 'replayed' => true];
        }

        // Fill line details from the catalogue where the register only sent an id.
        $products = Product::whereIn('id', collect($data['lines'])->pluck('product_id')->filter()->all())->get()->keyBy('id');
        $lines = [];
        foreach ($data['lines'] as $i => $l) {
            $p = isset($l['product_id']) ? $products->get($l['product_id']) : null;
            if (! empty($l['product_id']) && ! $p) {
                throw new UnprocessableEntityHttpException("Line ".($i + 1).": product {$l['product_id']} does not exist.");
            }
            $unit = $l['unit_price'] ?? ($p ? $p->price : null);
            if ($unit === null) {
                throw new UnprocessableEntityHttpException("Line ".($i + 1).": unit_price is required for items not in inventory.");
            }
            $lines[] = [
                'product_id' => $p ? $p->id : '',
                'barcode' => $l['barcode'] ?? ($p->barcode ?? ''),
                'name' => $l['name'] ?? ($p->name ?? ''),
                'size' => $l['size'] ?? ($p->size ?? ''),
                'color' => $l['color'] ?? ($p->color ?? ''),
                'unit_cents' => Money::toCents($unit),
                'quantity' => (int) $l['quantity'],
            ];
        }
        // Discounts and price changes on inventory items need the "discount" permission.
        $priceChanged = false;
        foreach ($data['lines'] as $i => $l) {
            $p = isset($l['product_id']) ? $products->get($l['product_id']) : null;
            if ($p && isset($l['unit_price']) && Money::toCents($l['unit_price']) !== Money::toCents($p->price)) {
                $priceChanged = true;
            }
        }
        if (($priceChanged || Money::scaled((string) ($data['discount_percent'] ?? '0'), 2) > 0) && ! $staff->hasPermission('discount')) {
            throw new AccessDeniedHttpException('Your role does not allow discounts or price changes.');
        }
        if (! array_filter($lines, fn ($l) => $l['name'] !== '')) {
            throw new UnprocessableEntityHttpException('Each line needs a product_id or a name.');
        }

        $discountPct = (string) ($data['discount_percent'] ?? '0');
        $taxRate = $this->settings->get('TaxRatePercent');
        $t = Money::saleTotals($lines, $discountPct, $taxRate);

        $method = $data['payment_method'];
        $tendered = $t['total'];
        if ($method === 'Cash' && isset($data['amount_tendered'])) {
            $tendered = Money::toCents($data['amount_tendered']);
            if ($tendered < $t['total']) {
                throw new UnprocessableEntityHttpException('Cash received ('.Money::format($tendered).') is less than the total ('.Money::format($t['total']).').');
            }
        }
        $terminal = $staff->terminalLabel();
        $cashier = $staff->cashierName();
        $date = isset($data['sale_date']) ? Carbon::parse($data['sale_date'])->setTimezone(config('app.timezone')) : now();

        try {
            return DB::transaction(function () use ($data, $lines, $t, $method, $tendered, $terminal, $cashier, $date, $staff, $discountPct, $taxRate) {
                $number = $this->inventory->nextNumber('sale');
                $sale = Sale::create([
                    'number' => $number,
                    'sale_date' => $date,
                    'subtotal' => Money::format($t['subtotal']),
                    'discount_percent' => $discountPct,
                    'discount_amount' => Money::format($t['discount']),
                    'tax_rate' => $taxRate,
                    'tax' => Money::format($t['tax']),
                    'total' => Money::format($t['total']),
                    'payment_method' => $method,
                    'amount_tendered' => Money::format($tendered),
                    'change_due' => Money::format($method === 'Cash' ? $tendered - $t['total'] : 0),
                    'refunded' => false,
                    'refund_date' => null,
                    'note' => substr($data['note'] ?? '', 0, 500),
                    'terminal' => $terminal,
                    'cashier' => $cashier,
                ]);
                foreach ($lines as $i => $l) {
                    $sale->lines()->create([
                        'line_no' => $i + 1,
                        'product_id' => $l['product_id'],
                        'barcode' => $l['barcode'],
                        'name' => $l['name'],
                        'size' => $l['size'],
                        'color' => $l['color'],
                        'unit_price' => Money::format($l['unit_cents']),
                        'quantity' => $l['quantity'],
                    ]);
                }
                $quantities = $this->applyStock($lines, -1, 'sale', $sale->code, $terminal);
                DB::table('pos_sale_requests')->insert([
                    'client_ref' => $data['client_ref'],
                    'sale_number' => $number,
                    'register_id' => $staff->id,   // column predates staff logins; holds the staff id
                    'created_at' => now(),
                ]);

                return ['sale' => $sale->load('lines'), 'quantities' => $quantities, 'replayed' => false];
            });
        } catch (QueryException $e) {
            // Two retries raced: the other one won, so return its sale.
            if ($existing = $this->replay($data['client_ref'])) {
                return ['sale' => $existing, 'quantities' => [], 'replayed' => true];
            }
            throw $e;
        }
    }

    /** @return array{sale: Sale, quantities: array<string,int>} */
    public function refund(Staff $staff, Sale $sale): array
    {
        $terminal = $staff->terminalLabel();

        return DB::transaction(function () use ($sale, $terminal) {
            $updated = Sale::whereKey($sale->number)->where('refunded', false)
                ->update(['refunded' => true, 'refund_date' => now()]);
            if ($updated === 0) {
                throw new ConflictHttpException("Sale {$sale->code} was already refunded.");
            }
            $lines = $sale->lines->map(fn ($l) => ['product_id' => $l->product_id, 'quantity' => $l->quantity])->all();
            $quantities = $this->applyStock($lines, +1, 'refund', $sale->code, $terminal);

            return ['sale' => $sale->fresh('lines'), 'quantities' => $quantities];
        });
    }

    private function replay(string $clientRef): ?Sale
    {
        $number = DB::table('pos_sale_requests')->where('client_ref', $clientRef)->value('sale_number');

        return $number === null ? null : Sale::with('lines')->find($number);
    }

    /** @return array<string,int> product_id => new quantity */
    private function applyStock(array $lines, int $sign, string $reason, string $reference, string $terminal): array
    {
        $byProduct = [];
        foreach ($lines as $l) {
            if ($l['product_id'] !== '') {
                $byProduct[$l['product_id']] = ($byProduct[$l['product_id']] ?? 0) + $l['quantity'];
            }
        }
        $result = [];
        foreach ($byProduct as $id => $qty) {
            $q = $this->inventory->change($id, $sign * $qty, $reason, $reference, $terminal);
            if ($q !== null) {
                $result[$id] = $q;
            }
        }

        return $result;
    }
}
