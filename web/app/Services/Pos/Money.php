<?php

namespace App\Services\Pos;

use InvalidArgumentException;

/**
 * Exact money math in integer cents. Rounding is half away from zero, the same as
 * SaleMath.R() in the Windows app (MidpointRounding.AwayFromZero), so receipts match to the cent.
 */
final class Money
{
    /** Parses "89", "89.5", "89.50", 89.5 into an integer scaled by 10^$decimals. */
    public static function scaled(string|int|float $value, int $decimals = 2): int
    {
        if (is_int($value)) {
            $s = (string) $value;
        } elseif (is_float($value)) {
            $s = rtrim(rtrim(sprintf('%.6F', $value), '0'), '.');
        } else {
            $s = trim($value);
        }
        if (! preg_match('/^(-?)(\d+)(?:\.(\d+))?$/', $s, $m)) {
            throw new InvalidArgumentException("Not a valid amount: {$s}");
        }
        $frac = $m[3] ?? '';
        if (strlen($frac) > $decimals) {
            throw new InvalidArgumentException("Too many decimal places: {$s}");
        }
        $n = (int) $m[2] * (10 ** $decimals) + (int) str_pad($frac, $decimals, '0');

        return $m[1] === '-' ? -$n : $n;
    }

    public static function toCents(string|int|float $value): int
    {
        return self::scaled($value, 2);
    }

    public static function format(int $cents): string
    {
        $sign = $cents < 0 ? '-' : '';
        $abs = abs($cents);

        return sprintf('%s%d.%02d', $sign, intdiv($abs, 100), $abs % 100);
    }

    /** round($a * $b / $den), half away from zero, using integers only. */
    public static function mulDivRound(int $a, int $b, int $den): int
    {
        $num = $a * $b;
        $q = intdiv(abs($num) * 2 + $den, 2 * $den);

        return $num < 0 ? -$q : $q;
    }

    /**
     * Same formula as SaleMath.Compute in the Windows app.
     *
     * @param  array<int, array{unit_cents:int, quantity:int}>  $lines
     * @return array{subtotal:int, discount:int, tax:int, total:int}  cents
     */
    public static function saleTotals(array $lines, string|int|float $discountPercent, string|int|float $taxRatePercent): array
    {
        $subtotal = 0;
        foreach ($lines as $l) {
            $subtotal += $l['unit_cents'] * $l['quantity'];
        }
        $discount = self::mulDivRound($subtotal, self::scaled($discountPercent, 2), 10000);      // percent with 2 dp
        $taxable = $subtotal - $discount;
        $tax = self::mulDivRound($taxable, self::scaled($taxRatePercent, 3), 100000);           // percent with 3 dp

        return ['subtotal' => $subtotal, 'discount' => $discount, 'tax' => $tax, 'total' => $taxable + $tax];
    }
}
