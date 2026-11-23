<?php

namespace App\Console\Commands;

use App\Models\Pos\Register;
use Illuminate\Console\Command;

class PosRegisterRevoke extends Command
{
    protected $signature = 'pos:revoke {id : register ID from pos:registers}';

    protected $description = 'Revoke a POS register (e.g. a lost or replaced PC) and delete its tokens';

    public function handle(): int
    {
        $register = Register::findOrFail($this->argument('id'));
        $register->tokens()->delete();
        $register->forceFill(['revoked_at' => now()])->save();
        $this->info("Register #{$register->id} \"{$register->name}\" revoked.");

        return self::SUCCESS;
    }
}
