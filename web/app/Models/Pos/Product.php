<?php

namespace App\Models\Pos;

use DateTimeInterface;
use Illuminate\Database\Eloquent\Model;
use Illuminate\Support\Str;

/**
 * Row in the POS-owned `products` table (schema defined by the Windows app's MySqlStore).
 */
class Product extends Model
{
    protected $table = 'products';

    protected $keyType = 'string';

    public $incrementing = false;

    const CREATED_AT = 'created';

    const UPDATED_AT = 'updated';

    protected $fillable = [
        'id', 'barcode', 'name', 'category', 'brand', 'size', 'color',
        'price', 'cost', 'quantity', 'reorder_level', 'notes',
    ];

    protected $attributes = [
        'category' => '', 'brand' => '', 'size' => '', 'color' => '',
        'price' => 0, 'cost' => 0, 'quantity' => 0, 'reorder_level' => 2, 'notes' => '',
    ];

    protected function casts(): array
    {
        return [
            'price' => 'decimal:2',
            'cost' => 'decimal:2',
            'quantity' => 'integer',
            'reorder_level' => 'integer',
            'created' => 'datetime',
            'updated' => 'datetime',
        ];
    }

    /** Same id format as the Windows app: Guid.ToString("N"). */
    public static function newId(): string
    {
        return str_replace('-', '', (string) Str::uuid());
    }

    protected function serializeDate(DateTimeInterface $date): string
    {
        return $date->format('Y-m-d\TH:i:sP');
    }
}
