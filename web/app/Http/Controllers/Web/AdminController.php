<?php

namespace App\Http\Controllers\Web;

use App\Http\Controllers\Controller;
use App\Models\Pos\Product;
use App\Models\Pos\Sale;
use App\Services\Pos\Money;
use Illuminate\Http\Request;
use Illuminate\View\View;

class AdminController extends Controller
{
    public function dashboard(Request $request): View
    {
        $staff = $request->user();
        $stats = null;
        $recent = collect();
        if ($staff->hasPermission('sales.view')) {
            $today = Sale::where('sale_date', '>=', today())->where('sale_date', '<', today()->addDay());
            $done = (clone $today)->where('refunded', false)->get();
            $stats = [
                'count' => $done->count(),
                'net' => Money::format($done->sum(fn ($s) => Money::toCents($s->total))),
                'items' => (int) \DB::table('sale_lines')->whereIn('sale_number', $done->pluck('number'))->sum('quantity'),
            ];
            $recent = Sale::orderByDesc('sale_date')->limit(6)->get();
        }
        $canInventory = $staff->hasPermission('inventory.view') || $staff->hasPermission('inventory.edit') || $staff->hasPermission('stock.adjust');
        $low = $canInventory
            ? Product::whereColumn('quantity', '<=', 'reorder_level')->orderBy('quantity')->limit(8)->get()
            : collect();

        return view('admin.dashboard', [
            'staff' => $staff,
            'stats' => $stats,
            'recent' => $recent,
            'low' => $low,
            'lowCount' => $canInventory ? Product::whereColumn('quantity', '<=', 'reorder_level')->count() : 0,
            'productCount' => Product::count(),
            'canInventory' => $canInventory,
        ]);
    }
}
