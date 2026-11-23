<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\DB;
use Illuminate\Support\Facades\Schema;

/**
 * POS tables are OWNED by the Windows app (src/Storage.cs, MySqlStore migrations).
 * This migration only creates them when they are missing (fresh server, local dev, tests),
 * mirroring POS schema version 1 exactly. It never alters or drops them.
 *
 * The pos_* tables at the bottom are owned by this Laravel app.
 */
return new class extends Migration
{
    public function up(): void
    {
        $createdPosSchema = false;

        if (! Schema::hasTable('products')) {
            $createdPosSchema = true;
            Schema::create('products', function (Blueprint $t) {
                $t->char('id', 32)->primary();
                $t->string('barcode', 64)->unique('ux_products_barcode');
                $t->string('name', 200)->index('ix_products_name');
                $t->string('category', 100)->default('');
                $t->string('brand', 100)->default('');
                $t->string('size', 40)->default('');
                $t->string('color', 60)->default('');
                $t->decimal('price', 12, 2)->default(0);
                $t->decimal('cost', 12, 2)->default(0);
                $t->integer('quantity')->default(0);
                $t->integer('reorder_level')->default(2);
                $t->text('notes')->nullable();
                $t->dateTime('created');
                $t->dateTime('updated');
            });
        }

        if (! Schema::hasTable('sales')) {
            Schema::create('sales', function (Blueprint $t) {
                $t->integer('number')->primary();
                $t->dateTime('sale_date')->index('ix_sales_date');
                $t->decimal('subtotal', 12, 2);
                $t->decimal('discount_percent', 6, 2)->default(0);
                $t->decimal('discount_amount', 12, 2)->default(0);
                $t->decimal('tax_rate', 7, 3)->default(0);
                $t->decimal('tax', 12, 2)->default(0);
                $t->decimal('total', 12, 2);
                $t->string('payment_method', 20);
                $t->decimal('amount_tendered', 12, 2)->default(0);
                $t->decimal('change_due', 12, 2)->default(0);
                $t->boolean('refunded')->default(false);
                $t->dateTime('refund_date')->nullable();
                $t->string('note', 500)->default('');
                $t->string('terminal', 64)->default('');
            });
        }

        if (! Schema::hasTable('sale_lines')) {
            Schema::create('sale_lines', function (Blueprint $t) {
                $t->id();
                $t->integer('sale_number')->index('ix_lines_sale');
                $t->integer('line_no');
                $t->char('product_id', 32)->default('')->index('ix_lines_product');
                $t->string('barcode', 64)->default('');
                $t->string('name', 200)->default('');
                $t->string('size', 40)->default('');
                $t->string('color', 60)->default('');
                $t->decimal('unit_price', 12, 2);
                $t->integer('quantity');
                $t->foreign('sale_number', 'fk_lines_sale')->references('number')->on('sales')->cascadeOnDelete();
            });
        }

        if (! Schema::hasTable('stock_movements')) {
            Schema::create('stock_movements', function (Blueprint $t) {
                $t->id();
                $t->char('product_id', 32)->index('ix_moves_product');
                $t->integer('change_qty');
                $t->string('reason', 40);
                $t->string('reference', 40)->default('');
                $t->dateTime('created')->index('ix_moves_created');
                $t->string('terminal', 64)->default('');
            });
        }

        if (! Schema::hasTable('counters')) {
            Schema::create('counters', function (Blueprint $t) {
                $t->string('name', 32)->primary();
                $t->integer('value');
            });
        }
        DB::table('counters')->insertOrIgnore([
            ['name' => 'item', 'value' => 0],
            ['name' => 'sale', 'value' => 1000],
        ]);

        if (! Schema::hasTable('settings')) {
            Schema::create('settings', function (Blueprint $t) {
                $t->string('name', 64)->primary();
                $t->text('value')->nullable();
            });
        }

        if (! Schema::hasTable('schema_info')) {
            Schema::create('schema_info', function (Blueprint $t) {
                $t->integer('version');
            });
        }
        if ($createdPosSchema && ! DB::table('schema_info')->exists()) {
            DB::table('schema_info')->insert(['version' => 1]);
        }

        // ---------- Laravel-owned tables ----------

        Schema::create('pos_registers', function (Blueprint $t) {
            $t->id();
            $t->string('name', 64);
            $t->timestamp('last_seen_at')->nullable();
            $t->timestamp('revoked_at')->nullable();
            $t->timestamps();
        });

        // Makes POST /sales safe to retry: the same client_ref always returns the same sale.
        Schema::create('pos_sale_requests', function (Blueprint $t) {
            $t->string('client_ref', 64)->primary();
            $t->integer('sale_number');
            $t->foreignId('register_id')->nullable();
            $t->timestamp('created_at')->nullable();
        });

        // Lets registers that sync with ?updated_since learn about deleted items.
        Schema::create('pos_deleted_products', function (Blueprint $t) {
            $t->id();
            $t->char('product_id', 32)->index();
            $t->string('barcode', 64);
            $t->dateTime('deleted_at')->index();
        });
    }

    public function down(): void
    {
        // Only Laravel-owned tables. POS tables hold the shop's data and are never dropped here.
        Schema::dropIfExists('pos_deleted_products');
        Schema::dropIfExists('pos_sale_requests');
        Schema::dropIfExists('pos_registers');
    }
};
