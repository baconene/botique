<?php

namespace Tests\Feature\Pos;

use App\Models\Pos\Product;
use App\Models\Pos\Staff;
use App\Services\Pos\PasswordHasher;
use App\Models\User;
use App\Services\Pos\Money;
use Illuminate\Foundation\Testing\RefreshDatabase;
use Illuminate\Support\Facades\DB;
use Illuminate\Support\Str;
use Tests\TestCase;

class PosApiTest extends TestCase
{
    use RefreshDatabase;

    private Staff $admin;

    private array $auth;

    protected function setUp(): void
    {
        parent::setUp();
        $this->admin = new Staff(['username' => 'admin', 'display_name' => 'Ana Admin', 'role_id' => 1, 'active' => true]);
        $this->admin->password_hash = PasswordHasher::hash('secret123');
        $this->admin->save();
        $this->auth = ['Authorization' => 'Bearer '.$this->admin->createToken('Front counter')->plainTextToken];
    }

    private function product(array $over = []): Product
    {
        $p = new Product(array_merge([
            'barcode' => 'BTQ'.Str::random(6), 'name' => 'Linen Wrap Dress', 'size' => 'M', 'color' => 'Sage',
            'price' => '89.00', 'cost' => '34.00', 'quantity' => 6,
        ], $over));
        $p->id = Product::newId();
        $p->save();

        return $p;
    }

    private function sell(array $payload)
    {
        return $this->withHeaders($this->auth)->postJson('/api/v1/sales', array_merge([
            'client_ref' => (string) Str::uuid(), 'payment_method' => 'Card',
        ], $payload));
    }

    // ---------------- auth ----------------

    public function test_ping_is_public(): void
    {
        $this->getJson('/api/v1/ping')->assertOk()->assertJson(['status' => 'ok', 'api' => 'v1']);
    }

    public function test_endpoints_require_a_staff_session(): void
    {
        $this->getJson('/api/v1/products')->assertUnauthorized();
        $this->withHeaders(['Authorization' => 'Bearer nonsense'])->getJson('/api/v1/products')->assertUnauthorized();
        $this->withHeaders($this->auth)->getJson('/api/v1/auth/me')->assertOk()->assertJsonPath('data.username', 'admin');
    }

    public function test_web_user_tokens_are_not_staff_sessions(): void
    {
        $token = User::factory()->create()->createToken('dashboard')->plainTextToken;
        $this->withHeaders(['Authorization' => 'Bearer '.$token])->getJson('/api/v1/products')->assertForbidden();
    }
    // ---------------- products ----------------

    public function test_create_product_generates_barcode_and_logs_stock(): void
    {
        $r = $this->withHeaders($this->auth)->postJson('/api/v1/products', [
            'name' => 'Silk Cami Top', 'size' => 'S', 'color' => null, 'price' => '54.00', 'quantity' => 8,
        ])->assertCreated();

        $r->assertJsonPath('data.barcode', 'BTQ000001')->assertJsonPath('data.price', '54.00')
            ->assertJsonPath('data.quantity', 8)->assertJsonPath('data.color', '');
        $this->assertMatchesRegularExpression('/^[0-9a-f]{32}$/', $r->json('data.id'));
        $this->assertDatabaseHas('stock_movements', ['product_id' => $r->json('data.id'), 'change_qty' => 8, 'reason' => 'new item']);

        $this->withHeaders($this->auth)->postJson('/api/v1/products', ['name' => 'Dup', 'price' => 1, 'barcode' => 'BTQ000001'])
            ->assertUnprocessable()->assertJsonValidationErrors('barcode');
        $this->withHeaders($this->auth)->postJson('/api/v1/products', ['name' => 'Bad', 'price' => '1.999'])
            ->assertUnprocessable()->assertJsonValidationErrors('price');
    }

