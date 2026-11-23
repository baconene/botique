# Boutique POS & Inventory (Windows)

Inventory + point-of-sale app for a clothing boutique. It works with a Bluetooth barcode
scanner and prints Code 128 barcode labels and receipts. It runs on any Windows 10/11 PC and needs nothing installed.

## Run it
Double-click **`bin\BoutiquePOS.exe`**. (To rebuild after code changes, run `.\build.ps1`.)
On first launch, 6 sample items are created so you can try things out. Edit or delete them whenever you like.

## Screens
| Tab | What it does |
|---|---|
| **Sell (F1)** | Scan or search items into the cart. Change quantity or price (markdowns), apply a % discount, add tax, take Cash/Card/Other, see change due, and finish the sale with **F12**. Stock goes down automatically. |
| **Inventory (F2)** | Add, edit or delete items (name, category, brand, size, color, price, cost, qty, low-stock alert). **Add size/color variant** copies an item with a new barcode. **Receive stock mode**: each scan adds +1. Also: CSV import/export and low-stock highlighting. |
| **Sales History (F3)** | Sales by date range with totals (net, tax, cash/card split). Reprint or preview a receipt, refund a sale (items go back into stock), or export to CSV. Scan the barcode on a receipt to jump to that sale. |
| **Barcode Labels (F4)** | Queue items (scan them or pick from inventory) and set copies. Then preview and print on Avery 5160/5163 sheets, thermal roll labels (2"x1", 2.25"x1.25", 1.5"x1) or 2"x3" hang tags. You can also save a barcode as a PNG. |
| **Settings & Scanner** | Store name, address, tax %, receipt footer, barcode prefix, scanner mode/COM port, receipt printer, and backups. |

## Connecting a Bluetooth barcode scanner
1. Put the scanner in pairing mode (usually by scanning a "pair" barcode in its manual).
2. Go to **Settings & Scanner > Open Windows Bluetooth settings > Add device > Bluetooth** and pick the scanner.
3. **Keyboard/HID mode (most scanners):** nothing else to set up. Scans work on every tab, even when no text box has focus.
   The scanner must send **Enter** after each code, which is the factory default on almost all scanners.
4. **SPP/serial mode:** choose *Serial*, pick the COM port (usually the "Outgoing" Bluetooth port) and click **Apply**.
5. Scan anything while the Settings tab is open. The code shows up in the test area.

What a scan does depends on the tab: it adds the item to the sale, finds the item (or adds stock in receive mode), queues a label, or opens a receipt.
If you scan an unknown barcode, the app offers to create a new item with it, so you can reuse manufacturers' tags.

## Where data is stored
The first time the app runs (or the installer asks), you choose one of these:

| Option | Use when |
|---|---|
| **On this computer** | One shop. The installer sets up a private MySQL 8.4 database (Windows service `BoutiquePOS-MySQL`, port 3307, local-only) with a nightly backup to `%ProgramData%\BoutiquePOS\Backups`. |
| **Cloud - our web server (e.g. Forge)** | Several registers or branches share stock and sales over HTTPS. Enter the site address; staff sign in with their own accounts, which the server checks. Create the first Admin on the server with `php artisan pos:admin <username>`. No database port is exposed. See `web/docs/API.md`. |
| **MySQL server (direct connection)** | You already run MySQL 8 on this PC or the shop network, or online with SSL REQUIRED and access limited to the shop's IP. |
| **Offline file** | One register, no database server. You can move the data into a database later; the wizard offers to copy it. |

Change it any time in **Settings & Scanner > Change database connection**. Connection details are kept in
`%ProgramData%\BoutiquePOS\db.config`, with the password encrypted by Windows (DPAPI).

## Staff accounts and roles
Everyone signs in with their own username and password, in every storage mode.
- **First run (this computer, MySQL or offline file):** the app asks you to create the **Admin** account.
  **Web server:** create it on the server with `php artisan pos:admin <username>`.
- **Users & roles** tab (Admins only): add staff, reset passwords, disable accounts, and create or edit roles by ticking what each one may do.
  Built-in roles are Admin (everything), Manager (everything except users), Cashier (sell, labels) and Stock clerk (inventory, stock, labels).
- Pages and buttons a role can't use are hidden or greyed out. The data layer and the web server refuse the action too.
- Discounts and price changes need the "Discounts and price changes" permission. Receipts and Sales history show the cashier.
- **Sign out** (top right) ends the session for a cashier change. One active Admin always remains.

## Building the installer (for selling)
```powershell
winget install JRSoftware.InnoSetup      # once
.\build-installer.ps1                    # -> dist\BoutiquePOS-Setup-1.1.0.exe
```
The first build downloads the prerequisites (about 400 MB) into `%LOCALAPPDATA%\BoutiquePOS-build\redist`:
.NET Framework 4.8, the VC++ runtime, MySQL Connector/ODBC 8.4 and the MySQL 8.4 LTS server.
The setup program installs only what the customer's PC is missing.

Before selling:
- **MySQL licence:** MySQL Community Server and Connector/ODBC are GPL. Bundling them with a closed-source
  product you sell may need a commercial (OEM) licence from Oracle, so get advice. Alternatives are to sell
  cloud-hosted setups or to have the customer download MySQL themselves.
- **Code signing:** sign `BoutiquePOS.exe` and the setup file with a code-signing certificate, or Windows
  SmartScreen will warn customers.
- **EULA:** add your licence agreement (`LicenseFile=` in `installer\BoutiquePOS.iss`).

## CSV import columns
`Barcode, Name, Category, Brand, Size, Color, Price, Cost, Quantity, ReorderLevel, Notes`.
Only **Name** is required. Rows with a barcode that already exists update that item, and rows without a barcode get one generated.
