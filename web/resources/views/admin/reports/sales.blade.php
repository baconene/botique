@extends('layouts.admin')
@section('title', 'Sales reports')
@php($L = \App\Support\Look::class)
@section('content')
<div class="page-head"><div><span class="eyebrow">The business edit</span><h1>Reports</h1><p class="muted">A clearer view of what is selling.</p></div><button type="button" class="btn print-report">Print this page</button></div>
@include('admin.reports.nav')
<form class="report-filters panel" method="get">
<div class="field"><label for="from">From</label><input type="date" id="from" name="from" value="{{ $from }}" required></div>
<div class="field"><label for="to">To</label><input type="date" id="to" name="to" value="{{ $to }}" required></div>
<button class="btn accent">Apply dates</button><a class="btn" href="{{ route('admin.reports.sales') }}">Last 30 days</a>
</form>
<p class="report-note">Sale dates: {{ $from }} to {{ $to }} · {{ config('app.timezone') }}. Completed sales exclude fully refunded receipts. Refund totals below refer to receipts originally sold in this period, regardless of refund date.</p>
<div class="metric-grid">
<div class="stat featured"><span>Completed sales · incl. tax</span><strong>{{ $L::money($totals->total) }}</strong><small>{{ $totals->orders }} receipts</small></div>
<div class="stat"><span>Tax collected</span><strong>{{ $L::money($totals->tax) }}</strong></div>
<div class="stat"><span>Discounts</span><strong>{{ $L::money($totals->discounts) }}</strong></div>
<div class="stat"><span>Refunded receipts</span><strong>{{ $L::money($refunds) }}</strong></div>
</div>
<div class="two-col section-space">
<section class="panel"><div class="section-heading"><h2>Sales by day</h2><span class="badge">Completed</span></div>
@if($daily->isEmpty())<p class="empty-note">No completed sales in this period. Try another date range.</p>@else
<div class="rank-list scroll-chart">@foreach($daily as $day)<div class="rank-row"><span>{{ $day->day }}</span><meter min="0" max="{{ max(1,$daily->max('total')) }}" value="{{ $day->total }}" aria-label="Sales on {{ $day->day }}"></meter><strong>{{ $L::money($day->total) }}</strong></div>@endforeach</div>
@endif</section>
<section class="panel"><h2>Payment mix</h2><div class="rank-list">@forelse($payments as $payment)<div class="rank-row"><span>{{ $payment->payment_method }}<small>{{ $payment->orders }} receipts</small></span><meter min="0" max="{{ max(1,$totals->total) }}" value="{{ $payment->total }}" aria-label="{{ $payment->payment_method }} sales"></meter><strong>{{ $L::money($payment->total) }}</strong></div>@empty<p class="empty-note">Payment totals will appear after your first sale.</p>@endforelse</div>
<h3 class="section-space">Most purchased</h3>@forelse($top as $item)<div class="list-row"><span>{{ $item->name }}</span><strong>{{ $item->units }} units</strong></div>@empty<p class="muted">No items sold in this period.</p>@endforelse
</section></div>
<section class="panel"><div class="section-heading"><h2>Sales ledger</h2><span class="badge">{{ $sales->total() }} receipts</span></div>
<div class="table-scroll" tabindex="0" role="region" aria-label="Sales ledger"><table class="data"><thead><tr><th>Receipt</th><th>Date</th><th>Cashier</th><th>Payment</th><th>Status</th><th class="num">Total</th></tr></thead><tbody>
@forelse($sales as $sale)<tr><td><a href="{{ route('admin.pos.receipt',$sale->code) }}">{{ $sale->code }}</a></td><td>{{ $sale->sale_date->format('M j, Y H:i') }}</td><td>{{ $sale->cashier ?: '—' }}</td><td>{{ $sale->payment_method }}</td><td><span class="badge {{ $sale->refunded ? 'bad' : 'ok' }}">{{ $sale->refunded ? 'Refunded' : 'Completed' }}</span></td><td class="num">{{ $L::money($sale->total) }}</td></tr>@empty<tr><td colspan="6" class="empty-note">No sales match these dates.</td></tr>@endforelse
</tbody></table></div>
@include('admin.reports.pagination', ['paginator'=>$sales])
</section>
@endsection
