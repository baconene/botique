<?php

namespace Tests\Feature\Web;

use App\Models\Pos\Product;
use App\Models\Pos\Staff;
use App\Services\Pos\PasswordHasher;
use Illuminate\Foundation\Testing\RefreshDatabase;
use Tests\TestCase;

class StoreAndBackOfficeTest extends TestCase
{
    use RefreshDatabase;

    private function staff(string $username, int $roleId, bool $active = true): Staff
    {
        $s = new Staff(['username' => $username, 'display_name' => ucfirst($username), 'role_id' => $roleId, 'active' => $active]);
        $s->password_hash = PasswordHasher::hash('secret123');
        $s->save();

        return $s;
    }

    private function product(array $over = []): Product
    {
        $p = new Product(array_merge(['barcode' => 'BTQ'.random_int(100000, 999999), 'name' => 'Linen Wrap Dress', 'brand' => 'Maison Lune',
            'category' => 'Dresses', 'size' => 'M', 'color' => 'Sage', 'price' => '89.00', 'quantity' => 5], $over));
        $p->id = Product::newId();
        $p->save();

        return $p;
    }

    // ---------------- storefront ----------------

    public function test_storefront_shows_live_stock_grouped_by_style(): void
    {
        $m = $this->product();
        $this->product(['size' => 'S', 'quantity' => 2]);
        $this->product(['name' => 'Sold Out Tee', 'category' => 'Tops', 'quantity' => 0]);

        $this->get('/')->assertOk()->assertSee('Linen Wrap Dress')->assertSee('Shop by category')->assertDontSee('Sold Out Tee')
            ->assertSee('Staff login');
        $this->get('/shop?category=Dresses')->assertOk()->assertSee('Linen Wrap Dress')->assertDontSee('Sold Out Tee');
        $this->get('/shop?q=sage')->assertOk()->assertSee('Linen Wrap Dress');
        $this->get('/shop/item/'.$m->id)->assertOk()->assertSee('Maison Lune')->assertSee('In stock in our boutique')->assertSee('₱89.00');
    }

    public function test_storefront_works_with_an_empty_catalogue(): void
    {
        $this->get('/')->assertOk()->assertSee('Our online collection');
    }

    public function test_demo_data_command_only_fills_an_empty_catalogue(): void
    {
        $this->artisan('pos:demo-data')->assertSuccessful();
        $count = Product::count();
        $this->assertGreaterThan(30, $count);
        $this->artisan('pos:demo-data')->assertSuccessful();
        $this->assertSame($count, Product::count());
        $this->get('/')->assertOk()->assertSee('Featured pieces')->assertDontSee('Our online collection');
        $this->get('/shop')->assertOk()->assertSee('Cashmere Crew Sweater')->assertSee('80 styles');   // colorways of one style group together
    }

    public function test_product_seeder_adds_ten_styles_per_category_and_can_run_twice(): void
    {
        $this->seed(\Database\Seeders\BoutiqueProductSeeder::class);
        $count = Product::count();
        $perCategory = Product::query()->selectRaw('category, count(distinct name) as styles')->groupBy('category')->pluck('styles', 'category');
        $this->assertCount(8, $perCategory);
        foreach ($perCategory as $category => $styles) {
            $this->assertGreaterThanOrEqual(10, $styles, $category);
        }
        $this->assertSame($count, Product::query()->distinct()->count('barcode'));   // every item has its own barcode
        $this->assertSame(Product::where('quantity', '>', 0)->count(), (int) \Illuminate\Support\Facades\DB::table('stock_movements')->count());   // opening stock is logged (sold-out samples have none)
        $this->assertGreaterThan(0, Product::whereColumn('quantity', '<=', 'reorder_level')->count());   // some low stock to show

        $this->seed(\Database\Seeders\BoutiqueProductSeeder::class);                 // second run adds nothing
        $this->assertSame($count, Product::count());
    }

    // ---------------- login ----------------

    public function test_staff_login_and_logout(): void
    {
        $this->staff('owner', 1);
        $this->get('/admin')->assertRedirect('/login');
        $this->post('/login', ['username' => 'owner', 'password' => 'nope'])->assertSessionHasErrors('username');
        $this->post('/login', ['username' => 'OWNER', 'password' => 'secret123'])->assertRedirect('/admin');
        $this->assertAuthenticated('staff');
        $this->get('/admin')->assertOk()->assertSee('Point of sale')->assertSee('Online inventory');
        $this->post('/logout')->assertRedirect('/');
        $this->assertGuest('staff');
    }

