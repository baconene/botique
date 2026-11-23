<?php

namespace App\Models\Pos;

use Illuminate\Database\Eloquent\Model;

class SaleLine extends Model
{
    protected $table = 'sale_lines';

    public $timestamps = false;

    protected $guarded = [];

    protected $hidden = ['id', 'sale_number'];

    protected function casts(): array
    {
        return [
            'line_no' => 'integer',
            'unit_price' => 'decimal:2',
            'quantity' => 'integer',
        ];
    }
}
