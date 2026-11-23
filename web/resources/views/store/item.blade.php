@extends('layouts.store')
@section('title', $style['name'].' – '.$store['StoreName'])
@php($L = \App\Support\Look::class)

@section('content')
<div class="wrap item-layout">
    <div class="item-art">
        @include('partials.art', ['category' => $style['category'], 'color' => $selected->color ?: $style['color'], 'tag' => $style['in_stock'] <= 0 ? 'Sold out' : null])
    </div>
    <div>
        <a class="muted" href="{{ route('shop', ['category' => $style['category']]) }}">&larr; {{ $style['category'] ?: 'Shop' }}</a>
        @if ($style['brand'])<p class="eyebrow" style="margin: 18px 0 6px">{{ $style['brand'] }}</p>@endif
        <h1 style="font-size: 3rem">{{ $style['name'] }}</h1>
        <p style="font-size: 1.4rem; font-weight: 600">{{ $L::priceRange($style['price_min'], $style['price_max']) }}</p>

        @if (count($style['colors']))
            <label>Color</label>
            <div class="swatches" style="gap: 10px; margin-bottom: 1.2em">
                @foreach ($style['colors'] as $c)
                    <span class="swatch" style="width: 28px; height: 28px; background: {{ $L::hex($c) }}" title="{{ $c }}"></span>
                @endforeach
                <span class="muted" style="align-self: center">{{ implode(', ', $style['colors']) }}</span>
            </div>
        @endif

        @if (count($style['sizes']))
            <label>Size</label>
            <div class="size-picker">
                @foreach ($style['sizes'] as $s)
                    <span class="{{ in_array($s, $style['sizes_in_stock']) ? '' : 'out' }}">{{ $s }}</span>
                @endforeach
            </div>
        @endif

        @if ($style['in_stock'] > 0)
            <span class="badge {{ $style['low'] ? 'warn' : 'ok' }}">{{ $style['low'] ? 'Only '.$style['in_stock'].' left in store' : 'In stock in our boutique' }}</span>
        @else
            <span class="badge bad">Sold out — ask us about restocks</span>
        @endif

        <div class="note">
            <strong>Reserve in store.</strong> Online checkout is coming soon. Call us at {{ $store['Phone'] ?: 'the boutique' }}
            or visit {{ $store['Address'] ?: 'our store' }} and we'll hold your size.
        </div>

        <table class="variant-table">
            <thead><tr><th>Size</th><th>Color</th><th>Price</th><th>Availability</th></tr></thead>
            <tbody>
            @foreach ($style['variants'] as $v)
                <tr>
                    <td>{{ $v->size ?: '—' }}</td>
                    <td>{{ $v->color ?: '—' }}</td>
                    <td>{{ $L::money($v->price) }}</td>
                    <td>
                        @if ($v->quantity <= 0)<span class="badge bad">Sold out</span>
                        @elseif ($v->quantity <= 2)<span class="badge warn">Last {{ $v->quantity }}</span>
                        @else<span class="badge ok">Available</span>@endif
                    </td>
                </tr>
            @endforeach
            </tbody>
        </table>
    </div>
</div>

@if ($related->count())
<section class="block" style="padding-top: 0">
    <div class="wrap">
        <div class="section-head"><h2>You may also like</h2></div>
        <div class="grid">
            @foreach ($related as $r)
                @include('partials.card', ['style' => $r])
            @endforeach
        </div>
    </div>
</section>
@endif
@endsection
