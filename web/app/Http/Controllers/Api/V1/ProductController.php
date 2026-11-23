<?php

namespace App\Http\Controllers\Api\V1;

use App\Http\Controllers\Controller;
use App\Models\Pos\Product;
use App\Services\Pos\Inventory;
use Illuminate\Http\JsonResponse;
use Illuminate\Http\Request;
use Illuminate\Support\Carbon;
use Illuminate\Support\Facades\DB;
use Illuminate\Validation\Rule;

class ProductController extends Controller
{
    private const BARCODE = ['string', 'max:64', 'regex:/^[\x20-\x7E]+$/'];   // Code 128B: printable ASCII

    public function __construct(private Inventory $inventory) {}

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
        $v = $request->validate([
            'id' => ['nullable', 'regex:/^[0-9a-f]{32}$/', 'unique:products,id'],
            'barcode' => array_merge(['nullable'], self::BARCODE, ['unique:products,barcode']),
        ] + $this->fieldRules(true) + [
            'quantity' => ['nullable', 'integer', 'min:-99999', 'max:999999'],
        ]);
        $v = $this->clean($v);

        $product = DB::transaction(function () use ($v, $request) {
            $p = new Product($v);
            $p->id = $v['id'] ?? Product::newId();
            $p->barcode = $v['barcode'] ?? $this->inventory->nextBarcode();
            $p->quantity = $v['quantity'] ?? 0;
            $p->save();
            $this->inventory->log($p->id, $p->quantity, 'new item', '', $request->user()->terminalLabel());

            return $p;
        });

        return response()->json(['data' => $product->fresh()], 201);
    }

    /** PUT /products/{id}. Stock is changed with quantity_delta (relative), never overwritten. */
    public function update(Request $request, Product $product): JsonResponse
    {
        $v = $request->validate([
            'barcode' => array_merge(['sometimes'], self::BARCODE, [Rule::unique('products', 'barcode')->ignore($product->id)]),
        ] + $this->fieldRules(false) + [
            'quantity_delta' => ['nullable', 'integer', 'min:-99999', 'max:99999'],
        ]);
        $v = $this->clean($v);

        DB::transaction(function () use ($v, $product, $request) {
            $product->fill(collect($v)->except('quantity_delta')->all())->save();
            if (! empty($v['quantity_delta'])) {
                $this->inventory->change($product->id, (int) $v['quantity_delta'], 'edit', '', $request->user()->terminalLabel());
            }
        });

        return response()->json(['data' => $product->fresh()]);
    }

    /** POST /products/{id}/adjust  {delta, reason} */
    public function adjust(Request $request, Product $product): JsonResponse
    {
        $v = $request->validate([
            'delta' => ['required', 'integer', 'min:-99999', 'max:99999', 'not_in:0'],
            'reason' => ['nullable', 'string', 'max:40'],
        ]);
        DB::transaction(fn () => $this->inventory->change($product->id, (int) $v['delta'], $v['reason'] ?? 'adjustment', '', $request->user()->terminalLabel()));

        return response()->json(['data' => $product->fresh()]);
    }

    public function destroy(Product $product): JsonResponse
    {
        DB::transaction(function () use ($product) {
            DB::table('pos_deleted_products')->insert([
                'product_id' => $product->id, 'barcode' => $product->barcode, 'deleted_at' => now(),
            ]);
            $product->delete();   // past sale lines keep their own copy of name/barcode/price
        });

        return response()->json(null, 204);
    }

    /** POS columns are NOT NULL with '' / 0 defaults, so blank optional fields become '' / 0. */
    private function clean(array $v): array
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

    private function fieldRules(bool $creating): array
    {
        $req = $creating ? 'required' : 'sometimes';

        return [
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
    }
}
