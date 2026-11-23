<?php

namespace App\Http\Controllers\Api\V1;

use App\Http\Controllers\Controller;
use App\Models\Pos\Product;
use App\Models\Pos\Sale;
use App\Services\Pos\Inventory;
use App\Services\Pos\Money;
use App\Services\Pos\PosSettings;
use Illuminate\Http\JsonResponse;
use Illuminate\Http\Request;
use Illuminate\Support\Carbon;
use Illuminate\Support\Facades\DB;

/** Small endpoints: health, register identity, shared settings, barcode numbering, reports. */
class StoreController extends Controller
{
    public function __construct(private PosSettings $settings, private Inventory $inventory) {}

    public function ping(): JsonResponse
    {
        return response()->json([
            'status' => 'ok',
            'api' => 'v1',
            'server_time' => now()->format('Y-m-d\TH:i:sP'),
        ]);
    }

    public function me(Request $request): JsonResponse
    {
        $r = $request->user();

        return response()->json(['data' => ['id' => $r->id, 'name' => $r->name]]);
    }

    /** GET /stats - catalogue size and the date range of recorded sales (used for "All time" views). */
    public function stats(): JsonResponse
    {
        $first = Sale::min('sale_date');
        $last = Sale::max('sale_date');
        $fmt = fn ($d) => $d === null ? null : Carbon::parse($d)->format('Y-m-d\TH:i:sP');

        return response()->json(['data' => [
            'product_count' => Product::count(),
            'sale_count' => Sale::count(),
            'first_sale_date' => $fmt($first),
            'last_sale_date' => $fmt($last),
        ]]);
    }

    public function settings(): JsonResponse
    {
        return response()->json(['data' => $this->settings->all()]);
    }

    public function updateSettings(Request $request): JsonResponse
    {
        $v = $request->validate([
            'StoreName' => ['sometimes', 'string', 'min:1', 'max:100'],
            'Address' => ['sometimes', 'nullable', 'string', 'max:200'],
            'Phone' => ['sometimes', 'nullable', 'string', 'max:50'],
            'ReceiptFooter' => ['sometimes', 'nullable', 'string', 'max:1000'],
            'TaxRatePercent' => ['sometimes', 'numeric', 'decimal:0,3', 'min:0', 'max:30'],
            'BarcodePrefix' => ['sometimes', 'nullable', 'string', 'max:6', 'regex:/^[\x20-\x7E]*$/'],
        ]);

        return response()->json(['data' => $this->settings->update(array_map(fn ($x) => $x ?? '', $v))]);
    }

    public function nextBarcode(): JsonResponse
    {
        return response()->json(['data' => ['barcode' => $this->inventory->nextBarcode()]]);
    }

    /** GET /reports/summary?from=&to=  (inclusive dates, default today) */
    public function summary(Request $request): JsonResponse
    {
        $v = $request->validate([
            'from' => ['nullable', 'date'],
            'to' => ['nullable', 'date', 'after_or_equal:from'],
        ]);
        $from = Carbon::parse($v['from'] ?? 'today')->startOfDay();
        $to = Carbon::parse($v['to'] ?? ($v['from'] ?? 'today'))->startOfDay()->addDay();
        $range = fn () => Sale::where('sale_date', '>=', $from)->where('sale_date', '<', $to);

        $done = $range()->where('refunded', false)->get(['number', 'total', 'tax', 'discount_amount', 'payment_method']);
        $cents = fn ($rows, $field) => $rows->sum(fn ($s) => Money::toCents($s->{$field}));
        $byMethod = [];
        foreach (['Cash', 'Card', 'Other'] as $m) {
            $byMethod[$m] = Money::format($cents($done->where('payment_method', $m), 'total'));
        }

        $top = DB::table('sale_lines')->join('sales', 'sales.number', '=', 'sale_lines.sale_number')
            ->where('sales.sale_date', '>=', $from)->where('sales.sale_date', '<', $to)->where('sales.refunded', false)
            ->groupBy('sale_lines.barcode', 'sale_lines.name', 'sale_lines.size', 'sale_lines.color')
            ->orderByDesc('qty')->limit(10)
            ->get(['sale_lines.barcode', 'sale_lines.name', 'sale_lines.size', 'sale_lines.color', DB::raw('SUM(sale_lines.quantity) as qty')])
            ->map(fn ($r) => ['barcode' => $r->barcode, 'name' => $r->name, 'size' => $r->size, 'color' => $r->color, 'quantity' => (int) $r->qty]);

        return response()->json(['data' => [
            'from' => $from->toDateString(),
            'to' => $to->copy()->subDay()->toDateString(),
            'sales_count' => $done->count(),
            'items_sold' => (int) DB::table('sale_lines')->whereIn('sale_number', $done->pluck('number'))->sum('quantity'),
            'net_sales' => Money::format($cents($done, 'total')),
            'discounts' => Money::format($cents($done, 'discount_amount')),
            'tax' => Money::format($cents($done, 'tax')),
            'by_payment_method' => $byMethod,
            'refunded_total' => Money::format($cents($range()->where('refunded', true)->get(['total']), 'total')),
            'top_items' => $top,
            'low_stock_items' => Product::whereColumn('quantity', '<=', 'reorder_level')->count(),
        ]]);
    }
}
