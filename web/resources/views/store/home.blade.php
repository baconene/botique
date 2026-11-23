@extends('layouts.store')
@php($L = \App\Support\Look::class)

@section('content')
<section class="hero">
    <div class="wrap">
        <div>
            <span class="eyebrow">The {{ date('Y') }} collection</span>
            <h1>Dressed for every <em>quiet</em> moment.</h1>
            <p class="lead">{{ config('pos.tagline') }} Linen, silk and soft knits, curated in our boutique and ready to try on.</p>
            <div class="cta">
                <a class="btn accent" href="{{ route('shop') }}">Shop the collection</a>
                <a class="btn ghost" href="{{ route('shop', ['sort' => 'newest']) }}">See what's new</a>
            </div>
            <div class="hero-stats">
                <div><strong>{{ $styleCount }}</strong><span>styles in stock</span></div>
                <div><strong>{{ $categories->count() }}</strong><span>categories</span></div>
                <div><strong>Live</strong><span>store stock</span></div>
            </div>
        </div>
        <div class="hero-art" aria-hidden="true">
            <div class="tile t1">@include('partials.art', ['category' => 'Dresses', 'color' => 'Sage'])</div>
            <div class="tile t2">@include('partials.art', ['category' => 'Tops', 'color' => 'Ivory'])</div>
            <div class="tile t3">@include('partials.art', ['category' => 'Knitwear', 'color' => 'Camel'])</div>
        </div>
    </div>
</section>

@if ($categories->count())
<section class="block" style="padding-top: 0">
    <div class="wrap">
        <div class="section-head">
            <div><span class="eyebrow">Browse</span><h2>Shop by category</h2></div>
        </div>
        <div class="cats">
            @foreach ($categories as $cat)
                <a class="cat" href="{{ route('shop', ['category' => $cat['name']]) }}">
                    @include('partials.art', ['category' => $cat['name'], 'color' => ['Dresses' => 'Sage', 'Tops' => 'Blush', 'Bottoms' => 'Indigo', 'Knitwear' => 'Camel', 'Accessories' => 'Natural', 'Outerwear' => 'Charcoal'][$cat['name']] ?? $cat['name']])
                    <strong>{{ $cat['name'] }}</strong>
                    <span>{{ $cat['count'] }} {{ \Illuminate\Support\Str::plural('style', $cat['count']) }}</span>
                </a>
            @endforeach
        </div>
    </div>
</section>
@endif

<section class="block" style="padding-top: 8px">
    <div class="wrap">
        <div class="section-head">
            <div><span class="eyebrow">In the boutique now</span><h2>Featured pieces</h2></div>
            <a class="btn ghost small" href="{{ route('shop') }}">View all</a>
        </div>
        @if ($featured->count())
            <div class="grid">
                @foreach ($featured as $style)
                    @include('partials.card', ['style' => $style])
                @endforeach
            </div>
        @else
            <div class="empty">
                <h3>The rails are being restocked</h3>
                <p>New pieces appear here as soon as they're added to inventory.</p>
            </div>
        @endif
    </div>
</section>

<section class="block">
    <div class="wrap">
        <div class="visit">
            <div>
                <span class="eyebrow" style="color:#e7c9d9">Visit us</span>
                <h2>Try it on in store</h2>
                <p class="muted">Everything you see here is on our rails right now. Stock updates live from our register,
                    so if it's on the site, it's in the boutique. Our stylists will have your size ready.</p>
            </div>
            <div>
                <p><strong>{{ $store['StoreName'] }}</strong><br>{{ $store['Address'] }}<br>{{ $store['Phone'] }}</p>
                <a class="btn accent" href="{{ route('shop') }}">Browse the collection</a>
            </div>
        </div>
    </div>
</section>
@endsection
