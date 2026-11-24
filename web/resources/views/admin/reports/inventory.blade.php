@extends('layouts.admin')
@section('title', 'Inventory reports')
@php($L = \App\Support\Look::class)
@section('content')
<div class="page-head"><div><span class="eyebrow">The business edit</span><h1>Reports</h1><p class="muted">Know your stock. Plan your next collection.</p></div><button type="button" class="btn print-report">Print this page</button></div>
@include('admin.reports.nav')
<form class="report-filters panel" method="get">
<div class="field"><label for="category">Category</label><select id="category" name="category"><option value="">All categories</option>@foreach($categories as $category)<option @selected(($filters['category']??'')===$category)>{{ $category }}</option>@endforeach</select></div>
<div class="field"><label for="stock">Stock status</label><select id="stock" name="stock"><option value="all">All stock</option><option value="low" @selected(($filters['stock']??'')==='low')>Low or out of stock</option><option value="out" @selected(($filters['stock']??'')==='out')>Out of stock</option></select></div>
<button class="btn accent">Apply filters</button><a class="btn" href="{{ route('admin.reports.inventory') }}">Reset</a></form>
<p class="report-note">Current stock snapshot · {{ now()->format('M j, Y H:i') }} {{ config('app.timezone') }}. Values reflect current unit prices and costs, not historical valuation. Totals follow the selected filters.</p>
<div class="metric-grid">
<div class="stat featured"><span>Units on hand</span><strong>{{ number_format($totals->units) }}</strong><small>{{ $totals->items }} product variants</small></div>
<div class="stat"><span>Inventory at cost</span><strong>{{ $L::money($totals->cost) }}</strong></div>
<div class="stat"><span>Inventory at retail</span><strong>{{ $L::money($totals->retail) }}</strong></div>
<div class="stat"><span>Retail less cost</span><strong>{{ $L::money($totals->retail - $totals->cost) }}</strong><small>Before tax, discounts & expenses</small></div>
</div>
<section class="panel section-space"><div class="section-heading"><h2>Stock by category</h2><span class="badge">Units on hand</span></div><div class="rank-list">
@forelse($breakdown as $category)<div class="rank-row"><span>{{ $category->category ?: 'Uncategorized' }}<small>{{ $category->items }} variants</small></span><meter min="0" max="{{ max(1,$breakdown->max('units')) }}" value="{{ max(0,$category->units) }}" aria-label="{{ $category->category }} units"></meter><strong>{{ number_format($category->units) }}</strong></div>@empty<p class="empty-note">No inventory matches these filters.</p>@endforelse
</div></section>
<section class="panel"><div class="section-heading"><h2>Inventory detail</h2><span class="badge">Lowest stock first</span></div>
<div class="table-scroll" tabindex="0" role="region" aria-label="Inventory detail"><table class="data"><thead><tr><th>Item / barcode</th><th>Category</th><th>Variant</th><th class="num">Units</th><th class="num">Reorder at</th><th class="num">Unit cost</th><th class="num">Retail value</th></tr></thead><tbody>
@forelse($products as $product)<tr><td><strong>{{ $product->name }}</strong><small class="cell-sub">{{ $product->barcode }}</small></td><td>{{ $product->category ?: '—' }}</td><td>{{ trim($product->size.' / '.$product->color,' /') ?: '—' }}</td><td class="num"><span class="badge {{ $product->quantity<=0 ? 'bad' : ($product->quantity<=$product->reorder_level ? 'warn' : 'ok') }}">{{ $product->quantity }}</span></td><td class="num">{{ $product->reorder_level }}</td><td class="num">{{ $L::money($product->cost) }}</td><td class="num">{{ $L::money(max(0,$product->quantity)*$product->price) }}</td></tr>@empty<tr><td colspan="7" class="empty-note">No items match these filters.</td></tr>@endforelse
</tbody></table></div>
@include('admin.reports.pagination', ['paginator'=>$products])
</section>
@endsection
