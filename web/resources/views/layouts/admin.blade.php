@php($me = auth('staff')->user())
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8">
    <meta name="viewport" content="width=device-width, initial-scale=1">
    <meta name="csrf-token" content="{{ csrf_token() }}">
    <title>@yield('title', 'Back office') – {{ app(\App\Services\Pos\PosSettings::class)->get('StoreName') }}</title>
    <link href="https://fonts.googleapis.com/css2?family=Cormorant+Garamond:wght@500;600;700&family=Inter:wght@400;500;600&display=swap" rel="stylesheet">
    <link rel="stylesheet" href="{{ asset('css/app.css') }}?v=1">
</head>
<body>
<div class="admin">
    <aside class="sidebar">
        <a class="logo" href="{{ route('admin.dashboard') }}">{{ app(\App\Services\Pos\PosSettings::class)->get('StoreName') }}</a>
        <a class="nav-item {{ request()->routeIs('admin.dashboard') ? 'active' : '' }}" href="{{ route('admin.dashboard') }}">◆ Dashboard</a>
        @if ($me->hasPermission('sell'))
            <a class="nav-item {{ request()->routeIs('admin.pos*') ? 'active' : '' }}" href="{{ route('admin.pos') }}">▣ Point of sale</a>
        @endif
        @if ($me->hasPermission('inventory.view') || $me->hasPermission('inventory.edit') || $me->hasPermission('stock.adjust'))
            <a class="nav-item {{ request()->routeIs('admin.inventory*') ? 'active' : '' }}" href="{{ route('admin.inventory.index') }}">▤ Inventory</a>
        @endif
        <a class="nav-item" href="{{ route('home') }}" target="_blank">↗ View storefront</a>
        <div class="who">
            <strong>{{ $me->cashierName() }}</strong>
            {{ $me->role?->name }}
            <form method="post" action="{{ route('logout') }}">@csrf<button type="submit">Sign out</button></form>
        </div>
    </aside>
    <main class="main">
        @if (session('status'))<div class="flash">{{ session('status') }}</div>@endif
        @if ($errors->any())<div class="flash bad">{{ $errors->first() }}</div>@endif
        @yield('content')
    </main>
</div>
@stack('scripts')
</body>
</html>