    public function test_lookup_search_update_adjust_delete(): void
    {
        $p = $this->product(['barcode' => 'TAG/123 A']);
        $this->product(['name' => 'Woven Straw Tote', 'barcode' => 'BTQ900002']);

        $this->withHeaders($this->auth)->getJson('/api/v1/products/barcode/TAG/123 A')->assertOk()->assertJsonPath('data.id', $p->id);
        $this->withHeaders($this->auth)->getJson('/api/v1/products/barcode/NOPE')->assertNotFound();
        $this->withHeaders($this->auth)->getJson('/api/v1/products?q=linen sage')->assertOk()->assertJsonPath('meta.total', 1);

        // quantity_delta is relative: another register sold 2 meanwhile, the edit must not overwrite that
        Product::whereKey($p->id)->decrement('quantity', 2);
        $this->withHeaders($this->auth)->putJson("/api/v1/products/{$p->id}", ['price' => '79.50', 'quantity_delta' => 3])
            ->assertOk()->assertJsonPath('data.price', '79.50')->assertJsonPath('data.quantity', 7);

        $this->withHeaders($this->auth)->postJson("/api/v1/products/{$p->id}/adjust", ['delta' => -1, 'reason' => 'damaged'])
            ->assertOk()->assertJsonPath('data.quantity', 6);

        $since = now()->subMinute()->format('Y-m-d\TH:i:sP');
        $this->withHeaders($this->auth)->deleteJson("/api/v1/products/{$p->id}")->assertNoContent();
        $this->withHeaders($this->auth)->getJson('/api/v1/products?updated_since='.urlencode($since))
            ->assertOk()->assertJsonPath('deleted_ids.0', $p->id);
    }

    // ---------------- money ----------------

    public function test_money_matches_windows_app_rounding(): void
    {
        $t = Money::saleTotals([['unit_cents' => 11000, 'quantity' => 2]], '10', '7');
        $this->assertSame(['subtotal' => 22000, 'discount' => 2200, 'tax' => 1386, 'total' => 21186], $t);

        // half-cent cases round away from zero: 0.125 * 100 = 12.5 cents -> 13
        $this->assertSame(13, Money::mulDivRound(125, 1, 10));
        $this->assertSame(-13, Money::mulDivRound(-125, 1, 10));
        $this->assertSame('0.05', Money::format(5));
        $this->assertSame(8950, Money::toCents('89.5'));
    }

    // ---------------- sales ----------------

    public function test_sale_computes_totals_decrements_stock_and_numbers_receipts(): void
    {
        $p = $this->product(['price' => '110.00', 'quantity' => 10]);

        $r = $this->sell([
            'lines' => [['product_id' => $p->id, 'quantity' => 2]],
            'discount_percent' => 10, 'payment_method' => 'Cash', 'amount_tendered' => '250.00',
        ])->assertCreated();

        $r->assertJsonPath('data.number', 1001)->assertJsonPath('data.code', 'R001001')
            ->assertJsonPath('data.subtotal', '220.00')->assertJsonPath('data.discount_amount', '22.00')
            ->assertJsonPath('data.tax', '13.86')->assertJsonPath('data.total', '211.86')
            ->assertJsonPath('data.change_due', '38.14')->assertJsonPath('data.terminal', 'Front counter / admin')->assertJsonPath('data.cashier', 'Ana Admin')
            ->assertJsonPath('data.lines.0.name', 'Linen Wrap Dress')->assertJsonPath("quantities.{$p->id}", 8);
        $this->assertSame(8, $p->fresh()->quantity);
        $this->assertDatabaseHas('stock_movements', ['product_id' => $p->id, 'change_qty' => -2, 'reason' => 'sale', 'reference' => 'R001001']);

        $this->sell(['lines' => [['product_id' => $p->id, 'quantity' => 1]]])->assertCreated()->assertJsonPath('data.number', 1002);
        $this->withHeaders($this->auth)->getJson('/api/v1/sales/R001001')->assertOk()->assertJsonPath('data.total', '211.86');
        $this->withHeaders($this->auth)->getJson('/api/v1/sales?from='.now()->toDateString())->assertOk()->assertJsonPath('meta.total', 2);
    }

    public function test_retrying_a_sale_with_same_client_ref_does_not_double_charge(): void
    {
        $p = $this->product(['quantity' => 5]);
        $payload = ['client_ref' => 'till1-0001', 'lines' => [['product_id' => $p->id, 'quantity' => 1]]];

        $this->sell($payload)->assertCreated()->assertHeader('Idempotent-Replay', 'false');
        $this->sell($payload)->assertOk()->assertHeader('Idempotent-Replay', 'true')->assertJsonPath('data.number', 1001);
        $this->assertSame(4, $p->fresh()->quantity);
        $this->assertSame(1, DB::table('sales')->count());
    }

