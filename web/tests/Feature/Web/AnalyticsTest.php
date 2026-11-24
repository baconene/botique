<?php

namespace Tests\Feature\Web;

use App\Models\Pos\Product;
use App\Models\Pos\Staff;
use App\Services\Pos\PasswordHasher;
use Illuminate\Foundation\Testing\RefreshDatabase;
use Illuminate\Support\Facades\DB;
use Tests\TestCase;

class AnalyticsTest extends TestCase
{
    use RefreshDatabase;

    private function staff(string $username, int $roleId): Staff
    {
        $s = new Staff(['username' => $username, 'display_name' => ucfirst($username), 'role_id' => $roleId, 'active' => true]);
        $s->password_hash = PasswordHasher::hash('secret123');
        $s->save();

        return $s;
    }

    private function product(string $name, string $size, float $price, float $cost, int $qty, string $category = 'Tops'): Product
    {
        $p = new Product(['barcode' => 'BTQ'.random_int(100000, 999999), 'name' => $name, 'brand' => 'Ellery & Co', 'category' => $category,
            'size' => $size, 'color' => 'Ivory', 'price' => $price, 'cost' => $cost, 'quantity' => $qty, 'reorder_level' => 2]);
        $p->id = Product::newId();
        $p->save();

        return $p;
    }

    /** @param  array<int, array{0: Product, 1: int}>  $lines */
    private function sale(int $number, string $when, array $lines, float $discount = 0, string $method = 'Cash', bool $refunded = false, string $terminal = 'Front register'): void
    {
        $subtotal = array_sum(array_map(fn ($l) => $l[0]->price * $l[1], $lines));
        DB::table('sales')->insert([
            'number' => $number, 'sale_date' => $when, 'subtotal' => $subtotal, 'discount_amount' => $discount,
            'tax' => 0, 'total' => $subtotal - $discount, 'payment_method' => $method, 'refunded' => $refunded,
            'refund_date' => $refunded ? $when : null, 'terminal' => $terminal, 'note' => '',
        ]);
        foreach ($lines as $i => [$p, $qty]) {
            DB::table('sale_lines')->insert(['sale_number' => $number, 'line_no' => $i + 1, 'product_id' => $p->id, 'barcode' => $p->barcode,
                'name' => $p->name, 'size' => $p->size, 'color' => $p->color, 'unit_price' => $p->price, 'quantity' => $qty]);
        }
    }

    public function test_cashier_cannot_open_analytics(): void
    {
        $this->actingAs($this->staff('maria', 3), 'staff');
        $this->get('/admin/analytics')->assertForbidden();
        $this->get('/admin/analytics/data')->assertForbidden();
    }

    public function test_dashboard_page_loads_for_admin(): void
    {
        $this->actingAs($this->staff('owner', 1), 'staff');
        $this->get('/admin/analytics')->assertOk()->assertSee('analytics-root', false)->assertSee('Analytics');
    }

    public function test_figures_come_from_real_sales_and_samples_are_flagged(): void
    {
        $tee = $this->product('Silk Cami Top', 'M', 1000, 400, 1);
        $teeS = $this->product('Silk Cami Top', 'S', 1000, 400, 8);
        $jeans = $this->product('Wide Leg Jeans', '28', 2000, 900, 10, 'Bottoms');
        // Wednesday 2026-11-18, 12:15 and 20:40; one refunded sale; one sale on the previous day.
        $this->sale(1001, '2026-11-18 12:15:00', [[$tee, 2], [$jeans, 1]], 400, 'Card');
        $this->sale(1002, '2026-11-18 20:40:00', [[$teeS, 1]]);
        $this->sale(1003, '2026-11-18 15:00:00', [[$jeans, 1]], 0, 'Cash', true);
        $this->sale(1004, '2026-11-17 10:00:00', [[$teeS, 1]]);
        $this->sale(1005, '2026-11-18 13:00:00', [[$teeS, 3]], 0, 'Cash', false, 'Web POS / owner');

        $this->actingAs($this->staff('owner', 1), 'staff');
        $d = $this->getJson('/admin/analytics/data?from=2026-11-18&to=2026-11-18')->assertOk()->json();

        // Gross = all receipts' subtotals (4000 + 1000 + 2000 + 3000); refund 2000 and discount 400 come off.
        $this->assertEquals(10000, $d['kpis']['gross']);
        $this->assertEquals(2000, $d['kpis']['cancellations']);
        $this->assertEquals(400, $d['kpis']['discounts']);
        $this->assertEquals(7600, $d['kpis']['net']);
        $this->assertSame(3, $d['kpis']['orders']);
        $this->assertEquals(1000, $d['kpis']['prev']['net']);             // 2026-11-17

        $this->assertSame(1, $d['heatmap']['counts'][2][12]);             // Wednesday 12:00
        $this->assertSame(1, $d['heatmap']['counts'][2][20]);
        $this->assertSame(0, $d['heatmap']['counts'][2][15]);             // refunded sale not counted

        $cami = collect($d['products'])->firstWhere('name', 'Silk Cami Top');
        $this->assertSame(6, $cami['units']);
        $this->assertSame(['S' => 4, 'M' => 2], $cami['sizes']);
        $jeansRow = collect($d['products'])->firstWhere('name', 'Wide Leg Jeans');
        $this->assertEquals(50.0, $jeansRow['returnRate']);               // 1 sold, 1 returned

        $cogs = collect($d['waterfall']['steps'])->firstWhere('label', 'Cost of goods');
        $this->assertEquals(-(6 * 400 + 900), $cogs['amount']);
        $this->assertSame('real', $cogs['kind']);
        $this->assertSame('demo', collect($d['waterfall']['steps'])->firstWhere('label', 'Marketing')['kind']);

        $this->assertContains('Silk Cami Top', collect($d['lowStock'])->pluck('name')->all());   // M has 1 left
        $this->assertTrue($d['ads']['demo']);
        $this->assertTrue($d['devices']['demo']);
        $this->assertTrue($d['rto']['demo']);
        $this->assertTrue($d['cohorts']['demo']);
        $this->assertSame('Lunch', collect($d['timing']['dayparts'])->sortByDesc('orders')->first()['name']);

        // Channel filter: Web POS only.
        $web = $this->getJson('/admin/analytics/data?from=2026-11-18&to=2026-11-18&channel=web')->json();
        $this->assertSame(1, $web['kpis']['orders']);
        $this->assertEquals(3000, $web['kpis']['net']);

        // Sample data is repeatable for the same dates.
        $again = $this->getJson('/admin/analytics/data?from=2026-11-18&to=2026-11-18')->json();
        $this->assertSame($d['ads']['spend'], $again['ads']['spend']);
    }

    public function test_rejects_reversed_dates(): void
    {
        $this->actingAs($this->staff('owner', 1), 'staff');
        $this->getJson('/admin/analytics/data?from=2026-11-18&to=2026-11-01')->assertStatus(422);
    }
}
