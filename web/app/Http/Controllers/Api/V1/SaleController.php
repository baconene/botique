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
        $v = $request->validate(SaleService::rules());

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
