<?php

namespace App\Models\Pos;

use Illuminate\Database\Eloquent\Model;
use Laravel\Sanctum\HasApiTokens;

/**
 * A POS register (one Windows PC running Boutique POS). It authenticates with a Sanctum token
 * created by `php artisan pos:register "Front counter"`.
 */
class Register extends Model
{
    use HasApiTokens;

    protected $table = 'pos_registers';

    protected $fillable = ['name'];

    protected function casts(): array
    {
        return [
            'last_seen_at' => 'datetime',
            'revoked_at' => 'datetime',
        ];
    }

    public function isRevoked(): bool
    {
        return $this->revoked_at !== null;
    }
}
