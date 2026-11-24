@extends('layouts.store')

@php
    $L = \App\Support\Look::class;
    $heroMain = $photos->get(0);
    $heroSide = $photos->get(1);
    $expressPick = $photos->get(2) ?? $heroMain;
    $moodPicks = $photos->slice(3, 3)->values();
    if ($moodPicks->count() < 3) { $moodPicks = $photos->take(3)->values(); }
    $used = collect([$heroMain, $heroSide, $expressPick])->merge($moodPicks)->filter()->pluck('id')->all();
    $unused = fn ($s) => $s['image'] && ! in_array($s['id'], $used, true);
    $cherish = $newest->first($unused) ?? $photos->first($unused) ?? $heroSide;
    $fallback = asset('images/editorial-sage.jpg');
@endphp

@section('content')
<link rel="stylesheet" href="{{ asset('css/home.css') }}?v=3">
<script>document.documentElement.classList.add('home-anim'); setTimeout(function () { if (!window.gsap) document.documentElement.classList.remove('home-anim'); }, 2500);</script>

<div class="drq" data-home>

{{-- ============ HERO ============ --}}
<section class="drq-hero" aria-labelledby="hero-title">
    <div class="drq-wrap drq-hero-grid">
        <div class="drq-hero-social" data-hero-in>
            <span>New season</span><span class="drq-dot-sm"></span><span>{{ $styleCount }} styles in store</span>
        </div>

        <h1 id="hero-title" class="drq-title">
            <span class="drq-line"><span data-hero-line><span class="drq-bracket" aria-hidden="true">[</span>Step Into The Edit</span></span>
            <span class="drq-line"><span data-hero-line>Of Your <em>Wardrobe</em></span></span>
        </h1>

        <figure class="drq-hero-main" data-hero-main>
            <img src="{{ $heroMain ? $L::img($heroMain['image'], 900) : $fallback }}" alt="{{ $heroMain ? trim($heroMain['brand'].' '.$heroMain['name']) : 'Style inspiration' }}" fetchpriority="high">
        </figure>

        <div class="drq-hero-left">
            <div class="drq-stat" data-hero-in>
                <div class="drq-avatars" aria-hidden="true">
                    @foreach ($photos->slice(4, 3) as $p)<img src="{{ $L::img($p['image'], 80) }}" alt="">@endforeach
                </div>
                <div><strong data-count="{{ $styleCount }}">{{ $styleCount }}</strong><span>pieces ready to try on</span></div>
            </div>
            <div class="drq-hero-copy" data-hero-in>
                <span class="drq-kicker">Est. in store · online catalogue</span>
                <p>{{ config('pos.tagline') }} Find it online. Make it yours in store.</p>
                <a class="drq-btn" href="{{ route('shop') }}">Explore the collection <span aria-hidden="true">→</span></a>
            </div>
        </div>

        <div class="drq-hero-right">
            <span class="drq-orb" data-orb aria-hidden="true"><span></span></span>
            @if ($heroSide)
                <a class="drq-cut" href="{{ route('shop.item', $heroSide['id']) }}" data-hero-cut>
                    <img src="{{ $L::img($heroSide['image'], 600) }}" alt="{{ trim($heroSide['brand'].' '.$heroSide['name']) }}">
                    <span class="drq-cut-tag">{{ $heroSide['name'] }} · {{ $L::priceRange($heroSide['price_min'], $heroSide['price_max']) }}</span>
                </a>
            @endif
            <p class="drq-hero-note" data-hero-in>your colour,<br>your fit,<br>your way.</p>
        </div>
    </div>
</section>

{{-- ============ EXPRESS YOUR OWN + COLLECTIONS ============ --}}
<section class="drq-express" aria-labelledby="express-title">
    <div class="drq-wrap drq-express-grid">
        <div class="drq-express-copy" data-reveal>
            <span class="drq-pill">Our collections</span>
            <h2 id="express-title" class="drq-h2">Express<br>Your Own</h2>
            <a class="drq-btn" href="{{ route('shop', ['sort' => 'newest']) }}">New arrivals <span aria-hidden="true">→</span></a>
            <div class="drq-express-foot">
                <strong>Dressing for yourself, every day.</strong>
                <p>Easy layers, considered details and pieces you'll reach for again and again.</p>
            </div>
        </div>
        <figure class="drq-express-photo drq-frame" data-reveal-photo>
            <img src="{{ $expressPick ? $L::img($expressPick['image'], 800) : $fallback }}" alt="{{ $expressPick ? trim($expressPick['brand'].' '.$expressPick['name']) : 'Style inspiration' }}" loading="lazy">
            @if ($expressPick)<figcaption>{{ $expressPick['name'] }}</figcaption>@endif
        </figure>
        <div class="drq-collections" data-reveal>
            <h3>Shop by category</h3>
            @if ($categories->count())
                <ul>
                    @foreach ($categories as $cat)
                        <li data-list-item>
                            <a href="{{ route('shop', ['category' => $cat['name']]) }}">
                                <span class="drq-col-name">{{ $cat['name'] }}</span>
                                <span class="drq-col-count">{{ $cat['count'] }} {{ \Illuminate\Support\Str::plural('style', $cat['count']) }}</span>
                                <span class="drq-col-arrow" aria-hidden="true">↗</span>
                            </a>
                        </li>
                    @endforeach
                </ul>
            @else
                <p class="drq-muted">Categories appear here as soon as the collection is online.</p>
            @endif
        </div>
    </div>
</section>

{{-- ============ MOOD (DARK) ============ --}}
@if ($moodPicks->count() >= 3)
<section class="drq-mood" aria-labelledby="mood-title">
    <div class="drq-wrap">
        <div class="drq-mood-head">
            <h2 id="mood-title" class="drq-h2" data-reveal>Ignite<br>Your Style</h2>
            <p data-reveal>Unlock your everyday edge. Mix soft textures with sharp shapes and let one standout piece lead the look.</p>
            <a class="drq-btn light" href="{{ route('shop') }}" data-reveal>See all pieces <span aria-hidden="true">→</span></a>
        </div>
        <div class="drq-mood-frames">
            @foreach ($moodPicks as $i => $pick)
                <a class="drq-mood-frame drq-frame f{{ $i + 1 }}" href="{{ route('shop.item', $pick['id']) }}" data-mood-frame>
                    <img src="{{ $L::img($pick['image'], 700) }}" alt="{{ trim($pick['brand'].' '.$pick['name']) }}" loading="lazy">
                    <span class="drq-mood-label"><span class="drq-play" aria-hidden="true">▸</span>{{ $pick['name'] }}<em>{{ $L::priceRange($pick['price_min'], $pick['price_max']) }}</em></span>
                </a>
            @endforeach
        </div>
    </div>
</section>
@endif

{{-- ============ FEATURED ============ --}}
<section id="collection" class="drq-featured" aria-labelledby="collection-title">
    <div class="drq-wrap">
        <div class="drq-section-head" data-reveal>
            <div><span class="drq-pill">Discover the collection</span><h2 id="collection-title" class="drq-h2">Featured pieces</h2></div>
            <a class="drq-link" href="{{ route('shop') }}">View the full collection ↗</a>
        </div>
        @if ($featured->count())
            <div class="collection-controls" data-collection-filters hidden role="group" aria-label="Filter featured pieces">
                <button type="button" data-category="" aria-pressed="true">All pieces</button>
                @foreach ($featured->pluck('category')->filter()->unique() as $category)
                    <button type="button" data-category="{{ $category }}" aria-pressed="false">{{ $category }}</button>
                @endforeach
            </div>
            <p class="collection-count" data-collection-count role="status">{{ $featured->count() }} featured {{ \Illuminate\Support\Str::plural('style', $featured->count()) }}</p>
            <div class="grid featured-grid">
                @foreach ($featured as $style)
                    <div data-featured-category="{{ $style['category'] }}" data-card>@include('partials.card', ['style' => $style])</div>
                @endforeach
            </div>
        @else
            <div class="collection-empty">
                <div><span class="edition-label">Good things are on the way</span><h3>Our online collection<br>is taking shape.</h3></div>
                <div><p>We're getting the latest pieces ready to browse. In the meantime, get in touch with the boutique to ask about styles, colours and sizes.</p><a class="drq-btn" href="#visit">Contact the boutique</a></div>
            </div>
        @endif
    </div>
</section>

{{-- ============ CHERISH ============ --}}
@if ($newest->count())
@php($withPhoto = $newest->filter(fn ($s) => $s['image'])->values())
<section class="drq-cherish" aria-labelledby="cherish-title" data-swap>
    <div class="drq-wrap drq-cherish-grid">
        <figure class="drq-cherish-photo drq-frame" data-reveal-photo>
            {{-- One photo per new arrival; hovering a row below brings its photo forward. --}}
            @forelse ($withPhoto as $p)
                <img src="{{ $L::img($p['image'], 800) }}" alt="{{ trim($p['brand'].' '.$p['name']) }}" loading="lazy"
                     data-swap-img="{{ $p['id'] }}" class="{{ $loop->first ? 'is-active' : '' }}">
            @empty
                <img src="{{ $cherish ? $L::img($cherish['image'], 800) : $fallback }}" alt="" loading="lazy" class="is-active">
            @endforelse
            <figcaption class="drq-swap-caption" data-swap-caption>{{ $withPhoto->first()['name'] ?? '' }}</figcaption>
        </figure>
        <div class="drq-cherish-copy">
            <span class="drq-pill" data-reveal>Just arrived</span>
            <h2 id="cherish-title" class="drq-h2" data-reveal>Cherish Every<br>Moment…</h2>
            <p data-reveal>New pieces land in the boutique every week. Here's what just arrived. Reserve your size and try it on in store.</p>
            <ol class="drq-new-list">
                @foreach ($newest as $n)
                    <li data-list-item data-swap-row="{{ $n['image'] ? $n['id'] : '' }}" data-swap-name="{{ $n['name'] }}" class="{{ $loop->first ? 'is-active' : '' }}">
                        <a href="{{ route('shop.item', $n['id']) }}">
                            <span class="drq-new-num">0{{ $loop->iteration }}</span>
                            <span class="drq-new-name">{{ $n['name'] }}<small>{{ $n['brand'] }}</small></span>
                            <span class="drq-new-price">{{ $L::priceRange($n['price_min'], $n['price_max']) }}</span>
                            <span class="drq-new-go" aria-hidden="true">→</span>
                        </a>
                    </li>
                @endforeach
            </ol>
        </div>
    </div>
</section>
@endif

{{-- ============ STYLE EDIT ============ --}}
<section id="style-edit" class="drq-style" aria-labelledby="style-title">
    <div class="drq-wrap">
        <div class="drq-section-head" data-reveal>
            <div><span class="drq-pill">A little wardrobe inspiration</span><h2 id="style-title" class="drq-h2">What's your kind of day?</h2></div>
            <p class="drq-muted">Start with a mood.<br>Make the look your own.</p>
        </div>
        <div class="style-studio" data-style-studio data-reveal>
            <div class="style-choices" role="group" aria-label="Choose a style mood" hidden>
                <button type="button" data-style-choice="everyday" aria-pressed="true"><span class="choice-kicker">Relaxed & effortless</span><strong>The everyday edit</strong><span>Linen, soft layers, room to move.</span></button>
                <button type="button" data-style-choice="workday" aria-pressed="false"><span class="choice-kicker">Considered & confident</span><strong>A little more polished</strong><span>Simple shapes, a sharper finish.</span></button>
                <button type="button" data-style-choice="evening" aria-pressed="false"><span class="choice-kicker">Understated & expressive</span><strong>After hours</strong><span>Soft shine, your favourite details.</span></button>
            </div>
            <div class="style-advice" aria-live="polite" aria-atomic="true">
                <article data-style-panel="everyday"><span class="edition-label">Your styling notes</span><h3>Take the slow route.</h3><p>Start with breathable linen. Add a relaxed layer and a neutral accessory. A little texture does the rest.</p><div class="palette" aria-label="Suggested palette: sage, ivory and natural"><span style="--swatch:#819174">Sage</span><span style="--swatch:#eee7d7">Ivory</span><span style="--swatch:#bd966c">Natural</span></div><a class="text-link" href="{{ route('shop', ['q' => 'linen']) }}">Explore linen pieces</a></article>
                <article data-style-panel="workday"><span class="edition-label">Your styling notes</span><h3>A softer kind of statement.</h3><p>Pair structured bottoms with a simple top. Keep the palette focused, then let one considered detail stand out.</p><div class="palette" aria-label="Suggested palette: charcoal, ivory and indigo"><span style="--swatch:#49464a">Charcoal</span><span style="--swatch:#eee7d7">Ivory</span><span style="--swatch:#455975">Indigo</span></div><a class="text-link" href="{{ route('shop', ['category' => 'Bottoms']) }}">Explore bottoms</a></article>
                <article data-style-panel="evening"><span class="edition-label">Your styling notes</span><h3>Stay a little longer.</h3><p>Let silk or a fluid silhouette set the mood. Add your favourite jewellery and keep everything else beautifully simple.</p><div class="palette" aria-label="Suggested palette: plum, blush and black"><span style="--swatch:#6e3456">Plum</span><span style="--swatch:#c88f96">Blush</span><span style="--swatch:#242123">Black</span></div><a class="text-link" href="{{ route('shop', ['q' => 'silk']) }}">Explore silk pieces</a></article>
                <p class="style-disclaimer">Styling ideas, not stock recommendations. Browse to see current availability.</p>
            </div>
        </div>
    </div>
</section>

{{-- ============ VISIT ============ --}}
<section id="visit" class="drq-visit" aria-labelledby="visit-title">
    <div class="drq-wrap drq-visit-grid">
        <div data-reveal><span class="drq-pill dark">The boutique, in person</span><h2 id="visit-title" class="drq-h2">Your next favourite<br>deserves a try-on.</h2><p>See the colour. Feel the fabric. Find the fit that feels right for you. Contact us to confirm sizes and availability before your visit.</p></div>
        <div class="drq-contact" data-reveal><span class="drq-kicker">Come say hello</span><h3>{{ $store['StoreName'] }}</h3>
            @if ($store['Address'])<p>{{ $store['Address'] }}</p>@endif
            @if ($store['Phone'])<a class="drq-phone" href="tel:{{ preg_replace('/[^0-9+]/', '', $store['Phone']) }}">{{ $store['Phone'] }}</a>@endif
            <div class="drq-contact-actions">
                @if ($store['Address'])<a class="drq-btn light" href="https://www.google.com/maps/search/?api=1&amp;query={{ rawurlencode($store['Address']) }}" target="_blank" rel="noopener noreferrer">Get directions <span aria-hidden="true">↗</span><span class="sr-only"> (opens in a new tab)</span></a>@endif
            </div>
            @if (!$store['Address'] && !$store['Phone'])<p>Contact details will be shared here soon.</p>@endif
        </div>
    </div>
</section>

<section class="drq-faq" aria-labelledby="faq-title">
    <div class="drq-wrap drq-faq-grid">
        <div data-reveal><span class="drq-pill">Before you visit</span><h2 id="faq-title" class="drq-h2">A few good<br>things to know.</h2></div>
        <div data-reveal>
            <details><summary>Can I buy online?</summary><p>This is our browsing catalogue. Explore the pieces online, then contact or visit the boutique to purchase.</p></details>
            <details><summary>How do I check my size?</summary><p>Open a product to see its listed sizes and colours. Contact the boutique to confirm availability and ask about fit.</p></details>
            <details><summary>Is what I see in stock?</summary><p>Yes. Stock on this site comes live from the boutique's registers, so sold-out sizes are marked as they sell.</p></details>
        </div>
    </div>
</section>

</div>

<script src="https://cdnjs.cloudflare.com/ajax/libs/gsap/3.12.5/gsap.min.js" defer></script>
<script src="https://cdnjs.cloudflare.com/ajax/libs/gsap/3.12.5/ScrollTrigger.min.js" defer></script>
<script src="{{ asset('js/home.js') }}?v=3" defer></script>
@endsection
