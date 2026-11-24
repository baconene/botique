<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

/**
 * Product photos for the storefront and back office.
 * Owned by Laravel (not part of the POS schema), so the Windows app's `products` table is untouched.
 * `path` is either an uploaded file on the public disk (products/abc.jpg) or a full https:// URL.
 */
return new class extends Migration
{
    public function up(): void
    {
        Schema::create('product_images', function (Blueprint $t) {
            $t->string('product_id', 32)->primary();
            $t->string('path', 500);
            $t->timestamps();
        });
    }

    public function down(): void
    {
        Schema::dropIfExists('product_images');
    }
};
