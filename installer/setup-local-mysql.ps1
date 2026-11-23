# Creates (or repairs) the private MySQL instance used by Boutique POS "On this computer" mode.
# Run elevated by the installer. Safe to run again: an existing instance and its data are kept.
param(
  [Parameter(Mandatory = $true)][string]$AppDir,
  [string]$DataDir = (Join-Path $env:ProgramData 'BoutiquePOS')
)
$ErrorActionPreference = 'Stop'
$Service  = 'BoutiquePOS-MySQL'
$Database = 'boutique_pos'
$AppUser  = 'boutique'

New-Item -ItemType Directory -Force $DataDir | Out-Null
$log = Join-Path $DataDir 'mysql-setup.log'
function Log([string]$m) { Add-Content -Path $log -Value ("{0}  {1}" -f (Get-Date -Format s), $m) }

$base     = Join-Path $AppDir 'mysql'
$mysqld   = Join-Path $base 'bin\mysqld.exe'
$mysql    = Join-Path $base 'bin\mysql.exe'
$mysqldump = Join-Path $base 'bin\mysqldump.exe'
$dataPath = Join-Path $DataDir 'mysql-data'
$ini      = Join-Path $DataDir 'my.ini'
$cfgPath  = Join-Path $DataDir 'db.config'
$rootCnf  = Join-Path $DataDir 'mysql-root.cnf'      # admin-only: root password for maintenance/backups
$utf8NoBom = New-Object System.Text.UTF8Encoding $false

