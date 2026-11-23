<?php

namespace App\Services\Pos;

/**
 * Staff password hashes, shared with the Windows app (src/Security.cs PasswordHasher) so the same
 * accounts work on direct-MySQL registers and through this API:
 *   pbkdf2-sha256$<iterations>$<base64 salt>$<base64 32-byte hash>
 */
final class PasswordHasher
{
    public const ITERATIONS = 100000;

    public static function hash(string $password): string
    {
        $salt = random_bytes(16);
        $hash = hash_pbkdf2('sha256', $password, $salt, self::ITERATIONS, 32, true);

        return 'pbkdf2-sha256$'.self::ITERATIONS.'$'.base64_encode($salt).'$'.base64_encode($hash);
    }

    public static function verify(string $password, ?string $stored): bool
    {
        $parts = explode('$', (string) $stored);
        if (count($parts) !== 4 || $parts[0] !== 'pbkdf2-sha256' || ! ctype_digit($parts[1])) {
            // Still spend the time so a missing account can't be detected by timing.
            hash_pbkdf2('sha256', $password, 'timing-equalizer', self::ITERATIONS, 32, true);

            return false;
        }
        $salt = base64_decode($parts[2], true);
        $expected = base64_decode($parts[3], true);
        if ($salt === false || $expected === false) {
            return false;
        }
        $actual = hash_pbkdf2('sha256', $password, $salt, (int) $parts[1], strlen($expected), true);

        return hash_equals($expected, $actual);
    }
}
