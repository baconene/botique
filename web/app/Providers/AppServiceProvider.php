<?php

namespace App\Providers;

use Illuminate\Cache\RateLimiting\Limit;
use Illuminate\Http\Request;
use App\Services\Pos\Catalog;
use Illuminate\Support\Facades\RateLimiter;
use Illuminate\Support\Facades\View;
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
        // Registers scan quickly; allow 600 requests/minute per signed-in staff member.
        RateLimiter::for('pos', fn (Request $request) => Limit::perMinute(600)->by($request->user()?->getKey() ?: $request->ip()));
        // Storefront header: category menu from live stock.
        View::composer('layouts.store', fn ($view) => $view->with('navCategories', app(Catalog::class)->categories()));

        // Slow down password guessing: 10 sign-in attempts per minute per username + IP.
        RateLimiter::for('pos-login', fn (Request $request) => Limit::perMinute(10)->by(strtolower((string) $request->input('username')).'|'.$request->ip()));
    }
}
