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
        $trend = collect();
        $payments = collect();
        if ($staff->hasPermission('sales.view')) {
            $today = Sale::where('sale_date', '>=', today())->where('sale_date', '<', today()->addDay());
            $done = (clone $today)->where('refunded', false)->get();
            $stats = [
                'count' => $done->count(),
                'net' => Money::format($done->sum(fn ($s) => Money::toCents($s->total))),
                'items' => (int) \DB::table('sale_lines')->whereIn('sale_number', $done->pluck('number'))->sum('quantity'),
            ];
            $recent = Sale::orderByDesc('sale_date')->limit(6)->get();
            $daily = Sale::where('sale_date', '>=', today()->subDays(13))->where('sale_date', '<', today()->addDay())
                ->where('refunded', false)->selectRaw('DATE(sale_date) as day, SUM(total) as total')->groupByRaw('DATE(sale_date)')->pluck('total', 'day');
            $trend = collect(range(13, 0))->map(function ($ago) use ($daily) {
                $day = today()->subDays($ago);

                return ['date' => $day->toDateString(), 'label' => $day->format('j'), 'total' => (float) ($daily[$day->toDateString()] ?? 0)];
            });
            $payments = (clone $today)->where('refunded', false)->selectRaw('payment_method, SUM(total) as total')->groupBy('payment_method')->get();
        }
        $canInventory = $staff->hasPermission('inventory.view') || $staff->hasPermission('inventory.edit') || $staff->hasPermission('stock.adjust');
        $low = $canInventory
            ? Product::whereColumn('quantity', '<=', 'reorder_level')->orderBy('quantity')->limit(8)->get()
            : collect();

        return view('admin.dashboard', [
            'staff' => $staff,
            'stats' => $stats,
            'recent' => $recent,
            'trend' => $trend,
            'payments' => $payments,
            'low' => $low,
            'lowCount' => $canInventory ? Product::whereColumn('quantity', '<=', 'reorder_level')->count() : 0,
            'productCount' => $canInventory ? Product::count() : null,
            'units' => $canInventory ? Product::sum('quantity') : null,
            'canInventory' => $canInventory,
        ]);
    }
}
