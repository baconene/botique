<?php

namespace App\Services\Pos;

/**
 * RBAC permission list. MUST match src/Security.cs (Permissions) in the Windows app:
 * same keys, same default roles, '*' = every permission (Admin).
 */
final class Permissions
{
    public const ALL = [
        'sell' => 'Sell (checkout)',
        'discount' => 'Discounts and price changes',
        'refund' => 'Refunds',
        'sales.view' => 'Sales history and reports',
        'inventory.view' => 'View inventory',
        'inventory.edit' => 'Add, edit and delete items',
        'stock.adjust' => 'Receive and adjust stock',
        'labels.print' => 'Print barcode labels',
        'settings.manage' => 'Store settings',
        'users.manage' => 'Users and roles',
    ];

    public const ADMIN_ROLE_ID = 1;

    public static function defaultRoles(): array
    {
        return [
            ['id' => 1, 'name' => 'Admin', 'permissions' => '*', 'is_system' => true],
            ['id' => 2, 'name' => 'Manager', 'permissions' => 'sell,discount,refund,sales.view,inventory.view,inventory.edit,stock.adjust,labels.print,settings.manage', 'is_system' => false],
            ['id' => 3, 'name' => 'Cashier', 'permissions' => 'sell,labels.print', 'is_system' => false],
            ['id' => 4, 'name' => 'Stock clerk', 'permissions' => 'inventory.view,inventory.edit,stock.adjust,labels.print', 'is_system' => false],
        ];
    }

    /** @return list<string> */
    public static function parse(?string $csv): array
    {
        $csv = trim((string) $csv);
        if ($csv === '*') {
            return array_keys(self::ALL);
        }

        return array_values(array_intersect(array_keys(self::ALL), array_map('trim', explode(',', $csv))));
    }

    /** @param  list<string>  $permissions */
    public static function toCsv(array $permissions): string
    {
        return implode(',', array_values(array_intersect(array_keys(self::ALL), $permissions)));
    }
}
