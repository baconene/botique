<?php

namespace App\Http\Controllers\Api\V1;

use App\Http\Controllers\Controller;
use App\Models\Pos\Product;
use App\Services\Pos\ProductService;
use Illuminate\Http\JsonResponse;
use Illuminate\Http\Request;
use Illuminate\Support\Carbon;
use Illuminate\Support\Facades\DB;

class ProductController extends Controller
{
    public function __construct(private ProductService $products) {}

    /**
     * GET /products?q=&category=&low_stock=1&updated_since=&per_page=
     * With updated_since, the response also lists items deleted since then (deleted_ids) so registers can sync.
     */
    public function index(Request $request): JsonResponse
    {
        $v = $request->validate([
            'q' => ['nullable', 'string', 'max:200'],
            'category' => ['nullable', 'string', 'max:100'],
            'low_stock' => ['nullable', 'boolean'],
            'updated_since' => ['nullable', 'date'],
            'per_page' => ['nullable', 'integer', 'min:1', 'max:1000'],
        ]);

        $query = Product::query()->orderBy('name')->orderBy('color')->orderBy('size');
        foreach (preg_split('/\s+/', trim($v['q'] ?? ''), -1, PREG_SPLIT_NO_EMPTY) as $token) {
            $like = '%'.str_replace(['%', '_'], ['\%', '\_'], $token).'%';
            $query->where(fn ($w) => $w->where('name', 'like', $like)->orWhere('barcode', 'like', $like)
                ->orWhere('brand', 'like', $like)->orWhere('category', 'like', $like)
                ->orWhere('size', 'like', $like)->orWhere('color', 'like', $like));
        }
        if (! empty($v['category'])) {
            $query->where('category', $v['category']);
        }
        if (! empty($v['low_stock'])) {
            $query->whereColumn('quantity', '<=', 'reorder_level');
        }
        $since = isset($v['updated_since']) ? Carbon::parse($v['updated_since'])->setTimezone(config('app.timezone')) : null;
        if ($since) {
            $query->where('updated', '>=', $since);
        }

        $page = $query->paginate($v['per_page'] ?? 100);
        $body = [
            'data' => $page->items(),
            'meta' => ['current_page' => $page->currentPage(), 'last_page' => $page->lastPage(), 'total' => $page->total()],
            'server_time' => now()->format('Y-m-d\TH:i:sP'),
        ];
        if ($since) {
            $body['deleted_ids'] = DB::table('pos_deleted_products')->where('deleted_at', '>=', $since)->pluck('product_id')->unique()->values();
        }

        return response()->json($body);
    }

    public function show(Product $product): JsonResponse
    {
        return response()->json(['data' => $product]);
    }

    public function byBarcode(string $barcode): JsonResponse
    {
        return response()->json(['data' => Product::where('barcode', $barcode)->firstOrFail()]);
    }

    public function store(Request $request): JsonResponse
    {
        $v = $request->validate(ProductService::rules(null));

        return response()->json(['data' => $this->products->create($v, $request->user())], 201);
    }

    /** PUT /products/{id}. Stock is changed with quantity_delta (relative), never overwritten. */
    public function update(Request $request, Product $product): JsonResponse
    {
        $v = $request->validate(ProductService::rules($product));

        return response()->json(['data' => $this->products->update($product, $v, $request->user())]);
    }

    /** POST /products/{id}/adjust  {delta, reason} */
    public function adjust(Request $request, Product $product): JsonResponse
    {
        $v = $request->validate(ProductService::adjustRules());

        return response()->json(['data' => $this->products->adjust($product, (int) $v['delta'], $v['reason'] ?? null, $request->user())]);
    }

    public function destroy(Product $product): JsonResponse
    {
        $this->products->delete($product);

        return response()->json(null, 204);
    }
}