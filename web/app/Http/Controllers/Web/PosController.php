<?php

namespace App\Http\Controllers\Web;

use App\Http\Controllers\Controller;
use App\Models\Pos\Product;
use App\Models\Pos\Sale;
use App\Services\Pos\PosSettings;
use App\Services\Pos\SaleService;
use Illuminate\Http\JsonResponse;
use Illuminate\Http\Request;
use Illuminate\View\View;

/**
 * Browser POS. Sales go through the same SaleService as the Windows registers: server-side
 * totals, stock decrement, retry protection and the "discount" permission rule.
 */
class PosController extends Controller
{
    public function __construct(private SaleService $sales, private PosSettings $settings) {}

    public function index(Request $request): View
    {
        return view('admin.pos', [
            'store' => $this->settings->all(),
            'canDiscount' => $request->user()->hasPermission('discount'),
        ]);
    }

    /** Exact barcode first, otherwise a name/brand/color search. */
    public function search(Request $request): JsonResponse
    {
        $q = trim((string) $request->query('q', ''));
        if ($q === '') {
            return response()->json(['data' => []]);
        }
        $exact = Product::where('barcode', $q)->first();
        if ($exact) {
            return response()->json(['data' => [$this->item($exact)], 'exact' => true]);
        }
        $query = Product::query();
        foreach (preg_split('/\s+/', $q, -1, PREG_SPLIT_NO_EMPTY) as $token) {
            $like = '%'.str_replace(['%', '_'], ['\%', '\_'], $token).'%';
            $query->where(fn ($w) => $w->where('name', 'like', $like)->orWhere('barcode', 'like', $like)
                ->orWhere('brand', 'like', $like)->orWhere('size', 'like', $like)->orWhere('color', 'like', $like));
        }

        return response()->json(['data' => $query->orderBy('name')->limit(20)->get()->map(fn ($p) => $this->item($p)), 'exact' => false]);
    }

    public function sale(Request $request): JsonResponse
    {
        $v = $request->validate(SaleService::rules());
        $r = $this->sales->create($request->user(), $v);
        $sale = $r['sale'];

        return response()->json([
            'data' => $sale,
            'quantities' => (object) $r['quantities'],
            'receipt_url' => route('admin.pos.receipt', $sale->code),
        ], $r['replayed'] ? 200 : 201);
    }

    public function receipt(string $code): View
    {
        $number = Sale::numberFromCode($code);
        abort_if($number === null, 404);

        return view('admin.receipt', [
            'sale' => Sale::with('lines')->findOrFail($number),
            'store' => $this->settings->all(),
        ]);
    }

    private function item(Product $p): array
    {
        return [
            'id' => $p->id, 'barcode' => $p->barcode, 'name' => $p->name,
            'size' => $p->size, 'color' => $p->color, 'price' => $p->price, 'quantity' => $p->quantity,
        ];
    }
}