function New-Secret {
  $b = New-Object byte[] 18
  [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($b)
  ([Convert]::ToBase64String($b) -replace '[+/=]', 'x')
}

# Must match DbConfig.Password in src\DbConfig.cs
function Protect-Secret([string]$s) {
  Add-Type -AssemblyName System.Security
  $entropy = [Text.Encoding]::UTF8.GetBytes('BoutiquePOS.db.v1')
  $raw = [Security.Cryptography.ProtectedData]::Protect([Text.Encoding]::UTF8.GetBytes($s), $entropy, 'LocalMachine')
  [Convert]::ToBase64String($raw)
}

function Lock-AdminOnly([string]$path) {
  # Administrators + SYSTEM only (SIDs so it works on non-English Windows)
  & icacls $path /inheritance:r /grant:r '*S-1-5-32-544:F' '*S-1-5-18:F' | Out-Null
}

function Test-PortFree([int]$p) {
  -not (Get-NetTCPConnection -LocalPort $p -State Listen -ErrorAction SilentlyContinue)
}

function Wait-Port([int]$p) {
  for ($i = 0; $i -lt 60; $i++) {
    if (-not (Test-PortFree $p)) { return }
    Start-Sleep -Seconds 1
  }
  throw "MySQL did not start listening on port $p (see $DataDir\mysql-error.log)"
}

function Invoke-Sql([int]$port, [string]$sql, [string]$cnf) {
  $a = @()   # (not $args - that is a reserved PowerShell variable)
  if ($cnf) { $a += "--defaults-extra-file=$cnf" } else { $a += '-uroot', '--skip-password' }
  $a += '--protocol=TCP', '-h', '127.0.0.1', '-P', "$port", '-e', $sql
  $out = & $mysql @a 2>&1
  if ($LASTEXITCODE -ne 0) { throw "mysql failed: $out" }
}

function Write-DbConfig([int]$port, [string]$password) {
  $xml = @"
<?xml version="1.0" encoding="utf-8"?>
<DbConfig xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema">
  <Mode>MySql</Mode>
  <Location>Local</Location>
  <Host>localhost</Host>
  <Port>$port</Port>
  <Database>$Database</Database>
  <User>$AppUser</User>
  <PasswordProtected>$(Protect-Secret $password)</PasswordProtected>
  <SslMode>PREFERRED</SslMode>
  <Driver />
</DbConfig>
"@
  [IO.File]::WriteAllText($cfgPath, $xml, $utf8NoBom)
}

function Write-RootCnf([int]$port, [string]$password) {
  [IO.File]::WriteAllText($rootCnf, "[client]`r`nuser=root`r`npassword=$password`r`nport=$port`r`nhost=127.0.0.1`r`n", $utf8NoBom)
  Lock-AdminOnly $rootCnf
}

function Register-Backup([int]$port) {
  $script = Join-Path $DataDir 'mysql-backup.ps1'
  $backups = Join-Path $DataDir 'Backups'
  $body = @"
`$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force '$backups' | Out-Null
`$file = Join-Path '$backups' ('mysql-$Database-' + (Get-Date -Format 'yyyyMMdd-HHmm') + '.sql')
& '$mysqldump' --defaults-extra-file='$rootCnf' --single-transaction --routines --no-tablespaces --databases $Database --result-file="`$file"
Get-ChildItem '$backups' -Filter 'mysql-$Database-*.sql' | Sort-Object Name -Descending | Select-Object -Skip 30 | Remove-Item -Force
"@
  [IO.File]::WriteAllText($script, $body, $utf8NoBom)
  Lock-AdminOnly $script
  $action  = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument "-NoProfile -ExecutionPolicy Bypass -File `"$script`""
  $trigger = New-ScheduledTaskTrigger -Daily -At 11:30pm
  $settings = New-ScheduledTaskSettingsSet -StartWhenAvailable -DontStopIfGoingOnBatteries -AllowStartIfOnBatteries
  Register-ScheduledTask -TaskName 'BoutiquePOS MySQL backup' -Action $action -Trigger $trigger -Settings $settings `
    -User 'SYSTEM' -RunLevel Highest -Force | Out-Null
}

try {
  Log "Setup started. AppDir=$AppDir DataDir=$DataDir"
  if (-not (Test-Path $mysqld)) { throw "MySQL files not found in $base" }

  $svc = Get-Service $Service -ErrorAction SilentlyContinue
  if (-not $svc) {
    $port = 3307
    while (-not (Test-PortFree $port)) { $port++; if ($port -gt 3330) { throw 'No free TCP port between 3307 and 3330' } }
    $b = $base -replace '\\', '/'; $d = $dataPath -replace '\\', '/'; $e = (Join-Path $DataDir 'mysql-error.log') -replace '\\', '/'
    $iniText = @"
[mysqld]
basedir="$b"
datadir="$d"
port=$port
bind-address=127.0.0.1
mysqlx=OFF
character-set-server=utf8mb4
max_connections=100
innodb_buffer_pool_size=256M
log-error="$e"

[client]
port=$port
"@
    [IO.File]::WriteAllText($ini, $iniText, $utf8NoBom)   # MySQL cannot read a BOM

    if (-not (Test-Path (Join-Path $dataPath 'mysql'))) {
      Log 'Initializing data directory'
      $out = & $mysqld "--defaults-file=$ini" --initialize-insecure --console 2>&1
      if ($LASTEXITCODE -ne 0) { throw "mysqld --initialize failed: $out" }
    }
    Log "Installing service $Service on port $port"
    $out = & $mysqld --install $Service "--defaults-file=$ini" 2>&1
    if ($LASTEXITCODE -ne 0) { throw "Service install failed: $out" }
    Set-Service $Service -StartupType Automatic
    Start-Service $Service
    Wait-Port $port

    $rootPw = New-Secret; $appPw = New-Secret
    Invoke-Sql $port ("ALTER USER 'root'@'localhost' IDENTIFIED BY '$rootPw';") $null
    Write-RootCnf $port $rootPw
    Invoke-Sql $port (
      "CREATE DATABASE IF NOT EXISTS $Database CHARACTER SET utf8mb4;" +
      "CREATE USER IF NOT EXISTS '$AppUser'@'localhost' IDENTIFIED BY '$appPw';" +
      "CREATE USER IF NOT EXISTS '$AppUser'@'127.0.0.1' IDENTIFIED BY '$appPw';" +
      "GRANT ALL PRIVILEGES ON $Database.* TO '$AppUser'@'localhost';" +
      "GRANT ALL PRIVILEGES ON $Database.* TO '$AppUser'@'127.0.0.1';") $rootCnf
    Write-DbConfig $port $appPw
    Log 'Database and user created'
  }
  else {
    # Upgrade / repair: keep the existing instance and data.
    if ($svc.Status -ne 'Running') { Start-Service $Service }
    $port = [int](((Get-Content $ini) | Where-Object { $_ -match '^port=' } | Select-Object -First 1) -replace 'port=', '')
    Wait-Port $port
    if (-not (Test-Path $cfgPath) -and (Test-Path $rootCnf)) {
      $appPw = New-Secret
      Invoke-Sql $port ("ALTER USER '$AppUser'@'localhost' IDENTIFIED BY '$appPw'; ALTER USER '$AppUser'@'127.0.0.1' IDENTIFIED BY '$appPw';") $rootCnf
      Write-DbConfig $port $appPw
      Log 'db.config was missing - app password reset'
    }
    Log "Existing instance kept (port $port)"
  }
  Register-Backup $port
  Log 'Nightly backup task registered'
  Log 'Setup finished OK'
  exit 0
}
catch {
  Log ("ERROR: " + $_.Exception.Message)
  exit 1   # the app will show its setup wizard so the user can still choose another option
}
