# Builds dist\BoutiquePOS-Setup-<version>.exe
#   1. compiles the app (build.ps1)
#   2. downloads prerequisites into installer\redist (first time only)
#   3. compiles installer\BoutiquePOS.iss with Inno Setup 6
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path

& (Join-Path $root 'build.ps1')

$redist = Join-Path $env:LOCALAPPDATA 'BoutiquePOS-build\redist'   # outside OneDrive on purpose
if (-not (Test-Path (Join-Path $redist 'mysql\bin\mysqld.exe')) -or -not (Test-Path (Join-Path $redist 'mysql-connector-odbc.msi'))) {
  & (Join-Path $root 'installer\get-prereqs.ps1') -Redist $redist
}

$iscc = @(
  "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
  "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
  "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) {
  throw "Inno Setup 6 is not installed. Install it with:  winget install JRSoftware.InnoSetup   then run this script again."
}

& $iscc "/DRedist=$redist" (Join-Path $root 'installer\BoutiquePOS.iss')
if ($LASTEXITCODE -ne 0) { throw "Installer build failed ($LASTEXITCODE)" }
Get-ChildItem (Join-Path $root 'dist') -Filter *.exe | Sort-Object LastWriteTime -Descending | Select-Object -First 1 |
  ForEach-Object { Write-Host ("Installer ready: {0} ({1:N0} MB)" -f $_.FullName, ($_.Length / 1MB)) -ForegroundColor Green }
