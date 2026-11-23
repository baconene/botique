<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8">
    <meta name="viewport" content="width=device-width, initial-scale=1">
    <title>Staff sign in – {{ $store['StoreName'] }}</title>
    <link href="https://fonts.googleapis.com/css2?family=Cormorant+Garamond:wght@500;600;700&family=Inter:wght@400;500;600&display=swap" rel="stylesheet">
    <link rel="stylesheet" href="{{ asset('css/app.css') }}?v=1">
</head>
<body>
<div class="auth-page">
    <div class="auth-side">
        <a class="logo" href="{{ route('home') }}" style="color:#fff">{{ $store['StoreName'] }}</a>
        <div>
            <span class="eyebrow" style="color:#e7c9d9">Back office</span>
            <h1>Point of sale &amp; online inventory</h1>
            <p style="color:#c9bfb5; max-width: 30em">Sign in with the same staff account you use on the in-store register.
                What you can open depends on your role: cashiers sell, stock clerks manage inventory, managers and admins do both.</p>
        </div>
        <a href="{{ route('home') }}" style="color:#c9bfb5">&larr; Back to the store</a>
    </div>
    <div class="auth-form">
        <form class="auth-card" method="post" action="{{ route('login.attempt') }}">
            @csrf
            <h2>Staff sign in</h2>
            <p class="muted">Use your POS username and password.</p>
            @if (session('status'))<div class="flash">{{ session('status') }}</div>@endif
            <div class="field">
                <label for="username">Username</label>
                <input id="username" name="username" value="{{ old('username') }}" autocomplete="username" autofocus required>
                @error('username')<div class="error">{{ $message }}</div>@enderror
            </div>
            <div class="field">
                <label for="password">Password</label>
                <input id="password" name="password" type="password" autocomplete="current-password" required>
                @error('password')<div class="error">{{ $message }}</div>@enderror
            </div>
            <button class="btn accent block" type="submit">Sign in</button>
            <p class="muted" style="margin-top: 1.2em; font-size: .85rem">No account yet? An Admin creates staff accounts in the POS
                (Users &amp; roles). The very first Admin is created on the server with <code>php artisan pos:admin</code>.</p>
        </form>
    </div>
</div>
</body>
</html>
