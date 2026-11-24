<?php

namespace Database\Seeders;

use App\Models\Pos\Product;
use App\Services\Pos\Inventory;
use Illuminate\Database\Seeder;
use Illuminate\Support\Facades\DB;

/**
 * Sample boutique collection for demos: 8 categories with 10 styles each, every style in one or more
 * colours and a full size run. Each colour/size is its own POS item with a barcode and stock on hand.
 *
 * Safe to run more than once: an item whose name, brand, colour and size already exist is skipped.
 *
 *   php artisan db:seed --class=BoutiqueProductSeeder
 *   php artisan pos:demo-data          (same items, but only into an empty catalogue)
 */
class BoutiqueProductSeeder extends Seeder
{
    public const NOTE = 'Demo item';

    private const LETTER = ['XS', 'S', 'M', 'L', 'XL'];

    private const DENIM = ['25', '26', '27', '28', '29', '30', '31'];

    private const SHOES = ['36', '37', '38', '39', '40'];

    private const ONE = ['One Size'];

    /**
     * category => [sizes, styles]; each style is [name, brand, colours, price, cost] in pesos.
     * A style may override the category's sizes with a sixth element.
     */
    public const CATALOGUE = [
        'Dresses' => [self::LETTER, [
            ['Linen Wrap Dress', 'Maison Lune', ['Sage', 'Ivory'], 2490, 980],
            ['Satin Slip Midi Dress', 'Ellery & Co', ['Champagne', 'Black'], 3290, 1350],
            ['Tiered Cotton Maxi Dress', 'Sol Studio', ['Rust', 'Navy'], 2890, 1150],
            ['Smocked Floral Sundress', 'Sol Studio', ['Blue Floral'], 1990, 760],
            ['Ribbed Bodycon Dress', 'North Loom', ['Mocha', 'Black'], 1690, 640],
            ['Puff Sleeve Mini Dress', 'Maison Lune', ['Blush'], 2190, 850],
            ['Shirt Dress with Belt', 'Atelier Nord', ['Khaki', 'White'], 2690, 1050],
            ['Pleated Halter Dress', 'Ellery & Co', ['Emerald'], 3490, 1420],
            ['Eyelet Babydoll Dress', 'Maison Lune', ['White'], 2290, 890],
            ['Knit Polo Dress', 'North Loom', ['Cream', 'Navy'], 2390, 920],
        ]],
        'Tops' => [self::LETTER, [
            ['Silk Cami Top', 'Ellery & Co', ['Ivory', 'Black'], 1490, 540],
            ['Puff Sleeve Blouse', 'Maison Lune', ['Blush', 'White'], 1790, 660],
            ['Ribbed Knit Tank', 'North Loom', ['Cream', 'Black', 'Olive'], 790, 260],
            ['Linen Button-Up Shirt', 'Atelier Nord', ['White', 'Sky Blue'], 1890, 720],
            ['Square Neck Crop Top', 'Sol Studio', ['Red', 'Black'], 890, 300],
            ['Oversized Cotton Tee', 'North Loom', ['White', 'Heather Grey'], 690, 220],
            ['Ruffle Trim Blouse', 'Maison Lune', ['Lilac'], 1690, 620],
            ['Off-Shoulder Top', 'Sol Studio', ['White', 'Terracotta'], 1190, 420],
            ['Satin Button Shirt', 'Ellery & Co', ['Champagne', 'Navy'], 1990, 760],
            ['Lace Trim Camisole', 'Ellery & Co', ['Black'], 1290, 460],
        ]],
        'Bottoms' => [self::LETTER, [
            ['High-Rise Wide Leg Jeans', 'Denim Row', ['Indigo', 'Light Wash'], 2690, 1080, self::DENIM],
            ['Straight Leg Mom Jeans', 'Denim Row', ['Vintage Blue'], 2390, 950, self::DENIM],
            ['Pleated Midi Skirt', 'Sol Studio', ['Olive', 'Black'], 1890, 700],
            ['Tailored Linen Trousers', 'Maison Lune', ['Natural', 'Black'], 2290, 880],
            ['Satin Bias Midi Skirt', 'Ellery & Co', ['Champagne', 'Emerald'], 1990, 760],
            ['Denim Mini Skirt', 'Denim Row', ['Mid Wash'], 1490, 560, self::DENIM],
            ['Paperbag Waist Shorts', 'Sol Studio', ['Khaki', 'White'], 1290, 470],
            ['Wide Leg Culottes', 'Atelier Nord', ['Black', 'Taupe'], 1790, 660],
            ['Knit Lounge Pants', 'North Loom', ['Oatmeal', 'Charcoal'], 1590, 580],
            ['Cargo Parachute Pants', 'Atelier Nord', ['Sand'], 1990, 740],
        ]],
        'Knitwear' => [['S', 'M', 'L'], [
            ['Cashmere Crew Sweater', 'North Loom', ['Camel', 'Grey'], 4890, 2150],
            ['Chunky Cardigan', 'North Loom', ['Oatmeal'], 3290, 1290],
            ['Cropped Cable Knit Sweater', 'North Loom', ['Cream', 'Sage'], 2490, 960],
            ['V-Neck Merino Pullover', 'Atelier Nord', ['Navy', 'Burgundy'], 2990, 1180],
            ['Ribbed Turtleneck', 'North Loom', ['Black', 'Ivory'], 1890, 700],
            ['Knit Sweater Vest', 'Maison Lune', ['Argyle Brown'], 1590, 580],
            ['Mohair Blend Cardigan', 'Ellery & Co', ['Lilac', 'Butter'], 3490, 1380],
            ['Fine Knit Polo Sweater', 'Atelier Nord', ['Cream'], 2290, 880],
            ['Off-Shoulder Knit Top', 'Sol Studio', ['Rust'], 1790, 660],
            ['Knit Co-ord Set', 'North Loom', ['Mocha', 'Grey'], 3990, 1580],
        ]],
        'Outerwear' => [['S', 'M', 'L'], [
            ['Cropped Trench Coat', 'Atelier Nord', ['Tan'], 4490, 1850],
            ['Classic Denim Jacket', 'Denim Row', ['Mid Wash', 'Black'], 2890, 1150],
            ['Oversized Blazer', 'Atelier Nord', ['Black', 'Beige'], 3690, 1480],
            ['Linen Shacket', 'Maison Lune', ['Natural'], 2490, 960],
            ['Faux Leather Moto Jacket', 'Ellery & Co', ['Black', 'Chocolate'], 3990, 1600],
            ['Quilted Liner Jacket', 'North Loom', ['Olive'], 3290, 1290],
            ['Cropped Tweed Jacket', 'Ellery & Co', ['Pink Tweed'], 4290, 1750],
            ['Lightweight Utility Jacket', 'Atelier Nord', ['Khaki'], 2790, 1090],
            ['Wool Blend Long Coat', 'Atelier Nord', ['Camel', 'Charcoal'], 5990, 2600],
            ['Hooded Windbreaker', 'Sol Studio', ['Cream', 'Navy'], 2190, 820],
        ]],
        'Shoes' => [self::SHOES, [
            ['Strappy Block Heel Sandals', 'Paseo', ['Nude', 'Black'], 2490, 980],
            ['Leather Ballet Flats', 'Paseo', ['Black', 'Cream'], 1990, 760],
            ['Platform Sneakers', 'Paseo', ['White'], 2790, 1090],
            ['Slingback Kitten Heels', 'Paseo', ['Red', 'Black'], 2690, 1050],
            ['Woven Slide Sandals', 'Sol Studio', ['Tan'], 1490, 540],
            ['Chunky Loafers', 'Atelier Nord', ['Black', 'Burgundy'], 3190, 1260],
            ['Ankle Chelsea Boots', 'Atelier Nord', ['Black', 'Taupe'], 3890, 1560],
            ['Espadrille Wedges', 'Sol Studio', ['Natural'], 2290, 880],
            ['Pointed Toe Mules', 'Paseo', ['Silver', 'Black'], 2190, 840],
            ['Canvas Low-Top Sneakers', 'North Loom', ['White', 'Black'], 1690, 620],
        ]],
        'Bags' => [self::ONE, [
            ['Woven Straw Tote', 'Sol Studio', ['Natural'], 1290, 450],
            ['Mini Shoulder Bag', 'Ellery & Co', ['Black', 'Cream', 'Butter'], 1890, 700],
            ['Quilted Chain Crossbody', 'Ellery & Co', ['Black', 'Blush'], 2490, 960],
            ['Canvas Market Tote', 'North Loom', ['Natural Stripe'], 890, 300],
            ['Leather Bucket Bag', 'Atelier Nord', ['Cognac', 'Black'], 3490, 1380],
            ['Pleated Pouch Clutch', 'Maison Lune', ['Champagne'], 1590, 580],
            ['Rattan Round Bag', 'Sol Studio', ['Natural'], 1690, 620],
            ['Nylon Belt Bag', 'North Loom', ['Black', 'Olive'], 990, 340],
            ['Structured Top Handle Bag', 'Ellery & Co', ['White', 'Brown'], 2990, 1180],
            ['Mini Backpack', 'Atelier Nord', ['Black', 'Taupe'], 2190, 840],
        ]],
        'Accessories' => [self::ONE, [
            ['Silk Hair Scarf', 'Ellery & Co', ['Terracotta', 'Navy Print'], 690, 220],
            ['Pearl Hair Clip Set', 'Maison Lune', ['Pearl'], 490, 150],
            ['Gold Hoop Earrings', 'Ellery & Co', ['Gold'], 890, 300],
            ['Layered Chain Necklace', 'Ellery & Co', ['Gold', 'Silver'], 1190, 410],
            ['Wide Brim Straw Hat', 'Sol Studio', ['Natural'], 1290, 460],
            ['Woven Leather Belt', 'Atelier Nord', ['Tan', 'Black'], 990, 340],
            ['Oversized Cat-Eye Sunglasses', 'Sol Studio', ['Tortoise', 'Black'], 1090, 360],
            ['Claw Clip Duo', 'North Loom', ['Tortoise'], 390, 110],
            ['Beaded Charm Bracelet', 'Maison Lune', ['Multi'], 590, 190],
            ['Knit Beanie', 'North Loom', ['Cream', 'Charcoal'], 690, 230],
        ]],
    ];