    public function test_disabled_staff_cannot_sign_in_and_is_kicked_out(): void
    {
        $this->staff('ex', 3, false);
        $this->post('/login', ['username' => 'ex', 'password' => 'secret123'])->assertSessionHasErrors('username');

        $s = $this->staff('maria', 3);
        $this->actingAs($s, 'staff')->get('/admin')->assertOk();
        $s->forceFill(['active' => false])->save();
        $this->get('/admin')->assertRedirect('/login');
    }

    // ---------------- roles ----------------

    public function test_cashier_sees_pos_but_not_inventory(): void
    {
        $this->actingAs($this->staff('maria', 3), 'staff');
        $this->get('/admin')->assertOk()->assertSee('Point of sale')->assertDontSee('Online inventory');
        $this->get('/admin/pos')->assertOk();
        $this->get('/admin/inventory')->assertForbidden();
        $this->get('/admin/inventory/create')->assertForbidden();
    }

    public function test_stock_clerk_manages_inventory_but_cannot_sell(): void
    {
        $p = $this->product(['quantity' => 3]);
        $this->actingAs($this->staff('sam', 4), 'staff');
        $this->get('/admin/pos')->assertForbidden();
        $this->get('/admin/inventory')->assertOk()->assertSee('Linen Wrap Dress');

        $this->post('/admin/inventory', ['name' => 'Silk Cami Top', 'category' => 'Tops', 'size' => 'S', 'color' => 'Ivory',
            'price' => '54.00', 'cost' => '', 'quantity' => 4, 'reorder_level' => 2])->assertRedirect();
        $new = Product::firstWhere('name', 'Silk Cami Top');
        $this->assertNotNull($new);
        $this->assertSame('BTQ000001', $new->barcode);
        $this->assertSame(4, $new->quantity);

        $this->put("/admin/inventory/{$p->id}", ['name' => 'Linen Wrap Dress', 'barcode' => $p->barcode, 'price' => '79.50',
            'reorder_level' => 2, 'quantity_delta' => 2])->assertRedirect();
        $this->assertSame('79.50', $p->fresh()->price);
        $this->assertSame(5, $p->fresh()->quantity);

        $this->post("/admin/inventory/{$p->id}/adjust", ['delta' => -1, 'reason' => 'damaged'])->assertRedirect();
        $this->assertSame(4, $p->fresh()->quantity);
        $this->assertDatabaseHas('stock_movements', ['product_id' => $p->id, 'change_qty' => -1, 'terminal' => 'Web POS / sam']);

        $this->delete("/admin/inventory/{$new->id}")->assertRedirect();
        $this->assertNull(Product::find($new->id));
    }

    // ---------------- product photos ----------------

    public function test_edit_page_uploads_shows_and_removes_a_photo_for_all_sizes_of_a_colour(): void
    {
        \Illuminate\Support\Facades\Storage::fake('public');
        $m = $this->product(['size' => 'M']);
        $s = $this->product(['size' => 'S']);
        $ivory = $this->product(['size' => 'M', 'color' => 'Ivory']);
        $this->actingAs($this->staff('sam', 4), 'staff');

        $this->get("/admin/inventory/{$m->id}/edit")->assertOk()->assertSee('No photo yet')->assertSee('All 2 sizes in Sage');

        $file = \Illuminate\Http\UploadedFile::fake()->image('dress.jpg', 800, 1000);
        $this->put("/admin/inventory/{$m->id}", ['name' => $m->name, 'barcode' => $m->barcode, 'price' => '89.00', 'photo' => $file, 'photo_scope' => 'color'])
            ->assertRedirect()->assertSessionHas('status', fn ($s) => str_contains($s, 'photo uploaded for 2 items'));
        $path = 'products/'.$file->hashName();
        \Illuminate\Support\Facades\Storage::disk('public')->assertExists($path);
        $this->assertDatabaseHas('product_images', ['product_id' => $s->id, 'path' => $path]);
        $this->assertDatabaseMissing('product_images', ['product_id' => $ivory->id]);

        $url = route('media.product', $file->hashName());
        $this->get("/admin/inventory/{$m->id}/edit")->assertOk()->assertSee($url)->assertSee('Remove the photo');
        $this->get('/admin/inventory')->assertSee($url);
        $this->get('/shop')->assertSee($url);
        $this->get('/shop/item/'.$s->id)->assertSee($url);
        $this->get($url)->assertOk();

        $this->put("/admin/inventory/{$m->id}", ['name' => $m->name, 'barcode' => $m->barcode, 'price' => '89.00', 'remove_photo' => 1, 'photo_scope' => 'color'])
            ->assertRedirect();
        $this->assertSame(0, \App\Models\Pos\ProductImage::count());
        \Illuminate\Support\Facades\Storage::disk('public')->assertMissing($path);   // file no longer used, so deleted
    }

