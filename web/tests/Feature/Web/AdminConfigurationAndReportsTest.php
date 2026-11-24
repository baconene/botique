<?php

namespace Tests\Feature\Web;

use App\Models\Pos\Product;
use App\Models\Pos\Sale;
use App\Models\Pos\Staff;
use App\Models\Pos\StaffRole;
use App\Services\Pos\PasswordHasher;
use App\Services\Pos\ReferenceData;
use Illuminate\Foundation\Testing\RefreshDatabase;
use Tests\TestCase;

class AdminConfigurationAndReportsTest extends TestCase
{
    use RefreshDatabase;

    private function staff(int $role = 1): Staff
    {
        $staff = new Staff(['username' => 'staff'.random_int(1000, 9999), 'display_name' => 'Boutique owner', 'role_id' => $role, 'active' => true]);
        $staff->password_hash = PasswordHasher::hash('secret123');
        $staff->save();

        return $staff;
    }

    private function product(array $attributes = []): Product
    {
        $product = new Product(array_merge(['name' => 'Sage Dress', 'barcode' => 'BTQ'.random_int(1000, 9999), 'category' => 'Dresses', 'price' => '100.00', 'cost' => '40.00', 'quantity' => 3, 'reorder_level' => 2], $attributes));
        $product->id = Product::newId();
        $product->save();

        return $product;
    }

    private function sale(int $number, string $date, string $total, bool $refunded = false): Sale
    {
        return Sale::create(['number' => $number, 'sale_date' => $date, 'subtotal' => $total, 'discount_percent' => 0, 'discount_amount' => 0, 'tax_rate' => 0, 'tax' => 0, 'total' => $total, 'payment_method' => 'Card', 'refunded' => $refunded]);
    }

    public function test_admin_can_configure_store_and_reference_lists_without_changing_products(): void
    {
        $product = $this->product();
        $this->actingAs($this->staff(), 'staff');
        $this->get('/admin/settings')->assertOk()->assertSee('Category types')->assertSee('User types & permissions', false);
        $this->put('/admin/settings/store', ['StoreName' => 'The Edit', 'TaxRatePercent' => '8.5'])->assertRedirect();
        $this->assertDatabaseHas('settings', ['name' => 'StoreName', 'value' => 'The Edit']);
        $this->put('/admin/settings/references', ['field' => 'category', 'values' => "Occasionwear\nOccasionwear\nResort"])->assertRedirect();
        $this->assertSame(['Occasionwear', 'Resort'], app(ReferenceData::class)->configured('category'));
        $this->assertContains('Dresses', app(ReferenceData::class)->options('category'));
        $this->assertSame('Dresses', $product->fresh()->category);
        $this->get('/admin/inventory/create')->assertOk()->assertSee('Occasionwear');
        $this->put('/admin/settings/references', ['field' => 'size', 'values' => "0\n2\n4"])->assertRedirect();
        $this->assertContains('0', app(ReferenceData::class)->options('size'));
        $this->put('/admin/settings/store', ['TaxRatePercent' => '99'])->assertSessionHasErrors('TaxRatePercent');
        $this->put('/admin/settings/references', ['field' => 'password_hash', 'values' => 'x'])->assertSessionHasErrors('field');
    }

    public function test_user_types_and_accounts_reuse_access_safeguards(): void
    {
        $admin = $this->staff();
        $this->actingAs($admin, 'staff');
        $this->post('/admin/settings/roles', ['name' => 'Stylist', 'permissions' => ['inventory.view']])->assertRedirect();
        $role = StaffRole::where('name', 'Stylist')->firstOrFail();
        $this->put('/admin/settings/roles/'.$role->id, ['name' => 'Senior stylist', 'permissions' => ['inventory.view', 'sales.view']])->assertRedirect();
        $this->assertSame('inventory.view,sales.view', implode(',', array_intersect(['inventory.view', 'sales.view'], $role->fresh()->permissionList())));
        $this->post('/admin/settings/staff', ['username' => 'stylist', 'display_name' => 'Anna', 'password' => 'secret123', 'role_id' => $role->id, 'active' => 1])->assertRedirect();
        $stylist = Staff::where('username', 'stylist')->firstOrFail();
        $this->assertSame($role->id, $stylist->role_id);
        $this->delete('/admin/settings/roles/'.$role->id)->assertSessionHasErrors('role');
        $this->put('/admin/settings/roles/1', ['name' => 'Changed', 'permissions' => []])->assertSessionHasErrors('role');
        $this->put('/admin/settings/staff/'.$admin->id, ['role_id' => 3])->assertSessionHasErrors('staff');
        $this->put('/admin/settings/staff/'.$admin->id, ['active' => 0])->assertSessionHasErrors('staff');
        $this->assertTrue($admin->fresh()->active);
        $this->put('/admin/settings/roles/'.$role->id, ['name' => 'Stylist', 'permissions' => ['invented']])->assertSessionHasErrors('permissions.0');
    }

