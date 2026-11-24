<?php

namespace App\Http\Controllers\Web;

use App\Http\Controllers\Controller;
use App\Models\Pos\Product;
use App\Services\Pos\Catalog;
use App\Services\Pos\PosSettings;
use Illuminate\Http\Request;
use Illuminate\View\View;

/** Public demo storefront, showing live stock from the POS database. */
class StorefrontController extends Controller
{
    public function __construct(private Catalog $catalog, private PosSettings $settings) {}

    public function home(): View
    {
        $styles = $this->catalog->styles();

        return view('store.home', [
            'store' => $this->settings->all(),
            'featured' => $styles->where('in_stock', '>', 0)->take(8),
            'newest' => $styles->sortByDesc('newest')->where('in_stock', '>', 0)->take(4),
            'categories' => $this->catalog->categories(),
            'styleCount' => $styles->where('in_stock', '>', 0)->count(),
            // In-stock styles with a photo, for the editorial hero and mood frames.
            'photos' => $styles->where('in_stock', '>', 0)->filter(fn ($s) => $s['image'])->values(),
        ]);
    }

    public function shop(Request $request): View
    {
        $v = $request->validate([
            'category' => ['nullable', 'string', 'max:100'],
            'q' => ['nullable', 'string', 'max:100'],
            'sort' => ['nullable', 'in:featured,newest,price_asc,price_desc'],
        ]);

        return view('store.shop', [
            'store' => $this->settings->all(),
            'styles' => $this->catalog->styles($v['category'] ?? null, $v['q'] ?? null, $v['sort'] ?? 'featured'),
            'categories' => $this->catalog->categories(),
            'filters' => $v,
        ]);
    }

    public function item(Product $product): View
    {
        $style = $this->catalog->styleFor($product);

        return view('store.item', [
            'store' => $this->settings->all(),
            'style' => $style,
            'selected' => $product,
            'related' => $this->catalog->styles($style['category'])->where('id', '!=', $style['id'])->where('in_stock', '>', 0)->take(4),
        ]);
    }
}
