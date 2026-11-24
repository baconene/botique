<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8">
    <meta name="viewport" content="width=device-width, initial-scale=1">
    <title>@yield('title', $store['StoreName'] ?? 'Boutique')</title>
    <meta name="description" content="{{ config('pos.tagline') }}">
    <link rel="preconnect" href="https://fonts.googleapis.com">
    <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
    <link href="https://fonts.googleapis.com/css2?family=Cormorant+Garamond:wght@500;600;700&family=Inter:wght@400;500;600&display=swap" rel="stylesheet">
    <link rel="stylesheet" href="{{ asset('css/app.css') }}?v=3">
    <link rel="stylesheet" href="{{ asset('css/storefront.css') }}?v=2">
    <script src="{{ asset('js/storefront.js') }}?v=2" defer></script>
</head>
<body class="storefront">
    <a class="skip-link" href="#main-content">Skip to content</a>
    <div class="store-note">A little inspiration. A wardrobe that feels like you.</div>
    <header class="boutique-header">
        <div class="wrap header-inner">
            <a class="brandmark" href="{{ route('home') }}">{{ $store['StoreName'] }}<span>THE EVERYDAY EDIT</span></a>
            <nav class="desktop-nav" aria-label="Main navigation">
                <a href="{{ route('shop') }}" @if(request()->routeIs('shop') && !request('sort')) aria-current="page" @endif>Collection</a>
                <a href="{{ route('shop', ['sort' => 'newest']) }}">New arrivals</a>
                <a href="{{ route('home') }}#style-edit">The style edit</a>
                <a href="{{ route('home') }}#visit">Visit us</a>
            </nav>
            <details class="header-search">
                <summary>Search</summary>
                <form action="{{ route('shop') }}" method="get" role="search">
                    <label for="store-search">Find something you love</label>
                    <div class="search-row"><input id="store-search" type="search" name="q" maxlength="100" placeholder="Try linen, sage, a favourite brand…" value="{{ request('q') }}"><button class="btn accent" type="submit">Search collection</button></div>
                </form>
            </details>
            <details class="mobile-navigation">
                <summary>Menu</summary>
                <nav aria-label="Mobile navigation">
                    <a href="{{ route('shop') }}">Collection</a>
                    <a href="{{ route('shop', ['sort' => 'newest']) }}">New arrivals</a>
                    <a href="{{ route('home') }}#style-edit">The style edit</a>
                    <a href="{{ route('home') }}#visit">Visit us</a>
                </nav>
            </details>
        </div>
    </header>
    <main id="main-content">
        @if (session('status'))
            <div class="wrap"><div class="flash">{{ session('status') }}</div></div>
        @endif
        @yield('content')
    </main>
    <footer class="boutique-footer">
        <div class="wrap footer-inner">
            <div><a class="brandmark" href="{{ route('home') }}">{{ $store['StoreName'] }}</a><p>Everyday pieces. Your own point of view.</p></div>
            <nav aria-label="Footer navigation"><a href="{{ route('shop') }}">Explore the collection</a><a href="{{ route('home') }}#visit">Contact the boutique</a>
                @if (auth('staff')->check())<a href="{{ route('admin.dashboard') }}">Back office</a>@else<a href="{{ route('login') }}">Staff login</a>@endif
            </nav>
            <p class="copyright">&copy; {{ date('Y') }} {{ $store['StoreName'] }}</p>
        </div>
    </footer>
</body>
</html>
