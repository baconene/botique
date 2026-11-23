<?php

namespace App\Models\Pos;

use App\Services\Pos\Permissions;
use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\Relations\HasMany;

/** Row in the POS-owned `staff_roles` table. permissions = CSV of permission keys, or '*' for all. */
class StaffRole extends Model
{
    protected $table = 'staff_roles';

    public $timestamps = false;

    protected $fillable = ['name', 'permissions'];

    protected function casts(): array
    {
        return ['is_system' => 'boolean'];
    }

    public function staff(): HasMany
    {
        return $this->hasMany(Staff::class, 'role_id');
    }

    /** @return list<string> */
    public function permissionList(): array
    {
        return Permissions::parse($this->permissions);
    }

    public function toApi(): array
    {
        return [
            'id' => $this->id,
            'name' => $this->name,
            'permissions' => $this->permissionList(),
            'is_system' => $this->is_system,
            'user_count' => $this->staff()->count(),
        ];
    }
}
