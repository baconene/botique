<?php

namespace Database\Seeders;

use App\Models\Pos\Product;
use App\Models\Pos\ProductImage;
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

    /**
     * One photo per style: free-to-use Unsplash photos (https://unsplash.com/license), loaded from
     * Unsplash's CDN and resized by it (see Look::img). Replace them with your own in Inventory → Edit.
     */
    public const PHOTOS = [
        // Dresses
        'Linen Wrap Dress' => '1764298493197-a1c1cce57800',
        'Satin Slip Midi Dress' => '1594019739447-6c8a1d744d61',
        'Tiered Cotton Maxi Dress' => '1496747611176-843222e1e57c',
        'Smocked Floral Sundress' => '1762154057377-cc9d3dd6900c',
        'Ribbed Bodycon Dress' => '1789110853239-56a3f1f2142e',
        'Puff Sleeve Mini Dress' => '1632262049811-86d23941618b',
        'Shirt Dress with Belt' => '1789111161022-1970ba92f3b9',
        'Pleated Halter Dress' => '1499939667766-4afceb292d05',
        'Eyelet Babydoll Dress' => '1789110854264-0512e2a4bcd7',
        'Knit Polo Dress' => '1789110520279-49e8a3a6fb91',
        // Tops
        'Silk Cami Top' => '1588186941286-724357304676',
        'Puff Sleeve Blouse' => '1704775990821-23af362863d9',
        'Ribbed Knit Tank' => '1789110855604-b693bd403e43',
        'Linen Button-Up Shirt' => '1713881587420-113c1c43e28a',
        'Square Neck Crop Top' => '1548389995-fbd3151a501f',
        'Oversized Cotton Tee' => '1622445275992-e7efb32d2257',
        'Ruffle Trim Blouse' => '1778826971124-c5ee892e266e',
        'Off-Shoulder Top' => '1790516099879-091fc87cdf6d',
        'Satin Button Shirt' => '1790539459929-4b40e8b3f694',
        'Lace Trim Camisole' => '1770294758971-44fa1eae61a3',
        // Bottoms
        'High-Rise Wide Leg Jeans' => '1598554747436-c9293d6a588f',
        'Straight Leg Mom Jeans' => '1584642425274-db2c9bf955a8',
        'Pleated Midi Skirt' => '1789110853398-539d78dd63bc',
        'Tailored Linen Trousers' => '1789110853687-71692f0e86be',
        'Satin Bias Midi Skirt' => '1791745338926-0c641704cb1d',
        'Denim Mini Skirt' => '1743356914615-66062f1850e6',
        'Paperbag Waist Shorts' => '1729719762110-6ad6e60f4dbd',
        'Wide Leg Culottes' => '1789110519554-bdca2e4549e5',
        'Knit Lounge Pants' => '1759229874786-6d0cc75d95be',
        'Cargo Parachute Pants' => '1789028152833-cb9c223235d2',
        // Knitwear
        'Cashmere Crew Sweater' => '1610901157620-340856d0a50f',
        'Chunky Cardigan' => '1610288311735-39b7facbd095',
        'Cropped Cable Knit Sweater' => '1536992266094-82847e1fd431',
        'V-Neck Merino Pullover' => '1765337210176-1bb643f4776b',
        'Ribbed Turtleneck' => '1774727485537-e5455eec8baa',
        'Knit Sweater Vest' => '1784816836059-86bff419a8f8',
        'Mohair Blend Cardigan' => '1773747310674-b42a55d72003',
        'Fine Knit Polo Sweater' => '1711355249709-1733df63e028',
        'Off-Shoulder Knit Top' => '1768794521925-aaa277cc3a78',
        'Knit Co-ord Set' => '1707279086909-4e4ec2df07b0',
        // Outerwear
        'Cropped Trench Coat' => '1676716105765-e19fe6a01851',
        'Classic Denim Jacket' => '1611312449408-fcece27cdbb7',
        'Oversized Blazer' => '1779398970492-2e5f14f9da93',
        'Linen Shacket' => '1740710748146-a15d840d6f40',
        'Faux Leather Moto Jacket' => '1602370463198-086436840055',
        'Quilted Liner Jacket' => '1776774646358-e7a8a9063375',
        'Cropped Tweed Jacket' => '1649937408746-4d2f603f91c8',
        'Lightweight Utility Jacket' => '1544022613-e87ca75a784a',
        'Wool Blend Long Coat' => '1618333453296-9e35280fd6b1',
        'Hooded Windbreaker' => '1571524632030-caf8f11fb9d3',
        // Shoes
        'Strappy Block Heel Sandals' => '1770757685000-c80e45122986',
        'Leather Ballet Flats' => '1674500762924-612a431c28dd',
        'Platform Sneakers' => '1553808373-92b0bcc3af65',
        'Slingback Kitten Heels' => '1596702874230-b5706dfb5bc7',
        'Woven Slide Sandals' => '1627388484741-74dcc56ec343',
        'Chunky Loafers' => '1654945419086-bcb1c1e1b875',
        'Ankle Chelsea Boots' => '1788478963160-1c38139e2932',
        'Espadrille Wedges' => '1565953198075-db265882ee68',
        'Pointed Toe Mules' => '1755151606192-1c4b4bb88390',
        'Canvas Low-Top Sneakers' => '1676379760823-ebf91f45de0b',
        // Bags
        'Woven Straw Tote' => '1641934777532-d277296b111c',
        'Mini Shoulder Bag' => '1604176424472-17cd740f74e9',
        'Quilted Chain Crossbody' => '1681747685985-a401c271156c',
        'Canvas Market Tote' => '1544816155-12df9643f363',
        'Leather Bucket Bag' => '1613482184972-f9c1022d0928',
        'Pleated Pouch Clutch' => '1761646238176-b480909e6ba6',
        'Rattan Round Bag' => '1684721688363-74cac722068a',
        'Nylon Belt Bag' => '1657603719375-8ffdacaac790',
        'Structured Top Handle Bag' => '1605733513597-a8f8341084e6',
        'Mini Backpack' => '1680039211156-66c721b87625',
        // Accessories
        'Silk Hair Scarf' => '1777795530497-205664bbd965',
        'Pearl Hair Clip Set' => '1767442989822-1f8efcfefc15',
        'Gold Hoop Earrings' => '1617038220319-276d3cfab638',
        'Layered Chain Necklace' => '1601821765780-754fa98637c1',
        'Wide Brim Straw Hat' => '1518061124653-4b85d51931f1',
        'Woven Leather Belt' => '1784832555222-230841ae7b34',
        'Oversized Cat-Eye Sunglasses' => '1760446031524-79586718421d',
        'Claw Clip Duo' => '1774301844178-c9ce96b76d92',
        'Beaded Charm Bracelet' => '1638768892257-8aec93a524e5',
        'Knit Beanie' => '1544967919-44c1ef2f9e7a',
    ];

    public static function photoUrl(string $style): ?string
    {
        return isset(self::PHOTOS[$style]) ? 'https://images.unsplash.com/photo-'.self::PHOTOS[$style] : null;
    }

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
                            $existing = Product::where('name', $name)->where('brand', $brand)
                                ->where('color', $color)->where('size', $size)->first();
                            if ($existing) {
                                $this->addPhoto($existing->id, $name);   // older demo items get their photo too
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
                            $this->addPhoto($p->id, $name);
                            $added++;
                        }
                    }
                }
            }
        });

        return $added;
    }

    /** Sets the style's photo unless the item already has one (a photo you uploaded is never replaced). */
    private function addPhoto(string $productId, string $style): void
    {
        $url = self::photoUrl($style);
        if ($url && ! ProductImage::where('product_id', $productId)->exists()) {
            ProductImage::create(['product_id' => $productId, 'path' => $url]);
        }
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
