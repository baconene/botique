@extends('layouts.admin')
@section('title', 'Inventory')
@php($L = \App\Support\Look::class)
@php($me = auth('staff')->user())
@php($canEdit = $me->hasPermission('inventory.edit'))
@php($canAdjust = $me->hasPermission('stock.adjust'))

@section('content')
<div class="page-head">
    <div>
        <span class="eyebrow">Online inventory</span>
        <h1>Inventory</h1>
        <p class="muted" style="margin:0">{{ $totals['items'] }} items · {{ $totals['units'] }} units on hand · retail value {{ $L::money($totals['retail']) }} · {{ $totals['low'] }} low or out of stock</p>
    </div>
    @if ($canEdit)<a class="btn accent" href="{{ route('admin.inventory.create') }}">+ Add item</a>@endif
</div>

<form class="toolbar" method="get" action="{{ route('admin.inventory.index') }}">
    <input type="search" name="q" value="{{ $filters['q'] ?? '' }}" placeholder="Search name, barcode, brand, size, color" style="max-width: 340px">
    <select name="category" style="max-width: 200px" onchange="this.form.submit()">
        <option value="">All categories</option>
        @foreach ($categories as $c)<option @selected(($filters['category'] ?? '') === $c)>{{ $c }}</option>@endforeach
    </select>
    <label style="display:flex; gap:6px; align-items:center; margin:0; font-weight:500">
        <input type="checkbox" name="low" value="1" style="width:auto" @checked(!empty($filters['low'])) onchange="this.form.submit()"> Low stock only
    </label>
    <button class="btn small" type="submit">Search</button>
</form>

<div class="panel" style="padding: 0; overflow-x: auto">
    <table class="data">
        <thead>
        <tr><th>Barcode</th><th>Item</th><th>Category</th><th>Size / color</th><th class="num">Price</th><th class="num">On hand</th><th></th></tr>
        </thead>
        <tbody>
        @forelse ($products as $p)
            <tr>
                <td><code>{{ $p->barcode }}</code></td>
                <td>
                    <div class="item-cell">
                        <div class="thumb">@include('partials.art', ['category' => $p->category, 'color' => $p->color, 'image' => $p->imageUrl(), 'width' => 120, 'alt' => $p->name])</div>
                        <div><strong>{{ $p->name }}</strong>@if ($p->brand)<br><span class="muted" style="font-size:.82rem">{{ $p->brand }}</span>@endif</div>
                    </div>
                </td>
                <td>{{ $p->category ?: '—' }}</td>
                <td>{{ trim($p->size.' / '.$p->color, ' /') ?: '—' }}</td>
                <td class="num">{{ $L::money($p->price) }}</td>
                <td class="num">
                    <span class="badge {{ $p->quantity <= 0 ? 'bad' : ($p->quantity <= $p->reorder_level ? 'warn' : 'ok') }}">{{ $p->quantity }}</span>
                </td>
                <td>
                    <div class="actions">
                        @if ($canAdjust)
                            <form class="adjust" method="post" action="{{ route('admin.inventory.adjust', $p) }}" title="Receive (+) or remove (−) stock">
                                @csrf
                                <input type="number" name="delta" placeholder="±" required>
                                <input type="hidden" name="reason" value="web adjust">
                                <button class="btn small ghost" type="submit">Adjust</button>
                            </form>
                        @endif
                        @if ($canEdit)
                            <a class="btn small ghost" href="{{ route('admin.inventory.edit', $p) }}">Edit</a>
                            <a class="btn small ghost" href="{{ route('admin.inventory.create', ['from' => $p->id]) }}" title="Same item in another size or color">+ Variant</a>
                            <form method="post" action="{{ route('admin.inventory.destroy', $p) }}" onsubmit="return confirm('Delete {{ addslashes($p->name) }} ({{ $p->barcode }})? Past sales keep their records.')">
                                @csrf @method('DELETE')
                                <button class="btn small danger" type="submit">Delete</button>
                            </form>
                        @endif
                    </div>
                </td>
            </tr>
        @empty
            <tr><td colspan="7" class="muted" style="padding: 32px; text-align:center">No items match. @if ($canEdit)<a href="{{ route('admin.inventory.create') }}">Add the first one</a>.@endif</td></tr>
        @endforelse
        </tbody>
    </table>
</div>

@if ($products->hasPages())
    <div class="pagination">
        @if ($products->previousPageUrl())<a href="{{ $products->previousPageUrl() }}">&larr; Previous</a>@endif
        <span class="current">Page {{ $products->currentPage() }} of {{ $products->lastPage() }}</span>
        @if ($products->nextPageUrl())<a href="{{ $products->nextPageUrl() }}">Next &rarr;</a>@endif
    </div>
@endif
@endsection
