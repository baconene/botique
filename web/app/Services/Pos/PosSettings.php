<?php

namespace App\Services\Pos;

use Illuminate\Support\Facades\DB;

/**
 * Settings shared by every register (the `settings` table). Keys and defaults match
 * MySqlStore.SharedSettings / StoreSettings in the Windows app. Device settings
 * (scanner, printers, label stock) stay on each PC and are not part of the API.
 */
class PosSettings
{
    public const DEFAULTS = [
        'StoreName' => 'My Boutique',
        'Address' => '123 Main Street',
        'Phone' => '(555) 555-0100',
        'ReceiptFooter' => "Thank you for shopping with us!\nExchanges within 14 days with receipt.",
        'TaxRatePercent' => '7',
        'BarcodePrefix' => 'BTQ',
    ];

    /** @return array<string, string> */
    public function all(): array
    {
        $stored = DB::table('settings')->whereIn('name', array_keys(self::DEFAULTS))->pluck('value', 'name')->all();

        return array_merge(self::DEFAULTS, array_map(fn ($v) => (string) $v, $stored));
    }

    public function get(string $key): string
    {
        return $this->all()[$key];
    }

    /** @param  array<string, string>  $values */
    public function update(array $values): array
    {
        $rows = [];
        foreach ($values as $name => $value) {
            if (array_key_exists($name, self::DEFAULTS)) {
                $rows[] = ['name' => $name, 'value' => (string) $value];
            }
        }
        if ($rows) {
            DB::table('settings')->upsert($rows, ['name'], ['value']);
        }

        return $this->all();
    }
}
