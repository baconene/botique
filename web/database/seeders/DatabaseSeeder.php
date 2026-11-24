<?php

namespace Database\Seeders;

use Illuminate\Database\Seeder;

class DatabaseSeeder extends Seeder
{
    /**
     * Seed the application's database (`php artisan db:seed`).
     * Staff accounts are not seeded: create the first admin with `php artisan pos:admin`.
     */
    public function run(): void
    {
        $this->call(BoutiqueProductSeeder::class);
    }
}
