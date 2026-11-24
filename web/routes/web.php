<?php

use App\Http\Controllers\Web\AdminController;
use App\Http\Controllers\Web\InventoryController;
use App\Http\Controllers\Web\PosController;
use App\Http\Controllers\Web\ReportController;
use App\Http\Controllers\Web\SettingsController;
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
    Route::get('reports/sales', [ReportController::class, 'sales'])->middleware('pos.can:sales.view')->name('reports.sales');
    Route::get('reports/inventory', [ReportController::class, 'inventory'])->middleware('pos.can:inventory.view,inventory.edit,stock.adjust')->name('reports.inventory');
    Route::get('settings', [SettingsController::class, 'index'])->middleware('pos.can:settings.manage,users.manage')->name('settings');
    Route::middleware('pos.can:settings.manage')->group(function () {
        Route::put('settings/store', [SettingsController::class, 'store'])->name('settings.store');
        Route::put('settings/references', [SettingsController::class, 'references'])->name('settings.references');
    });
    Route::middleware('pos.can:users.manage')->group(function () {
        Route::post('settings/roles', [SettingsController::class, 'role'])->name('settings.roles.create');
        Route::put('settings/roles/{role}', [SettingsController::class, 'role'])->name('settings.roles.update');
        Route::delete('settings/roles/{role}', [SettingsController::class, 'deleteRole'])->name('settings.roles.delete');
        Route::post('settings/staff', [SettingsController::class, 'user'])->name('settings.staff.create');
        Route::put('settings/staff/{staff}', [SettingsController::class, 'user'])->name('settings.staff.update');
    });

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
