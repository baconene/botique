@extends('layouts.admin')
@section('title', 'Dashboard')
@php($L = \App\Support\Look::class)

@section('content')
<div class="page-head">
    <div>
        <span class="eyebrow">{{ now()->format('l, F j') }}</span>
        <h1>Hello, {{ $staff->display_name ?: $staff->username }}</h1>
    </div>
</div>

<div class="tiles">
    @if ($staff->hasPermission('sell'))
        <a class="tile-link" href="{{ route('admin.pos') }}">
            <div class="icon">▣</div>
            <h3>Point of sale</h3>
            <p class="muted" style="margin:0">Ring up a sale in the browser: scan or search items, take cash or card, print a receipt.</p>
        </a>
    @endif
    @if ($canInventory)
        <a class="tile-link" href="{{ route('admin.inventory.index') }}">
            <div class="icon">▤</div>
            <h3>Online inventory</h3>
            <p class="muted" style="margin:0">{{ $productCount }} items. Add pieces, edit prices and adjust stock. Changes appear on the storefront right away.</p>
        </a>
    @endif
    <a class="tile-link" href="{{ route('home') }}" target="_blank">
        <div class="icon">↗</div>
        <h3>Storefront</h3>
        <p class="muted" style="margin:0">See the public shop exactly as customers do, with live stock.</p>
    </a>
</div>

@if ($stats)
    <div class="tiles">
        <div class="stat"><span>Sales today</span><strong>{{ $stats['count'] }}</strong></div>
        <div class="stat"><span>Net sales today</span><strong>{{ config('pos.currency_symbol') }}{{ number_format((float) $stats['net'], 2) }}</strong></div>
        <div class="stat"><span>Items sold today</span><strong>{{ $stats['items'] }}</strong></div>
        @if ($canInventory)<div class="stat"><span>Low or out of stock</span><strong>{{ $lowCount }}</strong></div>@endif
    </div>
@endif

<div class="two-col">
    @if ($stats)
        <div class="panel">
            <h3>Latest sales</h3>
            @if ($recent->count())
                <table class="data">
                    <thead><tr><th>Receipt</th><th>Time</th><th>Cashier</th><th class="num">Total</th></tr></thead>
                    <tbody>
                    @foreach ($recent as $s)
                        <tr>
                            <td><a href="{{ route('admin.pos.receipt', $s->code) }}">{{ $s->code }}</a> @if ($s->refunded)<span class="badge bad">Refunded</span>@endif</td>
                            <td>{{ $s->sale_date->format('M j, g:i A') }}</td>
                            <td>{{ $s->cashier ?: '—' }}</td>
                            <td class="num">{{ $L::money($s->total) }}</td>
                        </tr>
                    @endforeach
                    </tbody>
                </table>
            @else
                <p class="muted">No sales yet.</p>
            @endif
        </div>
    @endif
    @if ($canInventory)
        <div class="panel">
            <h3>Needs restocking</h3>
            @if ($low->count())
                <table class="data">
                    <thead><tr><th>Item</th><th>Size / color</th><th class="num">On hand</th></tr></thead>
                    <tbody>
                    @foreach ($low as $p)
                        <tr>
                            <td>{{ $p->name }}</td>
                            <td>{{ trim($p->size.' / '.$p->color, ' /') ?: '—' }}</td>
                            <td class="num"><span class="badge {{ $p->quantity <= 0 ? 'bad' : 'warn' }}">{{ $p->quantity }}</span></td>
                        </tr>
                    @endforeach
                    </tbody>
                </table>
            @else
                <p class="muted">Everything is above its low-stock alert level.</p>
            @endif
        </div>
    @endif
</div>
@endsection
