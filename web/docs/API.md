# Boutique POS API v1

The HTTPS API the Windows POS registers use to share one database on the web server (e.g. Forge).
Base URL: `https://<your-domain>/api/v1`. All bodies are JSON. Money is sent and returned as strings with
2 decimals (`"89.00"`). Times are the shop's local time with offset (`2026-11-24T14:05:00+08:00`).

## Sign-in and roles (RBAC)
Each person signs in with their own **staff account**; nobody has to create tokens by hand.
```
POST /api/v1/auth/login   {"username": "maria", "password": "...", "device": "FRONT-PC"}
-> 200 {"token": "...", "expires_at": "...", "user": {"id", "username", "display_name", "role_id", "role_name", "permissions": [...]}}
```
The app sends `Authorization: Bearer <token>` on the following requests. The session ends at `POST /auth/logout`, after
`SANCTUM_EXPIRATION` minutes (default 720 = 12 h), or when an admin disables the account, changes its role or resets its
password. Wrong credentials return `401`; a disabled account returns `403`. Sign-in is limited to 10 attempts per minute per
username and IP address.

Create the **first Admin** on the server (online, admins can't be created through the API until one exists):
```bash
php artisan pos:admin owner --name="Olivia Owner"   # asks for the password; also resets a locked-out admin
php artisan pos:staff                               # list accounts, roles, last sign-in, active sessions
```
After that, the Admin manages everyone in the app (Users & roles tab) or through the endpoints below.

**Permissions:** `sell`, `discount`, `refund`, `sales.view`, `inventory.view`, `inventory.edit`, `stock.adjust`,
`labels.print`, `settings.manage`, `users.manage`. **Default roles:** Admin (everything, built in, can't be edited
or deleted), Manager (everything except users), Cashier (`sell`, `labels.print`), Stock clerk (inventory, stock and labels).
Every endpoint checks the role. A forbidden action returns `403 {"message": "Your role does not allow this (...)."}`.
Discounts and price changes in `POST /sales` need `discount`. At least one active Admin always remains, and users can't
disable or delete their own account.

Accounts live in the POS tables `staff` and `staff_roles` (POS schema v2). Passwords use
`pbkdf2-sha256$100000$<salt>$<hash>`, the same format the Windows app uses, so one set of accounts works for direct-MySQL
registers and API registers alike.

Rate limit: 600 requests/minute per signed-in user.

Errors: `401` not signed in or session ended, `403` role not allowed or account disabled, `404` not found,
`409` conflict (already refunded, last Admin...), `422` validation (`{"message": "...", "errors": {"field": ["..."]}}`).
## Endpoints

| Method & path | Purpose |
|---|---|
| `GET /ping` | Health check (no auth). |
| `POST /auth/login` / `POST /auth/logout` / `GET /auth/me` | Sign in (no auth needed), sign out, and the current user with permissions. |
| `GET /products` | List / search. Query: `q` (all words must match name, barcode, brand, category, size or color), `category`, `low_stock=1`, `updated_since`, `per_page` (≤1000, default 100), `page`. With `updated_since`, the response also has `deleted_ids` so a register can sync. |
| `GET /products/barcode/{barcode}` | Scan lookup. |
| `GET /products/{id}` | One item. |
| `POST /products` | Create. `name`, `price` required. If `barcode` is omitted, the next `BTQ000123`-style code is assigned. Optional: `id` (32 hex, for items created offline), `category`, `brand`, `size`, `color`, `cost`, `quantity`, `reorder_level`, `notes`. |
| `PUT /products/{id}` | Edit any field. Change stock with **`quantity_delta`** (relative), never an absolute quantity, so sales made on other registers in the meantime aren't lost. |
| `POST /products/{id}/adjust` | `{"delta": -1, "reason": "damaged"}` for receiving, counts and damages. Logged in `stock_movements`. |
| `DELETE /products/{id}` | Delete. Past sales keep their own copy of the item details. |
| `POST /barcodes/next` | Reserve the next generated barcode. |
| `GET /sales` | `from`, `to` (dates, inclusive, default today), `per_page`, `page`. Includes lines. |
| `GET /sales/{code}` | By number (`1001`) or receipt code (`R001001`, as printed on the receipt barcode). |
| `POST /sales` | Record a sale (see below). |
| `POST /sales/{code}/refund` | Full refund: items go back into stock. `409` if already refunded. |
| `GET /settings` / `PUT /settings` | Shared store settings: `StoreName`, `Address`, `Phone`, `ReceiptFooter`, `TaxRatePercent`, `BarcodePrefix`. |
| `GET /stats` | Product and sale counts, plus first and last sale dates (the app uses this for "All time"). |
| `GET /permissions`, `GET/POST /roles`, `PUT/DELETE /roles/{id}` | Role administration (`users.manage`). Body: `{"name": "...", "permissions": ["sell", ...]}`. |
| `GET/POST /staff`, `PUT/DELETE /staff/{id}` | User administration (`users.manage`). Body: `username`, `display_name`, `password` (min 6; optional on edit), `role_id`, `active`. |
| `GET /reports/summary` | `from`, `to`. Sales count, items sold, net sales, discounts, tax, totals by payment method, refunds, top 10 items, low-stock count. |

## Recording a sale
```json
POST /api/v1/sales
{
  "client_ref": "c0a8f1e2-...-register-generated-guid",
  "lines": [
    { "product_id": "3f2a...32 hex", "quantity": 2 },
    { "product_id": "9b1c...", "quantity": 1, "unit_price": "45.00" },
    { "name": "Alteration", "unit_price": "15.00", "quantity": 1 }
  ],
  "discount_percent": 10,
  "payment_method": "Cash",
  "amount_tendered": "250.00",
  "note": ""
}
```
- **The server calculates the totals.** It uses the tax rate from shared settings and the same rounding as the
  Windows app (half away from zero, to the cent). Print the receipt from the **response**, not from local math.
- `unit_price` is optional for inventory items (the current price is used). Send it to apply a markdown.
  Lines without `product_id` are custom charges and need `name` + `unit_price`.
- `payment_method`: `Cash` | `Card` | `Other`. For cash, `amount_tendered` must cover the total; `change_due` is returned.
- **Retries are safe.** Generate one `client_ref` per sale and reuse it if the request times out. A repeat
  returns the original sale with `200` and `Idempotent-Replay: true`, so nothing is charged or deducted twice.
- `sale_date` (optional, not in the future) lets a register upload sales it queued while offline.

Response `201`:
```json
{
  "data": { "number": 1001, "code": "R001001", "sale_date": "...", "subtotal": "220.00",
            "discount_percent": "10.00", "discount_amount": "22.00", "tax_rate": "7.000", "tax": "13.86",
            "total": "211.86", "payment_method": "Cash", "amount_tendered": "250.00", "change_due": "38.14",
            "refunded": false, "refund_date": null, "terminal": "Front counter",
            "lines": [ { "line_no": 1, "product_id": "...", "barcode": "BTQ000001", "name": "...",
                         "size": "M", "color": "Sage", "unit_price": "110.00", "quantity": 2 } ] },
  "quantities": { "3f2a...": 8 }
}
```
`quantities` is the new stock on hand for each item sold, so the register can update its screen.

## Database
The API works on the **same tables as the Windows app's MySQL mode** (`products`, `sales`, `sale_lines`,
`stock_movements`, `counters`, `settings`). Registers can use the API and direct MySQL at the same time.
The Laravel migration only creates those tables if they're missing; it never alters or drops them.
The Laravel-owned tables are `pos_registers`, `pos_sale_requests` (retry protection) and
`pos_deleted_products` (sync tombstones).

## Connecting a Windows register
In Boutique POS choose **Cloud - our web server**, either on first run or later in Settings & Scanner > Change database
connection. Enter the site address (`https://...`) then click **Test connection** and **Save**; staff then sign in with their own username and password. The app checks the server on every start. It downloads the catalogue once, then syncs only changed and deleted
items (`updated_since`) when the Sell, Inventory or Labels tab is opened. If a sale request times out, the app resends it
with the same `client_ref`, so it is never recorded twice.

## Deploying on Forge
1. Create the site, point it at this `web/` folder's repository, and enable SSL (Let's Encrypt).
2. In the site's `.env`, set `DB_CONNECTION=mysql`, the DB credentials, `APP_ENV=production`,
   `APP_DEBUG=false`, and `APP_TIMEZONE` to the shop's timezone (e.g. `Asia/Manila`).
3. Add `php artisan migrate --force` to the deploy script.
4. SSH in and run `php artisan pos:admin <username>` once to create the first Admin.
5. With the API in place, MySQL port 3306 doesn't need to be open to the internet.
