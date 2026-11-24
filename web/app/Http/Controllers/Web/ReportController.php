<?php

namespace App\Http\Controllers\Web;

use App\Http\Controllers\Controller;
use App\Models\Pos\Product;
use App\Models\Pos\Sale;
use Illuminate\Http\Request;
use Illuminate\Support\Carbon;
use Illuminate\Support\Facades\DB;
use Illuminate\Validation\ValidationException;
use Illuminate\View\View;

class ReportController extends Controller
{
    public function sales(Request $request): View
    {
        $v = $request->validate([
            'from' => ['nullable', 'date_format:Y-m-d'],
            'to' => ['nullable', 'date_format:Y-m-d'],
        ]);
        $from = Carbon::parse($v['from'] ?? today()->subDays(29))->startOfDay();
        $to = Carbon::parse($v['to'] ?? today())->startOfDay();
        if ($to->lt($from) || $from->diffInDays($to) > 365) {
            throw ValidationException::withMessages(['to' => 'Choose an end date after the start, within a 366-day range.']);
        }
        $range = Sale::where('sale_date', '>=', $from)->where('sale_date', '<', $to->copy()->addDay());
        $completed = (clone $range)->where('refunded', false);
        $totals = (clone $completed)->selectRaw('COUNT(*) as orders, COALESCE(SUM(total),0) as total, COALESCE(SUM(tax),0) as tax, COALESCE(SUM(discount_amount),0) as discounts')->first();
        $refunds = (clone $range)->where('refunded', true)->sum('total');
        $daily = (clone $completed)->selectRaw('DATE(sale_date) as day, SUM(total) as total')->groupByRaw('DATE(sale_date)')->orderBy('day')->get();
        $payments = (clone $completed)->selectRaw('payment_method, COUNT(*) as orders, SUM(total) as total')->groupBy('payment_method')->get();
        $top = DB::table('sale_lines')->join('sales', 'sales.number', '=', 'sale_lines.sale_number')
            ->where('sales.sale_date', '>=', $from)->where('sales.sale_date', '<', $to->copy()->addDay())->where('sales.refunded', false)
            ->selectRaw('sale_lines.name, SUM(sale_lines.quantity) as units')->groupBy('sale_lines.name')->orderByDesc('units')->limit(5)->get();

        return view('admin.reports.sales', [
            'from' => $from->toDateString(), 'to' => $to->toDateString(),
            'totals' => $totals, 'refunds' => $refunds, 'daily' => $daily, 'payments' => $payments, 'top' => $top,
            'sales' => $range->orderByDesc('sale_date')->paginate(25)->withQueryString(),
        ]);
    }

    public function inventory(Request $request): View
    {
        $v = $request->validate(['category' => ['nullable', 'string', 'max:100'], 'stock' => ['nullable', 'in:all,low,out']]);
        $query = Product::query()->when($v['category'] ?? null, fn ($q, $category) => $q->where('category', $category));
        if (($v['stock'] ?? '') === 'low') {
            $query->whereColumn('quantity', '<=', 'reorder_level');
        } elseif (($v['stock'] ?? '') === 'out') {
            $query->where('quantity', '<=', 0);
        }
        $totals = (clone $query)->selectRaw('COUNT(*) as items, COALESCE(SUM(quantity),0) as units, COALESCE(SUM(CASE WHEN quantity > 0 THEN quantity * cost ELSE 0 END),0) as cost, COALESCE(SUM(CASE WHEN quantity > 0 THEN quantity * price ELSE 0 END),0) as retail')->first();
        $categories = (clone $query)->selectRaw('category, COUNT(*) as items, SUM(quantity) as units')->groupBy('category')->orderByDesc('units')->get();

        return view('admin.reports.inventory', [
            'totals' => $totals, 'breakdown' => $categories, 'filters' => $v,
            'categories' => Product::where('category', '!=', '')->distinct()->orderBy('category')->pluck('category'),
            'products' => $query->orderBy('quantity')->orderBy('name')->paginate(25)->withQueryString(),
        ]);
    }
}
