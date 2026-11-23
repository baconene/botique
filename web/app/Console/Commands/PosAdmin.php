<?php

namespace App\Console\Commands;

use App\Models\Pos\Staff;
use App\Services\Pos\PasswordHasher;
use App\Services\Pos\Permissions;
use Illuminate\Console\Command;

/**
 * Creates the first Admin on a server (or resets an Admin's password if you're locked out).
 * Online, admins can't be created through the API until one exists.
 */
class PosAdmin extends Command
{
    protected $signature = 'pos:admin {username} {--name= : display name} {--password= : omit to be asked (recommended)}';

    protected $description = 'Create a POS Admin account, or reset an existing account to Admin with a new password';

    public function handle(): int
    {
        $username = strtolower(trim($this->argument('username')));
        if (! preg_match('/^[a-z0-9._-]{3,64}$/', $username)) {
            $this->error('Username must be 3-64 characters: letters, digits, dot, dash or underscore.');

            return self::FAILURE;
        }
        $password = $this->option('password') ?? $this->secret('Password (min 6 characters)');
        if (strlen((string) $password) < 6) {
            $this->error('Password must be at least 6 characters.');

            return self::FAILURE;
        }

        $staff = Staff::firstOrNew(['username' => $username]);
        $isNew = ! $staff->exists;
        $staff->display_name = $this->option('name') ?? ($staff->display_name ?: ucfirst($username));
        $staff->role_id = Permissions::ADMIN_ROLE_ID;
        $staff->active = true;
        $staff->password_hash = PasswordHasher::hash($password);
        $staff->save();
        if (! $isNew) {
            $staff->tokens()->delete();
        }
        $this->info(($isNew ? 'Created' : 'Reset').' Admin "'.$username.'". Sign in from the POS app with this username and password.');

        return self::SUCCESS;
    }
}
