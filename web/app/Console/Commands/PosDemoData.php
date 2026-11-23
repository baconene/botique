<?php

namespace App\Console\Commands;

use App\Models\Pos\Product;
use App\Services\Pos\Inventory;
use Illuminate\Console\Command;
use Illuminate\Support\Facades\DB;

/** Fills an empty catalogue with a sample boutique collection so the storefront demo has something to show. */
class PosDemoData extends Command
{
    protected $signature = 'pos:demo-data {--force : add the samples even if the catalogue already has items}';

    protected $description = 'Add a sample clothing collection (for demos) to an empty POS catalogue';

    /** name, brand, category, color, price, cost, [size => qty] */
    private const ITEMS = [
        ['Linen Wrap Dress', 'Maison Lune', 'Dresses', 'Sage', 89, 34, ['XS' => 2, 'S' => 4, 'M' => 6, 'L' => 3]],
        ['Linen Wrap Dress', 'Maison Lune', 'Dresses', 'Ivory', 89, 34, ['S' => 3, 'M' => 2]],
        ['Satin Slip Midi Dress', 'Ellery & Co', 'Dresses', 'Champagne', 128, 52, ['XS' => 1, 'S' => 2, 'M' => 2]],
        ['Tiered Cotton Maxi', 'Sol Studio', 'Dresses', 'Rust', 112, 44, ['S' => 3, 'M' => 4, 'L' => 2]],
        ['Silk Cami Top', 'Ellery & Co', 'Tops', 'Ivory', 54, 19.5, ['S' => 5, 'M' => 8, 'L' => 4]],
        ['Silk Cami Top', 'Ellery & Co', 'Tops', 'Black', 54, 19.5, ['S' => 4, 'M' => 6]],
        ['Puff Sleeve Blouse', 'Maison Lune', 'Tops', 'Blush', 68, 25, ['XS' => 2, 'S' => 3, 'M' => 3]],
        ['Ribbed Knit Tank', 'North Loom', 'Tops', 'Cream', 36, 12, ['S' => 6, 'M' => 6, 'L' => 5]],
        ['High-Rise Wide Leg Jeans', 'Denim Row', 'Bottoms', 'Indigo', 98, 41, ['26' => 2, '27' => 3, '28' => 5, '29' => 3, '30' => 2]],
        ['Pleated Midi Skirt', 'Sol Studio', 'Bottoms', 'Olive', 76, 28, ['S' => 3, 'M' => 3, 'L' => 1]],
        ['Tailored Linen Trousers', 'Maison Lune', 'Bottoms', 'Natural', 92, 36, ['S' => 2, 'M' => 4, 'L' => 3]],
        ['Cashmere Crew Sweater', 'North Loom', 'Knitwear', 'Camel', 148, 62, ['S' => 1, 'M' => 2, 'L' => 2]],
        ['Chunky Cardigan', 'North Loom', 'Knitwear', 'Oatmeal Beige', 118, 47, ['S' => 2, 'M' => 3]],
        ['Cropped Trench', 'Atelier Nord', 'Outerwear', 'Tan', 168, 70, ['S' => 2, 'M' => 2, 'L' => 1]],
        ['Woven Straw Tote', 'Sol Studio', 'Accessories', 'Natural', 45, 15, ['One Size' => 10]],
        ['Silk Hair Scarf', 'Ellery & Co', 'Accessories', 'Terracotta', 28, 9, ['One Size' => 12]],
    ];

    public function handle(Inventory $inventory): int
    {
        if (Product::count() > 0 && ! $this->option('force')) {
            $this->warn('The catalogue already has items, so nothing was added. Use --force to add the samples anyway.');

            return self::SUCCESS;
        }

        $added = 0;
        DB::transaction(function () use ($inventory, &$added) {
            foreach (self::ITEMS as [$name, $brand, $category, $color, $price, $cost, $sizes]) {
                foreach ($sizes as $size => $qty) {
                    $p = new Product([
                        'name' => $name, 'brand' => $brand, 'category' => $category, 'color' => $color, 'size' => (string) $size,
                        'price' => $price, 'cost' => $cost, 'quantity' => $qty, 'reorder_level' => 2, 'notes' => 'Demo item',
                    ]);
                    $p->id = Product::newId();
                    $p->barcode = $inventory->nextBarcode();
                    $p->save();
                    $inventory->log($p->id, $qty, 'new item', 'demo', 'demo-data');
                    $added++;
                }
            }
        });
        $styles = collect(self::ITEMS)->map(fn ($i) => $i[0].'|'.$i[1])->unique()->count();
        $this->info("Added {$added} demo items ({$styles} styles in several sizes and colors). They are marked \"Demo item\" in Notes.");

        return self::SUCCESS;
    }
}