    public function run(): void
    {
        $added = $this->seedCatalogue(app(Inventory::class));
        if ($this->command) {
            $this->command->info("Added {$added} demo items (".self::styleCount().' styles in '.count(self::CATALOGUE).' categories).');
        }
    }

    /** Adds every missing colour/size of the catalogue and returns how many items were created. */
    public function seedCatalogue(Inventory $inventory): int
    {
        $added = 0;
        DB::transaction(function () use ($inventory, &$added) {
            foreach (self::CATALOGUE as $category => [$sizes, $styles]) {
                foreach ($styles as $style) {
                    [$name, $brand, $colors, $price, $cost] = $style;
                    foreach ($colors as $color) {
                        foreach ($style[5] ?? $sizes as $size) {
                            $exists = Product::where('name', $name)->where('brand', $brand)
                                ->where('color', $color)->where('size', $size)->exists();
                            if ($exists) {
                                continue;
                            }
                            $qty = self::stockFor($name, $color, $size);
                            $p = new Product([
                                'name' => $name, 'brand' => $brand, 'category' => $category, 'color' => $color, 'size' => $size,
                                'price' => $price, 'cost' => $cost, 'quantity' => $qty, 'reorder_level' => 2, 'notes' => self::NOTE,
                            ]);
                            $p->id = Product::newId();
                            $p->barcode = $inventory->nextBarcode();
                            $p->save();
                            $inventory->log($p->id, $qty, 'new item', 'demo', 'demo-data');
                            $added++;
                        }
                    }
                }
            }
        });

        return $added;
    }

    public static function styleCount(): int
    {
        return array_sum(array_map(fn ($c) => count($c[1]), self::CATALOGUE));
    }

    /**
     * Repeatable, realistic-looking stock: middle sizes carry more, and roughly one item in eight
     * is at or below its reorder level so the low-stock views have something to show.
     */
    private static function stockFor(string $name, string $color, string $size): int
    {
        $n = crc32($name.'|'.$color.'|'.$size);
        if ($n % 8 === 0) {
            return $n % 3;                              // 0-2: low or sold out
        }
        $base = in_array($size, ['S', 'M', '27', '28', '38', 'One Size'], true) ? 5 : 3;

        return $base + $n % 5;                          // 3-9
    }
}
