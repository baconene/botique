<?php

namespace App\Services\Pos;

use App\Models\Pos\Product;
use Illuminate\Support\Facades\DB;

class ReferenceData
{
    public const FIELDS = ['category' => 100, 'brand' => 100, 'size' => 40, 'color' => 60];

    public const DEFAULTS = [
        'category' => ['Dresses', 'Tops', 'Bottoms', 'Outerwear', 'Knitwear', 'Accessories', 'Shoes', 'Jewelry'],
        'brand' => [],
        'size' => ['XXS', 'XS', 'S', 'M', 'L', 'XL', 'XXL', 'One Size'],
        'color' => ['Black', 'White', 'Ivory', 'Sage', 'Navy'],
    ];

    public function configured(string $field): array
    {
        $stored = DB::table('settings')->where('name', 'Catalogue.'.$field)->value('value');

        return $stored === null ? self::DEFAULTS[$field] : (json_decode($stored, true) ?? []);
    }

    public function options(string $field): array
    {
        abort_unless(array_key_exists($field, self::FIELDS), 404);

        return collect($this->configured($field))->merge(Product::where($field, '!=', '')->distinct()->pluck($field))
            ->unique(fn ($value) => mb_strtolower($value))->sort()->values()->all();
    }

    public function save(string $field, array $values): void
    {
        DB::table('settings')->updateOrInsert(['name' => 'Catalogue.'.$field], ['value' => json_encode($values, JSON_THROW_ON_ERROR)]);
    }
}
