<?php

namespace App\Console\Commands;

use App\Models\Pos\Register;
use Illuminate\Console\Command;

class PosRegisterCreate extends Command
{
    protected $signature = 'pos:register {name : e.g. "Front counter"}';

    protected $description = 'Create a POS register and print its API token (shown once)';

    public function handle(): int
    {
        $register = Register::create(['name' => $this->argument('name')]);
        $token = $register->createToken('pos')->plainTextToken;
        $this->info("Register #{$register->id} \"{$register->name}\" created.");
        $this->line('API token (enter it in the POS app; it will not be shown again):');
        $this->line($token);

        return self::SUCCESS;
    }
}
