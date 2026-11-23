<?php

use App\Http\Controllers\Api\V1\ProductController;
use App\Http\Controllers\Api\V1\SaleController;
use App\Http\Controllers\Api\V1\StoreController;
use Illuminate\Support\Facades\Route;

/*
| Boutique POS API v1 - used by the Windows POS registers.
| Auth: "Authorization: Bearer <register token>" (create with `php artisan pos:register "Name"`).
| Reference: docs/API.md
*/

Route::prefix('v1')->group(function () {
    Route::get('ping', [StoreController::class, 'ping']);

    Route::middleware(['auth:sanctum', 'pos.register', 'throttle:pos'])->group(function () {
        Route::get('me', [StoreController::class, 'me']);

        Route::get('products', [ProductController::class, 'index']);
        Route::post('products', [ProductController::class, 'store']);
        Route::get('products/barcode/{barcode}', [ProductController::class, 'byBarcode'])->where('barcode', '.+');
        Route::get('products/{product}', [ProductController::class, 'show']);
        Route::put('products/{product}', [ProductController::class, 'update']);
        Route::delete('products/{product}', [ProductController::class, 'destroy']);
        Route::post('products/{product}/adjust', [ProductController::class, 'adjust']);
        Route::post('barcodes/next', [StoreController::class, 'nextBarcode']);

        Route::get('sales', [SaleController::class, 'index']);
        Route::post('sales', [SaleController::class, 'store']);
        Route::get('sales/{code}', [SaleController::class, 'show']);
        Route::post('sales/{code}/refund', [SaleController::class, 'refund']);

        Route::get('settings', [StoreController::class, 'settings']);
        Route::put('settings', [StoreController::class, 'updateSettings']);

        Route::get('reports/summary', [StoreController::class, 'summary']);
        Route::get('stats', [StoreController::class, 'stats']);
    });
});
