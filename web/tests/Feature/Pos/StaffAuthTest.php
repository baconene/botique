<?php

namespace Tests\Feature\Pos;

use App\Models\Pos\Product;
use App\Models\Pos\Staff;
use App\Services\Pos\PasswordHasher;
use Illuminate\Foundation\Testing\RefreshDatabase;
use Illuminate\Support\Str;
use Tests\TestCase;

class StaffAuthTest extends TestCase
{
    use RefreshDatabase;

    private function staff(string $username, int $roleId, string $password = 'secret123', bool $active = true): Staff
    {
        $s = new Staff(['username' => $username, 'display_name' => ucfirst($username), 'role_id' => $roleId, 'active' => $active]);
        $s->password_hash = PasswordHasher::hash($password);
        $s->save();

        return $s;
    }

    private function login(string $username, string $password = 'secret123'): array
    {
        $r = $this->postJson('/api/v1/auth/login', ['username' => $username, 'password' => $password, 'device' => 'Till 1'])->assertOk();

        return ['Authorization' => 'Bearer '.$r->json('token')];
    }

    private function product(): Product
    {
        $p = new Product(['barcode' => 'BTQ'.Str::random(5), 'name' => 'Silk Cami', 'price' => '54.00', 'quantity' => 5]);
        $p->id = Product::newId();
        $p->save();

        return $p;
    }

    // ---------------- hashing & login ----------------

    public function test_password_hash_format_matches_windows_app(): void
    {
        // Same vector is asserted in the Windows app's --selftest (Security.cs).
        $stored = 'pbkdf2-sha256$100000$'.base64_encode('fixed-salt-16byt').'$Tp1pzUOJkR3nd6S73zjMJlozvBG0iGolb7PdoeDgWhE=';
        $this->assertTrue(PasswordHasher::verify('S3cret!pass', $stored));
        $this->assertFalse(PasswordHasher::verify('wrong', $stored));
        $this->assertTrue(PasswordHasher::verify('abc123', PasswordHasher::hash('abc123')));
    }

    public function test_login_returns_session_with_role_permissions(): void
    {
        $this->staff('maria', 3);
        $r = $this->postJson('/api/v1/auth/login', ['username' => ' MARIA ', 'password' => 'secret123', 'device' => 'Till 1'])->assertOk();
        $r->assertJsonPath('user.username', 'maria')->assertJsonPath('user.role_name', 'Cashier')
            ->assertJsonPath('user.permissions', ['sell', 'labels.print']);
        $this->assertNotEmpty($r->json('expires_at'));
        $this->assertNotNull(Staff::firstWhere('username', 'maria')->last_login);

        $auth = ['Authorization' => 'Bearer '.$r->json('token')];
        $this->withHeaders($auth)->getJson('/api/v1/auth/me')->assertOk()->assertJsonPath('data.username', 'maria');
        $this->withHeaders($auth)->postJson('/api/v1/auth/logout')->assertNoContent();
        $this->app['auth']->forgetGuards();
        $this->withHeaders($auth)->getJson('/api/v1/auth/me')->assertUnauthorized();
    }

    public function test_login_rejects_bad_credentials_and_disabled_accounts(): void
    {
        $this->staff('maria', 3);
        $this->staff('ex', 3, 'secret123', false);
        $this->postJson('/api/v1/auth/login', ['username' => 'maria', 'password' => 'nope'])->assertUnauthorized();
        $this->postJson('/api/v1/auth/login', ['username' => 'ghost', 'password' => 'secret123'])->assertUnauthorized();
        $this->postJson('/api/v1/auth/login', ['username' => 'ex', 'password' => 'secret123'])->assertForbidden();
    }

    public function test_expired_session_is_refused(): void
    {
        $s = $this->staff('maria', 3);
        $token = $s->createToken('Till', ['*'], now()->subMinute())->plainTextToken;
        $this->withHeaders(['Authorization' => 'Bearer '.$token])->getJson('/api/v1/auth/me')->assertUnauthorized();
    }

    public function test_pos_admin_command_bootstraps_first_admin(): void
    {
        $this->artisan('pos:admin', ['username' => 'Owner', '--password' => 'topsecret'])->assertSuccessful();
        $this->postJson('/api/v1/auth/login', ['username' => 'owner', 'password' => 'topsecret'])
            ->assertOk()->assertJsonPath('user.role_name', 'Admin')->assertJsonCount(10, 'user.permissions');
    }

    // ---------------- permissions ----------------

    public function test_cashier_can_sell_but_not_refund_discount_edit_or_manage(): void
    {
        $p = $this->product();
        $this->staff('maria', 3);
        $auth = $this->login('maria');

        $this->withHeaders($auth)->getJson('/api/v1/products/barcode/'.$p->barcode)->assertOk();
        $this->withHeaders($auth)->postJson('/api/v1/sales', [
            'client_ref' => 'c1', 'payment_method' => 'Card', 'lines' => [['product_id' => $p->id, 'quantity' => 1]],
        ])->assertCreated()->assertJsonPath('data.cashier', 'Maria')->assertJsonPath('data.terminal', 'Till 1 / maria');

        $this->withHeaders($auth)->postJson('/api/v1/sales', [
            'client_ref' => 'c2', 'payment_method' => 'Card', 'discount_percent' => 10, 'lines' => [['product_id' => $p->id, 'quantity' => 1]],
        ])->assertForbidden();
        $this->withHeaders($auth)->postJson('/api/v1/sales', [
            'client_ref' => 'c3', 'payment_method' => 'Card', 'lines' => [['product_id' => $p->id, 'quantity' => 1, 'unit_price' => '1.00']],
        ])->assertForbidden();
        $this->withHeaders($auth)->postJson('/api/v1/sales/R001001/refund')->assertForbidden();
        $this->withHeaders($auth)->getJson('/api/v1/sales')->assertForbidden();
        $this->withHeaders($auth)->postJson('/api/v1/products', ['name' => 'X', 'price' => 1])->assertForbidden();
        $this->withHeaders($auth)->postJson("/api/v1/products/{$p->id}/adjust", ['delta' => 1])->assertForbidden();
        $this->withHeaders($auth)->putJson('/api/v1/settings', ['StoreName' => 'Hacked'])->assertForbidden();
        $this->withHeaders($auth)->getJson('/api/v1/staff')->assertForbidden();
        $this->assertSame(4, $p->fresh()->quantity);
    }

