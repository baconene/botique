<?php

use App\Http\Controllers\Api\V1\AuthController;
use App\Http\Controllers\Api\V1\ProductController;
use App\Http\Controllers\Api\V1\SaleController;
use App\Http\Controllers\Api\V1\StaffController;
use App\Http\Controllers\Api\V1\StoreController;
use Illuminate\Support\Facades\Route;

/*
| Boutique POS API v1 - used by the Windows POS registers.
| Auth: POST /auth/login with a staff username + password returns a session token;
| send it as "Authorization: Bearer <token>". Every route below checks the role's permissions (RBAC).
| Reference: docs/API.md
*/

Route::prefix('v1')->group(function () {
    Route::get('ping', [StoreController::class, 'ping']);
    Route::post('auth/login', [AuthController::class, 'login'])->middleware('throttle:pos-login');

    Route::middleware(['auth:sanctum', 'pos.staff', 'throttle:pos'])->group(function () {
        Route::get('auth/me', [AuthController::class, 'me']);
        Route::post('auth/logout', [AuthController::class, 'logout']);
        Route::get('settings', [StoreController::class, 'settings']);

        Route::middleware('pos.can:sell,inventory.view,labels.print')->group(function () {
            Route::get('products', [ProductController::class, 'index']);
            Route::get('products/barcode/{barcode}', [ProductController::class, 'byBarcode'])->where('barcode', '.+');
            Route::get('products/{product}', [ProductController::class, 'show']);
        });
        Route::middleware('pos.can:inventory.edit')->group(function () {
            Route::post('products', [ProductController::class, 'store']);
            Route::put('products/{product}', [ProductController::class, 'update']);
            Route::delete('products/{product}', [ProductController::class, 'destroy']);
            Route::post('barcodes/next', [StoreController::class, 'nextBarcode']);
        });
        Route::post('products/{product}/adjust', [ProductController::class, 'adjust'])->middleware('pos.can:stock.adjust');

        Route::post('sales', [SaleController::class, 'store'])->middleware('pos.can:sell');
        Route::middleware('pos.can:sales.view,refund')->group(function () {
            Route::get('sales', [SaleController::class, 'index']);
            Route::get('sales/{code}', [SaleController::class, 'show']);
        });
        Route::post('sales/{code}/refund', [SaleController::class, 'refund'])->middleware('pos.can:refund');

        Route::put('settings', [StoreController::class, 'updateSettings'])->middleware('pos.can:settings.manage');
        Route::middleware('pos.can:sales.view')->group(function () {
            Route::get('reports/summary', [StoreController::class, 'summary']);
            Route::get('stats', [StoreController::class, 'stats']);
        });

        Route::middleware('pos.can:users.manage')->group(function () {
            Route::get('permissions', [StaffController::class, 'permissions']);
            Route::get('roles', [StaffController::class, 'roles']);
            Route::post('roles', [StaffController::class, 'storeRole']);
            Route::put('roles/{role}', [StaffController::class, 'updateRole']);
            Route::delete('roles/{role}', [StaffController::class, 'destroyRole']);
            Route::get('staff', [StaffController::class, 'index']);
            Route::post('staff', [StaffController::class, 'store']);
            Route::put('staff/{staff}', [StaffController::class, 'update']);
            Route::delete('staff/{staff}', [StaffController::class, 'destroy']);
        });
    });
});
