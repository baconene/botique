<?php

namespace App\Models\Pos;

use DateTimeInterface;
use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\Relations\HasMany;

/**
 * Row in the POS-owned `sales` table. Receipt code shown to customers is "R" + 6-digit number.
 */
class Sale extends Model
{
    protected $table = 'sales';

    protected $primaryKey = 'number';

    public $incrementing = false;

    public $timestamps = false;

    protected $guarded = [];

    protected $appends = ['code'];

    protected function casts(): array
    {
        return [
            'number' => 'integer',
            'sale_date' => 'datetime',
            'subtotal' => 'decimal:2',
            'discount_percent' => 'decimal:2',
            'discount_amount' => 'decimal:2',
            'tax_rate' => 'decimal:3',
            'tax' => 'decimal:2',
            'total' => 'decimal:2',
            'amount_tendered' => 'decimal:2',
            'change_due' => 'decimal:2',
            'refunded' => 'boolean',
            'refund_date' => 'datetime',
        ];
    }

    public function lines(): HasMany
    {
        return $this->hasMany(SaleLine::class, 'sale_number', 'number')->orderBy('line_no');
    }

    public function getCodeAttribute(): string
    {
        return 'R'.str_pad((string) $this->number, 6, '0', STR_PAD_LEFT);
    }

    /** Accepts "1001" or "R001001". */
    public static function numberFromCode(string $code): ?int
    {
        $code = strtoupper(trim($code));
        if (str_starts_with($code, 'R')) {
            $code = substr($code, 1);
        }

        return ctype_digit($code) ? (int) $code : null;
    }

    protected function serializeDate(DateTimeInterface $date): string
    {
        return $date->format('Y-m-d\TH:i:sP');
    }
}