    public function test_stock_clerk_manages_inventory_but_cannot_sell(): void
    {
        $p = $this->product();
        $this->staff('sam', 4);
        $auth = $this->login('sam');
        $this->withHeaders($auth)->postJson("/api/v1/products/{$p->id}/adjust", ['delta' => 3])->assertOk()->assertJsonPath('data.quantity', 8);
        $this->withHeaders($auth)->postJson('/api/v1/products', ['name' => 'New tee', 'price' => '20.00'])->assertCreated();
        $this->withHeaders($auth)->postJson('/api/v1/sales', [
            'client_ref' => 'c1', 'payment_method' => 'Card', 'lines' => [['product_id' => $p->id, 'quantity' => 1]],
        ])->assertForbidden();
    }

    public function test_disabling_a_user_ends_their_sessions(): void
    {
        $this->staff('admin', 1);
        $maria = $this->staff('maria', 3);
        $adminAuth = $this->login('admin');
        $mariaAuth = $this->login('maria');

        $this->withHeaders($adminAuth)->putJson("/api/v1/staff/{$maria->id}", ['active' => false])->assertOk()->assertJsonPath('data.active', false);
        $this->app['auth']->forgetGuards();
        $this->withHeaders($mariaAuth)->getJson('/api/v1/auth/me')->assertUnauthorized();
    }

    // ---------------- users & roles admin ----------------

    public function test_admin_manages_users(): void
    {
        $admin = $this->staff('admin', 1);
        $auth = $this->login('admin');

        $r = $this->withHeaders($auth)->postJson('/api/v1/staff', [
            'username' => 'Joy.R', 'display_name' => 'Joy', 'password' => 'joypass1', 'role_id' => 3,
        ])->assertCreated()->assertJsonPath('data.username', 'joy.r')->assertJsonMissingPath('data.password_hash');
        $this->login('joy.r', 'joypass1');

        $this->withHeaders($auth)->postJson('/api/v1/staff', ['username' => 'joy.r', 'password' => 'x12345', 'role_id' => 3])
            ->assertUnprocessable()->assertJsonValidationErrors('username');
        $this->withHeaders($auth)->postJson('/api/v1/staff', ['username' => 'a b', 'password' => '1', 'role_id' => 99])
            ->assertUnprocessable()->assertJsonValidationErrors(['username', 'password', 'role_id']);

        $this->withHeaders($auth)->putJson("/api/v1/staff/{$r->json('data.id')}", ['password' => 'newpass1', 'role_id' => 2])
            ->assertOk()->assertJsonPath('data.role_name', 'Manager');
        $this->login('joy.r', 'newpass1');

        // Protections: no self-disable, no self-delete, always one active Admin
        $this->withHeaders($auth)->putJson("/api/v1/staff/{$admin->id}", ['active' => false])->assertStatus(409);
        $this->withHeaders($auth)->putJson("/api/v1/staff/{$admin->id}", ['role_id' => 2])->assertStatus(409);
        $this->withHeaders($auth)->deleteJson("/api/v1/staff/{$admin->id}")->assertStatus(409);
        $this->withHeaders($auth)->getJson('/api/v1/staff')->assertOk()->assertJsonCount(2, 'data');
        $this->withHeaders($auth)->deleteJson("/api/v1/staff/{$r->json('data.id')}")->assertNoContent();
    }

    public function test_admin_manages_roles(): void
    {
        $this->staff('admin', 1);
        $auth = $this->login('admin');

        $this->withHeaders($auth)->getJson('/api/v1/permissions')->assertOk()->assertJsonCount(10, 'data');
        $this->withHeaders($auth)->getJson('/api/v1/roles')->assertOk()->assertJsonPath('data.0.name', 'Admin')->assertJsonPath('data.0.is_system', true);

        $r = $this->withHeaders($auth)->postJson('/api/v1/roles', ['name' => 'Senior cashier', 'permissions' => ['sell', 'discount', 'refund']])
            ->assertCreated()->assertJsonPath('data.permissions', ['sell', 'discount', 'refund']);
        $this->withHeaders($auth)->postJson('/api/v1/roles', ['name' => 'Bad', 'permissions' => ['fly']])->assertUnprocessable();
        $this->withHeaders($auth)->putJson("/api/v1/roles/{$r->json('data.id')}", ['name' => 'Senior cashier', 'permissions' => ['sell']])
            ->assertOk()->assertJsonPath('data.permissions', ['sell']);

        $this->withHeaders($auth)->putJson('/api/v1/roles/1', ['name' => 'Admin', 'permissions' => []])->assertStatus(409);
        $this->withHeaders($auth)->deleteJson('/api/v1/roles/1')->assertStatus(409);
        $this->withHeaders($auth)->deleteJson('/api/v1/roles/3')->assertNoContent();             // Cashier, unused here
        $this->staff('lee', 2);
        $this->withHeaders($auth)->deleteJson('/api/v1/roles/2')->assertStatus(409);              // Manager, in use
    }
}
