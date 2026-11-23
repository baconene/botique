<?php

use App\Services\Pos\Permissions;
use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\DB;
use Illuminate\Support\Facades\Schema;

/**
 * POS schema version 2: staff accounts, roles (RBAC) and sales.cashier.
 * Owned by the Windows app (MySqlStore migration v2); mirrored here "create if missing" only.
 * Also removes the old pos_registers table (register tokens were replaced by staff logins).
 */
return new class extends Migration
{
    public function up(): void
    {
        if (! Schema::hasTable('staff_roles')) {
            Schema::create('staff_roles', function (Blueprint $t) {
                $t->increments('id');
                $t->string('name', 64)->unique('ux_roles_name');
                $t->text('permissions');
                $t->boolean('is_system')->default(false);
            });
        }
        DB::table('staff_roles')->insertOrIgnore(Permissions::defaultRoles());

        if (! Schema::hasTable('staff')) {
            Schema::create('staff', function (Blueprint $t) {
                $t->increments('id');
                $t->string('username', 64)->unique('ux_staff_username');
                $t->string('display_name', 100)->default('');
                $t->string('password_hash', 255);
                $t->integer('role_id')->index('ix_staff_role');
                $t->boolean('active')->default(true);
                $t->dateTime('last_login')->nullable();
                $t->dateTime('created');
                $t->dateTime('updated');
            });
        }

        if (! Schema::hasColumn('sales', 'cashier')) {
            Schema::table('sales', function (Blueprint $t) {
                $t->string('cashier', 100)->default('');
            });
        }

        if ((int) DB::table('schema_info')->max('version') < 2) {
            DB::table('schema_info')->insert(['version' => 2]);
        }

        Schema::dropIfExists('pos_registers');
    }

    public function down(): void
    {
        // POS-owned tables hold the shop's data and are never dropped here.
    }
};
