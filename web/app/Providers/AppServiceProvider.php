<?php

namespace App\Providers;

use Illuminate\Cache\RateLimiting\Limit;
use Illuminate\Http\Request;
use Illuminate\Support\Facades\RateLimiter;
use Illuminate\Support\ServiceProvider;

class AppServiceProvider extends ServiceProvider
{
    /**
     * Register any application services.
     */
    public function register(): void
    {
        //
    }

    /**
     * Bootstrap any application services.
     */
    public function boot(): void
    {
        // Registers scan quickly; allow 600 requests/minute per register token.
        RateLimiter::for('pos', fn (Request $request) => Limit::perMinute(600)->by($request->user()?->getKey() ?: $request->ip()));
    }
}
