<?php

namespace App\Http\Controllers\Api\V1;

use App\Http\Controllers\Controller;
use App\Models\Pos\Sale;
use App\Services\Pos\SaleService;
use Illuminate\Http\JsonResponse;
use Illuminate\Http\Request;
use Illuminate\Support\Carbon;

class SaleController extends Controller
{
    public function __construct(private SaleService $sales) {}

    /** GET /sales?from=YYYY-MM-DD&to=YYYY-MM-DD (inclusive, default today) */
    public function index(Request $request): JsonResponse
    {
        $v = $request->validate([
            'from' => ['nullable', 'date'],
            'to' => ['nullable', 'date', 'after_or_equal:from'],
            'per_page' => ['nullable', 'integer', 'min:1', 'max:500'],
        ]);
        $from = Carbon::parse($v['from'] ?? 'today')->startOfDay();
        $to = Carbon::parse($v['to'] ?? ($v['from'] ?? 'today'))->startOfDay()->addDay();

        $page = Sale::with('lines')->where('sale_date', '>=', $from)->where('sale_date', '<', $to)
            ->orderByDesc('sale_date')->paginate($v['per_page'] ?? 100);

        return response()->json([
            'data' => $page->items(),
            'meta' => ['current_page' => $page->currentPage(), 'last_page' => $page->lastPage(), 'total' => $page->total()],
        ]);
    }

    /** GET /sales/{number or receipt code, e.g. 1001 or R001001} */
    public function show(string $code): JsonResponse
    {
        return response()->json(['data' => $this->find($code)]);
    }

    /**
     * POST /sales
     * Totals and tax are calculated by the server (tax rate from shared settings). Send a unique
     * client_ref per sale (e.g. a GUID); retrying with the same client_ref returns the original sale.
     */
    public function store(Request $request): JsonResponse
    {
        $v = $request->validate([
            'client_ref' => ['required', 'string', 'max:64'],
            'lines' => ['required', 'array', 'min:1', 'max:500'],
            'lines.*.product_id' => ['nullable', 'regex:/^[0-9a-f]{32}$/'],
            'lines.*.barcode' => ['nullable', 'string', 'max:64'],
            'lines.*.name' => ['nullable', 'string', 'max:200'],
            'lines.*.size' => ['nullable', 'string', 'max:40'],
            'lines.*.color' => ['nullable', 'string', 'max:60'],
            'lines.*.unit_price' => ['nullable', 'numeric', 'decimal:0,2', 'min:0', 'max:9999999999'],
            'lines.*.quantity' => ['required', 'integer', 'min:1', 'max:999'],
            'discount_percent' => ['nullable', 'numeric', 'decimal:0,2', 'min:0', 'max:100'],
            'payment_method' => ['required', 'in:Cash,Card,Other'],
            'amount_tendered' => ['nullable', 'numeric', 'decimal:0,2', 'min:0', 'max:9999999999'],
            'note' => ['nullable', 'string', 'max:500'],
            'sale_date' => ['nullable', 'date', 'before_or_equal:now'],
        ]);

        $r = $this->sales->create($request->user(), $v);

        return response()->json(['data' => $r['sale'], 'quantities' => (object) $r['quantities']], $r['replayed'] ? 200 : 201)
            ->header('Idempotent-Replay', $r['replayed'] ? 'true' : 'false');
    }

    /** POST /sales/{code}/refund  - full refund, items go back into stock. 409 if already refunded. */
    public function refund(Request $request, string $code): JsonResponse
    {
        $r = $this->sales->refund($request->user(), $this->find($code));

        return response()->json(['data' => $r['sale'], 'quantities' => (object) $r['quantities']]);
    }

    private function find(string $code): Sale
    {
        $number = Sale::numberFromCode($code);
        abort_if($number === null, 404, 'Sale not found.');

        return Sale::with('lines')->findOrFail($number);
    }
}
