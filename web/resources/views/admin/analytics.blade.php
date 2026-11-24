@extends('layouts.admin')
@section('title', 'Analytics')
@section('content')
<link rel="stylesheet" href="{{ asset('css/analytics.css') }}?v=3">
<div class="page-head">
    <div>
        <span class="eyebrow">The business edit</span>
        <h1>Analytics</h1>
        <p class="muted">Daily operations, monthly profitability, annual strategy and sales timing, in one place.</p>
    </div>
</div>

<div id="analytics-root"
     data-endpoint="{{ route('admin.analytics.data') }}"
     data-usd-rate="{{ $usdRate }}"
     data-channels='@json($channels)'
     data-today="{{ today()->toDateString() }}">
    <div class="panel an-loading" role="status">Loading your dashboard…</div>
    <noscript><div class="panel">The analytics dashboard needs JavaScript. Sales and inventory reports are available under Reports.</div></noscript>
</div>

<script src="https://cdn.jsdelivr.net/npm/react@18.3.1/umd/react.production.min.js" crossorigin defer></script>
<script src="https://cdn.jsdelivr.net/npm/react-dom@18.3.1/umd/react-dom.production.min.js" crossorigin defer></script>
<script src="https://cdn.jsdelivr.net/npm/prop-types@15.8.1/prop-types.min.js" crossorigin defer></script>
<script src="https://cdn.jsdelivr.net/npm/recharts@2.12.7/umd/Recharts.min.js" crossorigin defer></script>
<script src="https://cdn.jsdelivr.net/npm/htm@3.1.1/dist/htm.umd.js" crossorigin defer></script>
<script src="https://cdn.jsdelivr.net/npm/lucide@0.460.0/dist/umd/lucide.min.js" crossorigin defer></script>
<script src="{{ asset('js/analytics.js') }}?v=3" defer></script>
@endsection
