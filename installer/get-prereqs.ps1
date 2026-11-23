# Downloads the redistributables the installer bundles. Run once (or for newer versions). ~400 MB.
# They go to %LOCALAPPDATA%\BoutiquePOS-build\redist by default so they don't sync to OneDrive.
param(
  [string]$MySqlVersion = '8.4.3',      # MySQL Community Server LTS (noinstall ZIP)
  [string]$OdbcVersion  = '8.4.0',      # MySQL Connector/ODBC (64-bit MSI)
  [string]$Redist = (Join-Path $env:LOCALAPPDATA 'BoutiquePOS-build\redist'),
  [switch]$Force
)
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'   # makes Invoke-WebRequest much faster
$redist = $Redist
New-Item -ItemType Directory -Force $redist | Out-Null

$mySeries = ($MySqlVersion -split '\.')[0..1] -join '.'
$odbcSeries = ($OdbcVersion -split '\.')[0..1] -join '.'
$items = @(
  @{ Name = 'ndp48-x86-x64-allos-enu.exe'; Url = 'https://go.microsoft.com/fwlink/?linkid=2088631' },
  @{ Name = 'vc_redist.x64.exe';           Url = 'https://aka.ms/vs/17/release/vc_redist.x64.exe' },
  @{ Name = 'mysql-connector-odbc.msi';    Url = "https://cdn.mysql.com/archives/mysql-connector-odbc-$odbcSeries/mysql-connector-odbc-$OdbcVersion-winx64.msi" },
  @{ Name = "mysql-$MySqlVersion-winx64.zip"; Url = "https://cdn.mysql.com/archives/mysql-$mySeries/mysql-$MySqlVersion-winx64.zip" }
)

foreach ($i in $items) {
  $dest = Join-Path $redist $i.Name
  if ((Test-Path $dest) -and -not $Force) { Write-Host "Already have $($i.Name)"; continue }
  Write-Host "Downloading $($i.Name) ..."
  Invoke-WebRequest -Uri $i.Url -OutFile $dest -UseBasicParsing -UserAgent 'Mozilla/5.0'
  $sig = Get-AuthenticodeSignature $dest
  if ($dest -notlike '*.zip' -and $sig.Status -ne 'Valid') { Remove-Item $dest; throw "$($i.Name) is not validly signed ($($sig.Status)) - download removed." }
}

# Unpack MySQL into redist\mysql and drop what a POS never needs (debug symbols, headers, test suite).
$zip = Join-Path $redist "mysql-$MySqlVersion-winx64.zip"
$target = Join-Path $redist 'mysql'
if ($Force -or -not (Test-Path (Join-Path $target 'bin\mysqld.exe'))) {
  if (Test-Path $target) { Remove-Item $target -Recurse -Force }
  $tmp = Join-Path $redist 'mysql-unzip'
  if (Test-Path $tmp) { Remove-Item $tmp -Recurse -Force }
  Write-Host 'Extracting MySQL ...'
  Expand-Archive $zip $tmp
  Move-Item (Get-ChildItem $tmp -Directory | Select-Object -First 1).FullName $target
  Remove-Item $tmp -Recurse -Force
  Get-ChildItem $target -Recurse -Include *.pdb, *.lib | Remove-Item -Force
  foreach ($d in 'include', 'docs', 'mysql-test', 'lib\mecab') { $p = Join-Path $target $d; if (Test-Path $p) { Remove-Item $p -Recurse -Force } }
  Get-ChildItem (Join-Path $target 'bin') -Filter '*test*' | Remove-Item -Force
}
$size = (Get-ChildItem $target -Recurse | Measure-Object Length -Sum).Sum / 1MB
Write-Host ("Prerequisites ready in {0} (MySQL files: {1:N0} MB)" -f $redist, $size) -ForegroundColor Green
