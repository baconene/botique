{{-- Product picture: the photo when there is one, otherwise a tinted garment illustration. --}}
@php($hex = \App\Support\Look::hex($color ?? ''))
<div class="art {{ !empty($image) ? 'has-photo' : '' }}" style="background: color-mix(in srgb, {{ $hex }} 20%, #f6f1ea);">
    @if (!empty($tag))<span class="tag">{{ $tag }}</span>@endif
    @if (!empty($image))
        <img src="{{ \App\Support\Look::img($image, $width ?? 600) }}" alt="{{ $alt ?? '' }}" loading="lazy" decoding="async">
    @else
        <svg viewBox="0 0 200 240" aria-hidden="true">
            <path d="{{ \App\Support\Look::shape($category ?? '') }}" fill="{{ $hex }}" fill-rule="evenodd"
                  stroke="{{ \App\Support\Look::isLight($color ?? '') ? '#cbbfb0' : 'rgba(0,0,0,.18)' }}" stroke-width="2" stroke-linejoin="round"/>
        </svg>
    @endif
</div>
