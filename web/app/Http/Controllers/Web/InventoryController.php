<?php

namespace App\Http\Controllers\Web;

use App\Http\Controllers\Controller;
use App\Models\Pos\Product;
use App\Services\Pos\ProductService;
use Illuminate\Http\RedirectResponse;
use Illuminate\Http\Request;
use Illuminate\View\View;

/** Online inventory: the same items, stock and rules as the Windows POS. */
class InventoryController extends Controller
{
    public function __construct(private ProductService $products) {}

    public function index(Request $request): View
    {
        $v = $request->validate([
            'q' => ['nullable', 'string', 'max:200'],
            'category' => ['nullable', 'string', 'max:100'],
            'low' => ['nullable', 'boolean'],
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
        if (! empty($v['low'])) {
            $query->whereColumn('quantity', '<=', 'reorder_level');
        }

        return view('admin.inventory.index', [
            'products' => $query->paginate(50)->withQueryString(),
            'categories' => Product::where('category', '!=', '')->distinct()->orderBy('category')->pluck('category'),
            'filters' => $v,
            'totals' => [
                'items' => Product::count(),
                'units' => (int) Product::where('quantity', '>', 0)->sum('quantity'),
                'retail' => (float) Product::where('quantity', '>', 0)->selectRaw('SUM(quantity * price) as v')->value('v'),
                'low' => Product::whereColumn('quantity', '<=', 'reorder_level')->count(),
            ],
        ]);
    }

    public function create(Request $request): View
    {
        // "Add variant": prefill from an existing item (new size/color, new barcode).
        $template = $request->filled('from') ? Product::find($request->query('from')) : null;

        return view('admin.inventory.form', ['product' => null, 'template' => $template, 'categories' => $this->categories()]);
    }

    public function store(Request $request): RedirectResponse
    {
        $v = $request->validate(ProductService::rules(null));
        $p = $this->products->create($v, $request->user());

        return redirect()->route('admin.inventory.index', ['q' => $p->barcode])->with('status', "Added {$p->name} ({$p->barcode}).");
    }

    public function edit(Product $product): View
    {
        return view('admin.inventory.form', ['product' => $product, 'template' => null, 'categories' => $this->categories()]);
    }

    public function update(Request $request, Product $product): RedirectResponse
    {
        $v = $request->validate(ProductService::rules($product));
        $p = $this->products->update($product, $v, $request->user());

        return redirect()->route('admin.inventory.index', ['q' => $p->barcode])->with('status', "Saved {$p->name}. On hand: {$p->quantity}.");
    }

    public function adjust(Request $request, Product $product): RedirectResponse
    {
        $v = $request->validate(ProductService::adjustRules());
        $p = $this->products->adjust($product, (int) $v['delta'], $v['reason'] ?? null, $request->user());

        return back()->with('status', "{$p->name} ".($v['delta'] > 0 ? '+' : '').$v['delta'].". Now {$p->quantity} on hand.");
    }

    public function destroy(Product $product): RedirectResponse
    {
        $name = $product->name;
        $this->products->delete($product);

        return back()->with('status', "Deleted {$name}. Past sales keep their records.");
    }

    private function categories()
    {
        return Product::where('category', '!=', '')->distinct()->orderBy('category')->pluck('category')
            ->merge(['Dresses', 'Tops', 'Bottoms', 'Outerwear', 'Knitwear', 'Accessories', 'Shoes', 'Jewelry'])->unique()->sort()->values();
    }
}
