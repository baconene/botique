<?php

namespace App\Console\Commands;

use App\Models\Pos\Register;
use Illuminate\Console\Command;

class PosRegisterList extends Command
{
    protected $signature = 'pos:registers';

    protected $description = 'List POS registers and when they were last seen';

    public function handle(): int
    {
        $this->table(['ID', 'Name', 'Last seen', 'Status'], Register::orderBy('id')->get()->map(fn (Register $r) => [
            $r->id, $r->name, $r->last_seen_at?->toDateTimeString() ?? 'never', $r->isRevoked() ? 'revoked' : 'active',
        ]));

        return self::SUCCESS;
    }
}