    public function test_reports_and_configuration_enforce_role_permissions(): void
    {
        $this->actingAs($this->staff(3), 'staff');
        foreach (['/admin/reports/sales', '/admin/reports/inventory', '/admin/settings'] as $path) {
            $this->get($path)->assertForbidden();
        }
        $this->put('/admin/settings/store', ['StoreName' => 'Not allowed'])->assertForbidden();
        $this->post('/admin/settings/roles', ['name' => 'Owner', 'permissions' => ['users.manage']])->assertForbidden();
        $this->actingAs($this->staff(4), 'staff');
        $this->get('/admin/reports/inventory')->assertOk();
        $this->get('/admin/reports/sales')->assertForbidden();
        $this->actingAs($this->staff(2), 'staff');
        $this->get('/admin/settings')->assertOk()->assertDontSee('Add a user type')->assertDontSee('Add a staff account');
        $this->post('/admin/settings/roles', ['name' => 'Owner', 'permissions' => []])->assertForbidden();
    }

    public function test_sales_report_has_inclusive_dates_and_excludes_refunded_sales(): void
    {
        $this->actingAs($this->staff(), 'staff');
        $this->sale(1001, '2026-11-01 00:00:00', '100.10');
        $this->sale(1002, '2026-11-02 23:59:59', '50.20');
        $this->sale(1003, '2026-11-02 12:00:00', '75.00', true);
        $this->sale(1004, '2026-11-03 00:00:00', '999.00');
        $response = $this->get('/admin/reports/sales?from=2026-11-01&to=2026-11-02')->assertOk();
        $response->assertViewHas('totals', fn ($totals) => (int) $totals->orders === 2 && round((float) $totals->total, 2) === 150.30);
        $response->assertViewHas('refunds', fn ($total) => (float) $total === 75.0);
        $response->assertSee('R001001')->assertSee('R001002')->assertDontSee('R001004');
        $this->get('/admin/reports/sales?from=2026-11-02&to=2026-11-01')->assertSessionHasErrors('to');
        $this->get('/admin/reports/sales?from=2020-01-01&to=2026-11-02')->assertSessionHasErrors('to');
        $this->get('/admin/reports/sales?from=bad')->assertSessionHasErrors('from');
    }

    public function test_inventory_report_filters_and_values_are_consistent(): void
    {
        $this->actingAs($this->staff(), 'staff');
        $this->product(['quantity' => 2]);
        $this->product(['name' => 'Sold out', 'quantity' => 0]);
        $this->product(['name' => 'Other category', 'category' => 'Tops', 'quantity' => 10]);
        $this->get('/admin/reports/inventory?category=Dresses&stock=low')->assertOk()
            ->assertViewHas('totals', fn ($totals) => (int) $totals->items === 2 && (int) $totals->units === 2 && (float) $totals->cost === 80.0 && (float) $totals->retail === 200.0)
            ->assertSee('Sage Dress')->assertDontSee('Other category');
        $this->get('/admin/reports/inventory?stock=out')->assertOk()->assertSee('Sold out')->assertDontSee('Sage Dress');
        $this->get('/admin/reports/inventory?stock=invalid')->assertSessionHasErrors('stock');
    }

    public function test_empty_reports_and_dashboard_render_without_fake_data(): void
    {
        $this->actingAs($this->staff(), 'staff');
        $this->get('/admin')->assertOk()->assertSee('Your boutique, at a glance.');
        $this->get('/admin/reports/sales')->assertOk()->assertSee('No completed sales in this period');
        $this->get('/admin/reports/inventory')->assertOk()->assertSee('No inventory matches these filters');
    }
}
