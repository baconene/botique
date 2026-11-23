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
    <link rel="stylesheet" href="{{ asset('css/app.css') }}?v=1">
</head>
<body>
    <div class="announce">
        Free alterations on every dress · Visit us at {{ $store['Address'] ?: 'our boutique' }}
        @if ($store['Phone']) · {{ $store['Phone'] }} @endif
    </div>
    <header class="site-header">
        <div class="wrap">
            <a class="logo" href="{{ route('home') }}">{{ $store['StoreName'] }}</a>
            <nav class="nav">
                <a href="{{ route('shop') }}" class="{{ request()->routeIs('shop') && !request('category') ? 'active' : '' }}">Shop all</a>
                <a href="{{ route('shop', ['sort' => 'newest']) }}">New in</a>
                @foreach (($navCategories ?? collect())->take(4) as $cat)
                    <a href="{{ route('shop', ['category' => $cat['name']]) }}" class="cat-link {{ request('category') === $cat['name'] ? 'active' : '' }}">{{ $cat['name'] }}</a>
                @endforeach
            </nav>
            <div class="header-actions">
                <form action="{{ route('shop') }}" method="get">
                    <input class="search-mini" type="search" name="q" placeholder="Search dresses, linen, sage…" value="{{ request('q') }}">
                </form>
                @if (auth('staff')->check())
                    <a class="btn small" href="{{ route('admin.dashboard') }}">Back office</a>
                @else
                    <a class="btn small ghost" href="{{ route('login') }}">Staff login</a>
                @endif
            </div>
        </div>
    </header>

    @if (session('status'))
        <div class="wrap" style="margin-top: 16px"><div class="flash">{{ session('status') }}</div></div>
    @endif

    @yield('content')

    <footer class="site-footer">
        <div class="wrap">
            <span>&copy; {{ date('Y') }} {{ $store['StoreName'] }} · {{ $store['Address'] }}</span>
            <span>Stock shown live from our in-store POS · <a href="{{ route('login') }}">Staff</a></span>
        </div>
    </footer>
</body>
</html>