    public function test_photo_link_and_add_variant_keeps_the_photo(): void
    {
        $p = $this->product();
        $this->actingAs($this->staff('sam', 4), 'staff');
        $this->put("/admin/inventory/{$p->id}", ['name' => $p->name, 'barcode' => $p->barcode, 'price' => '89.00',
            'photo_url' => 'https://images.unsplash.com/photo-1764298493197-a1c1cce57800', 'photo_scope' => 'item'])->assertRedirect();
        $this->get('/shop')->assertSee('https://images.unsplash.com/photo-1764298493197-a1c1cce57800?auto=format', false);

        $this->get("/admin/inventory/create?from={$p->id}")->assertOk()->assertSee('copy_photo_from', false);
        $this->post('/admin/inventory', ['name' => $p->name, 'brand' => $p->brand, 'size' => 'L', 'color' => 'Sage', 'price' => '89.00',
            'copy_photo_from' => $p->id])->assertRedirect();
        $l = Product::where('size', 'L')->first();
        $this->assertSame('https://images.unsplash.com/photo-1764298493197-a1c1cce57800', $l->image->path);

        $this->put("/admin/inventory/{$p->id}", ['name' => $p->name, 'barcode' => $p->barcode, 'price' => '89.00', 'photo_url' => 'http://insecure.example/x.jpg'])
            ->assertSessionHasErrors('photo_url');
        $this->delete("/admin/inventory/{$l->id}")->assertRedirect();
        $this->assertDatabaseMissing('product_images', ['product_id' => $l->id]);
    }

    public function test_seeded_demo_items_all_have_photos(): void
    {
        $this->seed(\Database\Seeders\BoutiqueProductSeeder::class);
        $this->assertSame(Product::count(), \App\Models\Pos\ProductImage::count());
        $this->assertCount(80, \Database\Seeders\BoutiqueProductSeeder::PHOTOS);
        foreach (\Database\Seeders\BoutiqueProductSeeder::CATALOGUE as [, $styles]) {
            foreach ($styles as $style) {
                $this->assertNotNull(\Database\Seeders\BoutiqueProductSeeder::photoUrl($style[0]), $style[0].' has no photo');
            }
        }
        $this->get('/shop')->assertSee('images.unsplash.com/photo-', false);
    }

    // ---------------- web POS ----------------

    public function test_web_pos_search_sale_and_receipt(): void
    {
        $p = $this->product(['price' => '110.00', 'quantity' => 10]);
        $this->actingAs($this->staff('owner', 1), 'staff');

        $this->getJson('/admin/pos/search?q='.$p->barcode)->assertOk()->assertJsonPath('exact', true)->assertJsonPath('data.0.id', $p->id);
        $this->getJson('/admin/pos/search?q=linen')->assertOk()->assertJsonPath('exact', false)->assertJsonCount(1, 'data');

        $r = $this->postJson('/admin/pos/sale', [
            'client_ref' => 'web-1', 'payment_method' => 'Cash', 'amount_tendered' => '250.00', 'discount_percent' => 10,
            'lines' => [['product_id' => $p->id, 'quantity' => 2, 'unit_price' => '110.00']],
        ])->assertCreated()->assertJsonPath('data.total', '211.86')->assertJsonPath('data.change_due', '38.14')
            ->assertJsonPath('data.terminal', 'Web POS / owner')->assertJsonPath('data.cashier', 'Owner');
        $this->assertSame(8, $p->fresh()->quantity);

        $this->get($r->json('receipt_url'))->assertOk()->assertSee('R001001')->assertSee('Cashier: Owner')->assertSee('211.86');
        // Retry with the same client_ref does not charge twice
        $this->postJson('/admin/pos/sale', ['client_ref' => 'web-1', 'payment_method' => 'Card',
            'lines' => [['product_id' => $p->id, 'quantity' => 2]]])->assertOk();
        $this->assertSame(8, $p->fresh()->quantity);
    }

    public function test_web_pos_blocks_discounts_for_cashiers(): void
    {
        $p = $this->product();
        $this->actingAs($this->staff('maria', 3), 'staff');
        $this->get('/admin/pos')->assertOk()->assertSee('Your role does not allow discounts');
        $this->postJson('/admin/pos/sale', ['client_ref' => 'x', 'payment_method' => 'Card', 'discount_percent' => 15,
            'lines' => [['product_id' => $p->id, 'quantity' => 1]]])->assertForbidden();
        $this->postJson('/admin/pos/sale', ['client_ref' => 'y', 'payment_method' => 'Card',
            'lines' => [['product_id' => $p->id, 'quantity' => 1]]])->assertCreated();
    }
}
