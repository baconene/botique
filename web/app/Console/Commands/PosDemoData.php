<?php

namespace App\Console\Commands;

use App\Models\Pos\Product;
use App\Services\Pos\Inventory;
use Database\Seeders\BoutiqueProductSeeder;
use Illuminate\Console\Command;

/** Fills an empty catalogue with the sample boutique collection so the storefront demo has something to show. */
class PosDemoData extends Command
{
    protected $signature = 'pos:demo-data {--force : add the samples even if the catalogue already has items}';

    protected $description = 'Add a sample clothing collection (for demos) to an empty POS catalogue';

    public function handle(Inventory $inventory): int
    {
        if (Product::count() > 0 && ! $this->option('force')) {
            $this->warn('The catalogue already has items, so nothing was added. Use --force to add the samples anyway.');

            return self::SUCCESS;
        }

        $added = (new BoutiqueProductSeeder)->seedCatalogue($inventory);
        $this->info("Added {$added} demo items (".BoutiqueProductSeeder::styleCount().' styles in '
            .count(BoutiqueProductSeeder::CATALOGUE).' categories, several sizes and colours). They are marked "'
            .BoutiqueProductSeeder::NOTE.'" in Notes.');

        return self::SUCCESS;
    }
}