    public function test_sale_validation(): void
    {
        $p = $this->product();
        $this->sell(['lines' => []])->assertUnprocessable()->assertJsonValidationErrors('lines');
        $this->sell(['lines' => [['product_id' => $p->id, 'quantity' => 0]]])->assertUnprocessable();
        $this->sell(['lines' => [['product_id' => str_repeat('a', 32), 'quantity' => 1]]])->assertUnprocessable();
        $this->sell(['lines' => [['product_id' => $p->id, 'quantity' => 1]], 'payment_method' => 'Cash', 'amount_tendered' => '10.00'])
            ->assertUnprocessable()->assertJsonFragment(['message' => 'Cash received (10.00) is less than the total (95.23).']);
        // custom (non-inventory) line with a markdown price is allowed
        $this->sell(['lines' => [['name' => 'Alteration', 'unit_price' => '15.00', 'quantity' => 1]]])
            ->assertCreated()->assertJsonPath('data.total', '16.05');
    }

    public function test_refund_restocks_once(): void
    {
        $p = $this->product(['quantity' => 3]);
        $this->sell(['lines' => [['product_id' => $p->id, 'quantity' => 2]]])->assertCreated();
        $this->assertSame(1, $p->fresh()->quantity);

        $this->withHeaders($this->auth)->postJson('/api/v1/sales/R001001/refund')
            ->assertOk()->assertJsonPath('data.refunded', true)->assertJsonPath("quantities.{$p->id}", 3);
        $this->withHeaders($this->auth)->postJson('/api/v1/sales/1001/refund')->assertStatus(409);
        $this->assertSame(3, $p->fresh()->quantity);
    }

    // ---------------- settings & reports ----------------

    public function test_settings_tax_rate_drives_sales_and_barcode_prefix(): void
    {
        $this->withHeaders($this->auth)->getJson('/api/v1/settings')->assertOk()->assertJsonPath('data.TaxRatePercent', '7');
        $this->withHeaders($this->auth)->putJson('/api/v1/settings', ['TaxRatePercent' => '12', 'BarcodePrefix' => 'SHP', 'Unknown' => 'x'])
            ->assertOk()->assertJsonPath('data.TaxRatePercent', '12')->assertJsonMissingPath('data.Unknown');

        $this->withHeaders($this->auth)->postJson('/api/v1/barcodes/next')->assertOk()->assertJsonPath('data.barcode', 'SHP000001');
        $this->sell(['lines' => [['name' => 'Gift card', 'unit_price' => '100.00', 'quantity' => 1]]])
            ->assertCreated()->assertJsonPath('data.tax', '12.00')->assertJsonPath('data.tax_rate', '12.000');
    }

    public function test_stats(): void
    {
        $this->withHeaders($this->auth)->getJson('/api/v1/stats')->assertOk()
            ->assertJsonPath('data.sale_count', 0)->assertJsonPath('data.first_sale_date', null);
        $this->sell(['lines' => [['name' => 'Gift wrap', 'unit_price' => '2.00', 'quantity' => 1]]])->assertCreated();
        $r = $this->withHeaders($this->auth)->getJson('/api/v1/stats')->assertOk()->assertJsonPath('data.sale_count', 1);
        $this->assertStringStartsWith(now()->toDateString(), $r->json('data.first_sale_date'));
    }

    public function test_summary_report(): void
    {
        $p = $this->product(['price' => '50.00', 'quantity' => 10, 'reorder_level' => 9]);
        $this->sell(['lines' => [['product_id' => $p->id, 'quantity' => 2]], 'payment_method' => 'Card'])->assertCreated();
        $this->sell(['lines' => [['product_id' => $p->id, 'quantity' => 1]], 'payment_method' => 'Cash'])->assertCreated();
        $this->withHeaders($this->auth)->postJson('/api/v1/sales/R001002/refund')->assertOk();

        $this->withHeaders($this->auth)->getJson('/api/v1/reports/summary')->assertOk()
            ->assertJsonPath('data.sales_count', 1)->assertJsonPath('data.items_sold', 2)
            ->assertJsonPath('data.net_sales', '107.00')->assertJsonPath('data.by_payment_method.Card', '107.00')
            ->assertJsonPath('data.refunded_total', '53.50')->assertJsonPath('data.top_items.0.quantity', 2)
            ->assertJsonPath('data.low_stock_items', 1);
    }
}
