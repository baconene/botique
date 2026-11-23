# Builds Boutique POS with the C# compiler that ships with Windows (.NET Framework 4.8). No SDK needed.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$csc  = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (-not (Test-Path $csc)) { throw '.NET Framework 4.x compiler not found.' }

$bin = Join-Path $root 'bin'
New-Item -ItemType Directory -Force $bin | Out-Null
$src = Get-ChildItem (Join-Path $root 'src') -Filter *.cs | ForEach-Object { $_.FullName }

$cscArgs = @(
  '/nologo', '/codepage:65001', '/target:winexe', '/optimize+', '/platform:anycpu',
  "/out:$(Join-Path $bin 'BoutiquePOS.exe')",
  '/r:System.dll', '/r:System.Core.dll', '/r:System.Drawing.dll',
  '/r:System.Windows.Forms.dll', '/r:System.Xml.dll', '/r:System.Data.dll', '/r:System.Security.dll', '/r:System.Web.Extensions.dll'
)
$manifest = Join-Path $root 'src\app.manifest'
if (Test-Path $manifest) { $cscArgs += "/win32manifest:$manifest" }
$icon = Join-Path $root 'src\app.ico'
if (Test-Path $icon) { $cscArgs += "/win32icon:$icon" }

& $csc @cscArgs @src
if ($LASTEXITCODE -ne 0) { throw "Build failed ($LASTEXITCODE)" }
# Developer builds keep their data in bin\Data. The installer does NOT ship this file,
# so installed copies use %ProgramData%\BoutiquePOS.
Set-Content (Join-Path $bin 'portable.txt') 'Developer build: data is stored in the Data folder next to the exe.'
Write-Host "Built $(Join-Path $bin 'BoutiquePOS.exe')" -ForegroundColor Green
