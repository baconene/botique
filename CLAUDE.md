# Boutique POS & Inventory — project guide

A Windows desktop app (WinForms, .NET Framework 4.8) for a clothing boutique:
inventory, point of sale, sales history/refunds, Code 128 barcode labels,
receipts, and Bluetooth barcode scanner support (HID keyboard mode and SPP serial mode).

## Hard constraints (read before changing code)
- **No SDK install required.** The app is compiled with the C# compiler built into Windows:
  `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`. Build with `.\build.ps1`.
- That compiler only supports **C# 5**. Do NOT use: string interpolation `$"..."`, `?.`, `??=`,
  `nameof`, expression-bodied members `=>` on members, auto-property initializers, `out var`,
  tuples, pattern matching, local functions. Lambdas, LINQ, generics and optional params are fine.
- **No NuGet packages.** Only framework assemblies (System, System.Core, System.Data, System.Drawing,
  System.Security, System.Web.Extensions, System.Windows.Forms, System.Xml). Barcode encoding (Code 128B) is hand-written in `src/Code128.cs`.
  MySQL is reached through **System.Data.Odbc + MySQL Connector/ODBC**, which the installer installs. The web API is reached
  with `HttpWebRequest` + `JavaScriptSerializer` (`src/ApiStore.cs`).
- UI is built in code (no designer files). Use `Ui.S(px)` for any fixed pixel size so it scales with DPI.
- **Data goes through the `DataStore` facade only** (`SaveProduct`, `ChangeQuantity`, `RecordSale`, `RefundSale`,
  `SalesBetween`, `SaveSettings`...). Never mutate `DataStore.Data` and persist it by hand. The back-end is
  `XmlStore` (offline file), `MySqlStore` (local or other MySQL) or `ApiStore` (our Laravel web server), chosen by `db.config`.
  With `ApiStore`, the server assigns sale numbers and totals, and `InsertSale` copies them back onto the `Sale`.
- Data folder: `%ProgramData%\BoutiquePOS` when installed; `bin\Data` for dev builds (`bin\portable.txt`).
- Money is `decimal`; round with `SaleMath.R()` (2 dp, away from zero).
- **RBAC everywhere** (`src/Security.cs`): staff sign in at start (`LoginForm`); `Session.Current` holds the permissions.
  Every `DataStore` write calls `Session.Require(...)`, and screens call `ApplyPermissions()`. Permission keys, default roles
  and the password hash format (`pbkdf2-sha256$100000$salt$hash`) MUST match `web/app/Services/Pos/Permissions.php` and
  `PasswordHasher.php`. Accounts are in the `staff` / `staff_roles` tables (POS schema v2), or in the XML file when offline.
  Built-in Admin role id 1 = "*". Rules: at least one active Admin, and nobody can disable or delete themselves.

## Layout
- `src/Program.cs` — entry point, DPI awareness, `--selftest <outdir>` mode used by QA.
- `src/Models.cs` — Product, Sale, SaleLine, StoreSettings, AppData, SaleMath.
- `src/DataStore.cs` — the data facade used by all screens (store first, then cache).
- `src/Storage.cs` — `IStore`, `XmlStore`, `MySqlStore` (schema migrations, ODBC queries), sample data.
- `src/DbConfig.cs` — `AppPaths`, `db.config` (DPAPI-encrypted password), ODBC driver detection.
- `src/DatabaseSetupForm.cs` — first-run wizard: this PC / cloud (Forge) / other server / offline.
- `installer/` — Inno Setup script, prerequisite downloader, local MySQL setup/removal scripts.
- `src/Code128.cs` — Code 128B encoder and pixel-exact bar drawing.
- `src/ScannerService.cs` — keyboard-wedge burst detection (IMessageFilter) + SerialPort (SPP).
- `src/Printing.cs` — label layouts (Avery 5160 sheet, roll labels) and receipt printing.
- `src/MainForm.cs` and `src/*Panel.cs` — the tabs: Sell, Inventory, Sales, Labels, Settings.
- `src/Dialogs.cs` — product editor, item picker, prompt.

## Build / run / test
```powershell
.\build.ps1                 # -> bin\BoutiquePOS.exe
.\bin\BoutiquePOS.exe
.\bin\BoutiquePOS.exe --selftest .\selftest-out   # writes report.txt + PNG screenshots, exit code 0 = pass
.\bin\BoutiquePOS.exe --dbtest localhost 3306 boutique_test user pass   # MySQL store tests (throwaway DB!)
.\bin\BoutiquePOS.exe --apitest http://127.0.0.1:8123 <adminUser> <adminPassword> # web API tests (test server!)
.\build-installer.ps1       # -> dist\BoutiquePOS-Setup-<ver>.exe (needs Inno Setup 6; downloads prerequisites once)
```
Version number lives in `src/AssemblyInfo.cs` and `installer/BoutiquePOS.iss` (keep both in sync).

## Web server project (`web/`)
A Laravel 13 app (PHP 8.5 via Laravel Herd) for our web servers, deployed with Forge. It is separate from the
Windows app, and none of the C# 5 rules above apply to it. Run commands from inside `web/`:
```powershell
php artisan serve      # http://127.0.0.1:8000  (or use Herd: http://web.test after `herd link`)
php artisan test
```
It hosts the **POS API v1** (`web/routes/api.php`, reference in `web/docs/API.md`): staff sign-in (`POST /auth/login`, expiring
Sanctum sessions, first admin via `php artisan pos:admin`), role checks on every route (`pos.can:` middleware), products, sales with server-side totals and retry protection (`client_ref`), refunds,
shared settings, and reports. Business logic is in `web/app/Services/Pos/`, and tests are in `web/tests/Feature/Pos/`.
Money math (`Money::saleTotals`) must stay identical to `SaleMath.Compute` in the Windows app.

Local dev uses SQLite (`web/.env`). On Forge, point `.env` at the server's MySQL. The POS tables (`products`, `sales`,
`sale_lines`, `stock_movements`, `counters`, `settings`, `schema_info`) are owned by `MySqlStore` migrations (see the
`database-engineer` agent). Laravel's `create_pos_tables` migration only creates them if they're missing and must never
alter or drop them. Schema changes start in `MySqlStore` and are then mirrored there.
If `artisan` says a directory "must be present and writable", OneDrive set the read-only flag:
`attrib -R storage /S /D` and `attrib -R bootstrap\cache /S /D`.

## Agents
Project agents live in `.claude/agents/`: `pos-architect`, `pos-developer`, `database-engineer`,
`hardware-integrator`, `qa-tester`. Use them for future feature work.
