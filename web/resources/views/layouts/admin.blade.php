@php($me = auth('staff')->user())
@php($canInventory = $me->hasPermission('inventory.view') || $me->hasPermission('inventory.edit') || $me->hasPermission('stock.adjust'))
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
<meta name="csrf-token" content="{{ csrf_token() }}">
<title>@yield('title', 'Back office') – {{ app(\App\Services\Pos\PosSettings::class)->get('StoreName') }}</title>
<link href="https://fonts.googleapis.com/css2?family=Cormorant+Garamond:wght@500;600;700&family=Inter:wght@400;500;600;700&display=swap" rel="stylesheet">
<link rel="stylesheet" href="{{ asset('css/app.css') }}?v=3">
<link rel="stylesheet" href="{{ asset('css/admin.css') }}?v=3">
<script src="{{ asset('js/admin.js') }}?v=1" defer></script>
</head>
<body class="admin-body">
<a class="skip-link" href="#admin-content">Skip to content</a>
<div class="admin">
<aside class="sidebar" aria-label="Boutique navigation">
<a class="logo" href="{{ route('admin.dashboard') }}">{{ app(\App\Services\Pos\PosSettings::class)->get('StoreName') }}<span>THE BUSINESS EDIT</span></a>
<button type="button" class="mobile-nav-toggle btn" aria-controls="admin-nav" aria-expanded="true" hidden><img src="{{ asset('icons/list.svg') }}" alt="">Menu</button>
<nav id="admin-nav" class="admin-nav">
<span class="nav-caption">WORKSPACE</span>
<a class="nav-item {{ request()->routeIs('admin.dashboard') ? 'active' : '' }}" @if(request()->routeIs('admin.dashboard')) aria-current="page" @endif href="{{ route('admin.dashboard') }}"><img src="{{ asset('icons/grid-1x2.svg') }}" alt="">Overview</a>
@if($me->hasPermission('sell'))<a class="nav-item {{ request()->routeIs('admin.pos*') ? 'active' : '' }}" href="{{ route('admin.pos') }}"><img src="{{ asset('icons/bag.svg') }}" alt="">Point of sale</a>@endif
@if($canInventory)<a class="nav-item {{ request()->routeIs('admin.inventory*') ? 'active' : '' }}" href="{{ route('admin.inventory.index') }}"><img src="{{ asset('icons/boxes.svg') }}" alt="">Inventory</a>@endif
@if($me->hasPermission('sales.view'))<a class="nav-item {{ request()->routeIs('admin.analytics*') ? 'active' : '' }}" @if(request()->routeIs('admin.analytics*')) aria-current="page" @endif href="{{ route('admin.analytics') }}"><img src="{{ asset('icons/graph-up.svg') }}" alt="">Analytics</a>@endif
@if($me->hasPermission('sales.view') || $canInventory)<a class="nav-item {{ request()->routeIs('admin.reports.*') ? 'active' : '' }}" href="{{ route($me->hasPermission('sales.view') ? 'admin.reports.sales' : 'admin.reports.inventory') }}"><img src="{{ asset('icons/bar-chart.svg') }}" alt="">Reports</a>@endif
<span class="nav-caption nav-secondary">YOUR BOUTIQUE</span>
@if($me->hasPermission('settings.manage') || $me->hasPermission('users.manage'))<a class="nav-item {{ request()->routeIs('admin.settings*') ? 'active' : '' }}" href="{{ route('admin.settings') }}"><img src="{{ asset('icons/sliders.svg') }}" alt="">Settings</a>@endif
<a class="nav-item" href="{{ route('home') }}" target="_blank" rel="noopener"><img src="{{ asset('icons/arrow-up-right.svg') }}" alt="">View storefront</a>
</nav>
<div class="sidebar-note"><span class="eyebrow">A little perspective</span><p>Beautiful pieces.<br>A thriving boutique.</p></div>
<div class="who"><div class="avatar" aria-hidden="true">{{ mb_substr($me->cashierName(),0,1) }}</div><div><strong>{{ $me->cashierName() }}</strong><span>{{ $me->role?->name }}</span></div><form method="post" action="{{ route('logout') }}">@csrf<button type="submit">Sign out</button></form></div>
</aside>
<main class="main" id="admin-content">
<header class="admin-topbar"><span>Workspace <span class="muted">/ @yield('title', 'Overview')</span></span>
@if($canInventory)<form class="admin-search" method="get" action="{{ route('admin.inventory.index') }}"><img src="{{ asset('icons/search.svg') }}" alt=""><input aria-label="Search inventory" name="q" placeholder="Find a piece, barcode, brand…" maxlength="200"><button type="submit">Search</button></form>@endif
</header>
@if(session('status'))<div class="flash" role="status">{{ session('status') }}</div>@endif
@if($errors->any())<div class="flash bad" role="alert"><strong>Please check your changes.</strong><ul>@foreach($errors->all() as $error)<li>{{ $error }}</li>@endforeach</ul></div>@endif
@yield('content')
<footer class="admin-footer">Made for the everyday business of beautiful things.</footer>
</main></div>
@stack('scripts')
</body></html>
