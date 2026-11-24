<?php

use App\Http\Controllers\Web\AdminController;
use App\Http\Controllers\Web\InventoryController;
use App\Http\Controllers\Web\PosController;
use App\Http\Controllers\Web\StaffAuthController;
use App\Http\Controllers\Web\StorefrontController;
use Illuminate\Support\Facades\Route;

// ---------------- Public demo storefront (live stock from the POS) ----------------
Route::get('/', [StorefrontController::class, 'home'])->name('home');
Route::get('/shop', [StorefrontController::class, 'shop'])->name('shop');
Route::get('/shop/item/{product}', [StorefrontController::class, 'item'])->name('shop.item');
Route::get('/media/products/{file}', [InventoryController::class, 'media'])->where('file', '[A-Za-z0-9._-]+')->name('media.product');

// ---------------- Staff sign-in (same accounts and roles as the Windows POS) ----------------
Route::get('/login', [StaffAuthController::class, 'show'])->name('login');
Route::post('/login', [StaffAuthController::class, 'login'])->middleware('throttle:pos-login')->name('login.attempt');
Route::post('/logout', [StaffAuthController::class, 'logout'])->name('logout');

// ---------------- Back office ----------------
Route::prefix('admin')->name('admin.')->middleware(['auth:staff', 'staff.session'])->group(function () {
    Route::get('/', [AdminController::class, 'dashboard'])->name('dashboard');

    Route::middleware('pos.can:sell')->group(function () {
        Route::get('pos', [PosController::class, 'index'])->name('pos');
        Route::get('pos/search', [PosController::class, 'search'])->name('pos.search');
        Route::post('pos/sale', [PosController::class, 'sale'])->name('pos.sale');
    });
    Route::get('pos/receipt/{code}', [PosController::class, 'receipt'])->middleware('pos.can:sell,sales.view,refund')->name('pos.receipt');

    Route::get('inventory', [InventoryController::class, 'index'])->middleware('pos.can:inventory.view,inventory.edit,stock.adjust')->name('inventory.index');
    Route::middleware('pos.can:inventory.edit')->group(function () {
        Route::get('inventory/create', [InventoryController::class, 'create'])->name('inventory.create');
        Route::post('inventory', [InventoryController::class, 'store'])->name('inventory.store');
        Route::get('inventory/{product}/edit', [InventoryController::class, 'edit'])->name('inventory.edit');
        Route::put('inventory/{product}', [InventoryController::class, 'update'])->name('inventory.update');
        Route::delete('inventory/{product}', [InventoryController::class, 'destroy'])->name('inventory.destroy');
    });
    Route::post('inventory/{product}/adjust', [InventoryController::class, 'adjust'])->middleware('pos.can:stock.adjust')->name('inventory.adjust');
});
