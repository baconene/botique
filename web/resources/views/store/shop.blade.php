@extends('layouts.store')
@section('title', ($filters['category'] ?? 'Shop all').' – '.$store['StoreName'])

@section('content')
<div class="wrap shop-layout">
    <aside class="filters">
        <span class="eyebrow">Categories</span>
        <div style="margin-top: 10px">
            <a href="{{ route('shop', array_filter(['q' => $filters['q'] ?? null, 'sort' => $filters['sort'] ?? null])) }}" class="{{ empty($filters['category']) ? 'active' : '' }}">
                <span>Everything</span>
            </a>
            @foreach ($categories as $cat)
                <a href="{{ route('shop', array_filter(['category' => $cat['name'], 'q' => $filters['q'] ?? null, 'sort' => $filters['sort'] ?? null])) }}"
                   class="{{ ($filters['category'] ?? '') === $cat['name'] ? 'active' : '' }}">
                    <span>{{ $cat['name'] }}</span><span>{{ $cat['count'] }}</span>
                </a>
            @endforeach
        </div>
    </aside>
    <div>
        <h1 style="font-size: 2.6rem">{{ $filters['category'] ?? 'Shop all' }}</h1>
        <div class="toolbar">
            <form method="get" action="{{ route('shop') }}">
                @if (!empty($filters['category']))<input type="hidden" name="category" value="{{ $filters['category'] }}">@endif
                <input type="search" name="q" placeholder="Search name, brand or color" value="{{ $filters['q'] ?? '' }}">
                <select name="sort" onchange="this.form.submit()">
                    @foreach (['featured' => 'Featured', 'newest' => 'Newest', 'price_asc' => 'Price: low to high', 'price_desc' => 'Price: high to low'] as $k => $label)
                        <option value="{{ $k }}" @selected(($filters['sort'] ?? 'featured') === $k)>{{ $label }}</option>
                    @endforeach
                </select>
                <button class="btn small" type="submit">Search</button>
            </form>
            <span class="muted">{{ $styles->count() }} {{ \Illuminate\Support\Str::plural('style', $styles->count()) }}</span>
        </div>
        @if ($styles->count())
            <div class="grid">
                @foreach ($styles as $style)
                    @include('partials.card', ['style' => $style])
                @endforeach
            </div>
        @else
            <div class="empty"><h3>Nothing matches yet</h3><p>Try another search or category.</p></div>
        @endif
    </div>
</div>
@endsection
