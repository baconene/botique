<?php

namespace App\Models\Pos;

use App\Services\Pos\Permissions;
use DateTimeInterface;
use Illuminate\Auth\Authenticatable;
use Illuminate\Contracts\Auth\Authenticatable as AuthenticatableContract;
use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\Relations\BelongsTo;
use Laravel\Sanctum\HasApiTokens;

/**
 * A POS user (cashier, manager, admin...) in the POS-owned `staff` table. Logs in with
 * POST /api/v1/auth/login and gets a session token that expires automatically.
 */
class Staff extends Model implements AuthenticatableContract
{
    use Authenticatable, HasApiTokens;

    protected $table = 'staff';

    const CREATED_AT = 'created';

    const UPDATED_AT = 'updated';

    protected $fillable = ['username', 'display_name', 'role_id', 'active'];

    protected $hidden = ['password_hash'];

    protected function casts(): array
    {
        return [
            'role_id' => 'integer',
            'active' => 'boolean',
            'last_login' => 'datetime',
            'created' => 'datetime',
            'updated' => 'datetime',
        ];
    }

    /** Web sign-in checks passwords with PasswordHasher, never Laravel's bcrypt hasher. */
    public function getAuthPassword()
    {
        return $this->password_hash;
    }

    /** The staff table has no remember-me column. */
    public function getRememberTokenName()
    {
        return '';
    }

    public function role(): BelongsTo
    {
        return $this->belongsTo(StaffRole::class, 'role_id');
    }

    /** @return list<string> */
    public function permissions(): array
    {
        return $this->role ? $this->role->permissionList() : [];
    }

    public function hasPermission(string $permission): bool
    {
        return in_array($permission, $this->permissions(), true);
    }

    public function isAdmin(): bool
    {
        return $this->role_id === Permissions::ADMIN_ROLE_ID;
    }

    /** Name printed on receipts. */
    public function cashierName(): string
    {
        return substr($this->display_name !== '' ? $this->display_name : $this->username, 0, 100);
    }

    /** "<device> / <username>", recorded on sales and stock movements. */
    public function terminalLabel(): string
    {
        $device = $this->currentAccessToken()?->name ?? 'Web POS';

        return substr($device.' / '.$this->username, 0, 64);
    }

    public function toApi(): array
    {
        return [
            'id' => $this->id,
            'username' => $this->username,
            'display_name' => $this->display_name,
            'role_id' => $this->role_id,
            'role_name' => $this->role?->name ?? '',
            'active' => $this->active,
            'last_login' => $this->last_login?->format('Y-m-d\TH:i:sP'),
            'permissions' => $this->permissions(),
        ];
    }

    protected function serializeDate(DateTimeInterface $date): string
    {
        return $date->format('Y-m-d\TH:i:sP');
    }
}
