@php($L = \App\Support\Look::class)
<a class="card" href="{{ route('shop.item', $style['id']) }}">
    @include('partials.art', [
        'category' => $style['category'],
        'color' => $style['color'],
        'tag' => $style['in_stock'] <= 0 ? 'Sold out' : ($style['low'] ? 'Only a few left' : null),
    ])
    <div class="body">
        @if ($style['brand'])<span class="brand">{{ $style['brand'] }}</span>@endif
        <span class="name">{{ $style['name'] }}</span>
        @if (count($style['colors']))
            <div class="swatches">
                @foreach (array_slice($style['colors'], 0, 6) as $c)
                    <span class="swatch" title="{{ $c }}" style="background: {{ $L::hex($c) }}"></span>
                @endforeach
            </div>
        @endif
        @if (count($style['sizes_in_stock']))
            <div class="sizes">{{ implode(' · ', $style['sizes_in_stock']) }}</div>
        @endif
        <span class="price">{{ $L::priceRange($style['price_min'], $style['price_max']) }}</span>
    </div>
</a>
