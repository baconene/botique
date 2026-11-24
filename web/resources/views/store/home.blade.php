@extends('layouts.store')

@section('content')
<section class="editorial-hero wrap" aria-labelledby="hero-title">
    <div class="hero-story">
        <span class="edition-label">A fresh perspective on everyday</span>
        <h1 id="hero-title">Less ordinary.<br>More <em>you.</em></h1>
        <p class="hero-intro">{{ config('pos.tagline') }} Discover your next favourite, from slow mornings to evenings out.</p>
        <div class="hero-links"><a class="btn accent" href="{{ route('shop') }}">Explore the collection</a><a class="text-link" href="#style-edit">Find your style inspiration</a></div>
        <div class="hero-footnote"><span class="fine-rule"></span><p>Find it online. Make it yours in store.</p></div>
    </div>
    <figure class="editorial-image">
        <img src="{{ asset('images/editorial-sage.jpg') }}" alt="Style inspiration: a flowing sage linen dress in a sunlit courtyard" width="1122" height="1402" fetchpriority="high">
        <div class="image-label"><span>THE MOOD</span><strong>Easy, naturally.</strong></div>
        <figcaption>AI-created editorial inspiration · not a product listing</figcaption>
    </figure>
    <a class="hero-side-note" href="#collection">Discover your next favourite</a>
</section>
<div class="editorial-strip"><div class="wrap"><span>Dress for yourself.</span><span>Find your colour.</span><span>Make it an everyday favourite.</span></div></div>

<section id="style-edit" class="style-section wrap" aria-labelledby="style-title">
    <div class="section-heading"><div><span class="edition-label">A little wardrobe inspiration</span><h2 id="style-title">What's your kind of day?</h2></div><p>Start with a mood.<br>Make the look your own.</p></div>
    <div class="style-studio" data-style-studio>
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
</section>

<section id="collection" class="collection-section wrap" aria-labelledby="collection-title">
    <div class="section-heading"><div><span class="edition-label">Discover the collection</span><h2 id="collection-title">Featured pieces</h2></div><a class="text-link" href="{{ route('shop') }}">View the full collection</a></div>
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
                <div data-featured-category="{{ $style['category'] }}">@include('partials.card', ['style' => $style])</div>
            @endforeach
        </div>
    @else
        <div class="collection-empty">
            <div><span class="edition-label">Good things are on the way</span><h3>Our online collection<br>is taking shape.</h3></div>
            <div><p>We're getting the latest pieces ready to browse. In the meantime, get in touch with the boutique to ask about styles, colours and sizes.</p><a class="btn accent" href="#visit">Contact the boutique</a><a class="text-link" href="#style-edit">Explore the style edit</a></div>
        </div>
    @endif
    @if ($categories->count())
        <div class="category-directory"><h3>Shop by category</h3><div>@foreach ($categories as $cat)<a href="{{ route('shop', ['category' => $cat['name']]) }}">{{ $cat['name'] }} <span>{{ $cat['count'] }}</span></a>@endforeach</div></div>
    @endif
</section>

<section id="visit" class="visit-section" aria-labelledby="visit-title">
    <div class="wrap visit-grid">
        <div><span class="edition-label">The boutique, in person</span><h2 id="visit-title">Your next favourite<br>deserves a <em>try-on.</em></h2><p>See the colour. Feel the fabric. Find the fit that feels right for you.</p><p class="visit-caption">Contact us to confirm sizes and availability before your visit.</p></div>
        <div class="contact-card"><span class="edition-label">Come say hello</span><h3>{{ $store['StoreName'] }}</h3>
            @if ($store['Address'])<p>{{ $store['Address'] }}</p>@endif
            @if ($store['Phone'])<a class="contact-phone" href="tel:{{ preg_replace('/[^0-9+]/', '', $store['Phone']) }}">{{ $store['Phone'] }}</a>@endif
            <div class="contact-actions">
                @if ($store['Address'])<a class="btn" href="https://www.google.com/maps/search/?api=1&amp;query={{ rawurlencode($store['Address']) }}" target="_blank" rel="noopener noreferrer">Get directions<span class="sr-only"> (opens in a new tab)</span></a>@endif
                @if ($store['Phone'])<a class="text-link" href="tel:{{ preg_replace('/[^0-9+]/', '', $store['Phone']) }}">Call the boutique</a>@endif
            </div>
            @if (!$store['Address'] && !$store['Phone'])<p>Contact details will be shared here soon.</p>@endif
        </div>
    </div>
</section>
<section class="boutique-faq wrap" aria-labelledby="faq-title"><div><span class="edition-label">Before you visit</span><h2 id="faq-title">A few good<br>things to know.</h2></div><div>
    <details><summary>Can I buy online?</summary><p>This is our browsing catalogue. Explore the pieces online, then contact or visit the boutique to purchase.</p></details>
    <details><summary>How do I check my size?</summary><p>Open a product to see its listed sizes and colours. Contact the boutique to confirm availability and ask about fit.</p></details>
    <details><summary>Are the editorial looks available to buy?</summary><p>The hero image is AI-created style inspiration. Actual inventory, prices and variants are shown in the collection listings.</p></details>
</div></section>
@endsection
