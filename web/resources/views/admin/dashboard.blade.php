@extends('layouts.admin')
@section('title', 'Overview')
@php($L = \App\Support\Look::class)
@section('content')
<div class="page-head"><div><span class="eyebrow">{{ now()->format('l, F j') }}</span><h1>Your boutique, at a glance.</h1><p class="muted">Welcome back, {{ $staff->display_name ?: $staff->username }}. Here is how things are looking.</p></div>
@if($staff->hasPermission('sell'))<a class="btn accent" href="{{ route('admin.pos') }}">New sale</a>@endif</div>
<div class="metric-grid">
@if($stats)
<div class="stat featured"><span>Sales today · incl. tax</span><strong>{{ $L::money($stats['net']) }}</strong><small>Completed receipts only</small></div>
<div class="stat"><span>Transactions</span><strong>{{ $stats['count'] }}</strong><small>Completed today</small></div>
<div class="stat"><span>Pieces sold</span><strong>{{ $stats['items'] }}</strong><small>Units sold today</small></div>
@endif
@if($canInventory)<a class="stat stock-metric" href="{{ route('admin.inventory.index',['low'=>1]) }}"><span>Needs attention</span><strong>{{ $lowCount }}</strong><small>Low or out-of-stock variants</small></a>@endif
</div>
<div class="dashboard-grid">
<div class="dashboard-primary">
@if($stats)
<section class="panel sales-chart-panel">
<div class="section-heading"><div><span class="eyebrow">Your sales story</span><h2>The last two weeks</h2></div><a class="text-link" href="{{ route('admin.reports.sales') }}">Full report</a></div>
<div class="chart-total">{{ $L::money($trend->sum('total')) }}<span>Completed sales, including tax</span></div>
<div class="sales-chart" role="img" aria-label="Daily completed sales for the last fourteen days. Exact figures are listed below.">
@foreach($trend as $day)<div class="chart-column"><div class="chart-track"><div class="chart-bar {{ $loop->last ? 'current' : '' }}" style="height:{{ $day['total'] > 0 ? max(2, $day['total']/max(1,$trend->max('total'))*100) : 0 }}%" title="{{ $day['date'] }}: {{ $L::money($day['total']) }}"></div></div><span>{{ $day['label'] }}</span></div>@endforeach
</div>
@if($trend->sum('total')===0.0)<p class="muted">No completed sales in the last two weeks.</p>@endif
<details class="chart-data"><summary>View daily figures</summary><div class="table-scroll"><table class="data"><thead><tr><th>Date</th><th class="num">Completed sales</th></tr></thead><tbody>@foreach($trend as $day)<tr><td>{{ $day['date'] }}</td><td class="num">{{ $L::money($day['total']) }}</td></tr>@endforeach</tbody></table></div></details>
</section>
@endif
<div class="quick-grid section-space">
@if($staff->hasPermission('sell'))<a class="quick-card" href="{{ route('admin.pos') }}"><img src="{{ asset('icons/bag.svg') }}" alt=""><div><h3>Point of sale</h3><p>From first scan to final receipt.</p></div><span aria-hidden="true">01</span></a>@endif
@if($canInventory)<a class="quick-card" href="{{ route('admin.inventory.index') }}"><img src="{{ asset('icons/boxes.svg') }}" alt=""><div><h3>Online inventory</h3><p>{{ $productCount }} variants · {{ number_format($units) }} units on hand</p></div><span aria-hidden="true">02</span></a>@endif
</div>
@if($stats)
<section class="panel"><div class="section-heading"><h2>Latest sales</h2><a class="text-link" href="{{ route('admin.reports.sales') }}">View all</a></div>
<div class="table-scroll" tabindex="0" role="region" aria-label="Latest sales"><table class="data"><thead><tr><th>Receipt</th><th>Time</th><th>Cashier</th><th class="num">Total</th></tr></thead><tbody>
@forelse($recent as $s)<tr><td><a href="{{ route('admin.pos.receipt',$s->code) }}">{{ $s->code }}</a>@if($s->refunded)<small class="cell-sub">Refunded</small>@endif</td><td>{{ $s->sale_date->format('M j, g:i A') }}</td><td>{{ $s->cashier ?: '—' }}</td><td class="num">{{ $L::money($s->total) }}</td></tr>@empty<tr><td colspan="4" class="empty-note">Your next sale starts the story. Receipts will appear here.</td></tr>@endforelse
</tbody></table></div></section>
@endif
</div>
<aside class="dashboard-rail" aria-label="Boutique insights">
@if($stats)<section class="panel payment-panel"><span class="eyebrow">Today's balance</span><h2>Payment mix</h2>
@forelse($payments as $payment)<div class="payment-row"><span>{{ $payment->payment_method }}</span><strong>{{ $L::money($payment->total) }}</strong><meter min="0" max="{{ max(1,(float)$stats['net']) }}" value="{{ $payment->total }}" aria-label="{{ $payment->payment_method }} sales today"></meter></div>@empty<p class="muted">Cash, card and other payments will appear here after checkout.</p>@endforelse
<p class="report-note">Completed sales only.</p></section>@endif
@if($canInventory)<section class="panel"><div class="section-heading"><div><span class="eyebrow">Keep the rails ready</span><h2>Stock watch</h2></div><span class="badge warn">{{ $lowCount }}</span></div>
@forelse($low as $p)<div class="stock-row"><div><strong>{{ $p->name }}</strong><small>{{ trim($p->size.' / '.$p->color,' /') ?: 'No variant' }}</small></div><span class="badge {{ $p->quantity<=0 ? 'bad' : 'warn' }}">{{ $p->quantity }} left</span></div>@empty<p class="muted">{{ $productCount ? 'Every item is above its reorder level.' : 'Add your first pieces to start tracking stock.' }}</p>@endforelse
<a class="btn rail-button" href="{{ route('admin.reports.inventory',['stock'=>'low']) }}">Review stock report</a></section>@endif
<section class="boutique-note"><span class="eyebrow">The storefront edit</span><h2>Make a lovely<br>first impression.</h2><p>Your collection, ready to discover.</p><a href="{{ route('home') }}" target="_blank" rel="noopener">Visit your storefront</a></section>
</aside>
</div>
@endsection
