<?php

namespace App\Console\Commands;

use App\Models\Pos\Staff;
use Illuminate\Console\Command;

class PosStaffList extends Command
{
    protected $signature = 'pos:staff';

    protected $description = 'List POS staff accounts, their roles and active sessions';

    public function handle(): int
    {
        $this->table(['ID', 'Username', 'Name', 'Role', 'Status', 'Last sign-in', 'Sessions'],
            Staff::with('role')->orderBy('username')->get()->map(fn (Staff $s) => [
                $s->id, $s->username, $s->display_name, $s->role?->name ?? '?', $s->active ? 'active' : 'disabled',
                $s->last_login?->toDateTimeString() ?? 'never', $s->tokens()->where(fn ($q) => $q->whereNull('expires_at')->orWhere('expires_at', '>', now()))->count(),
            ]));

        return self::SUCCESS;
    }
}
