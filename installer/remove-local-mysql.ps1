# Run by the uninstaller: stops and removes the private MySQL service and backup task.
# The customer's data (%ProgramData%\BoutiquePOS) is deliberately KEPT.
param([string]$AppDir)
$ErrorActionPreference = 'Continue'
$Service = 'BoutiquePOS-MySQL'
Unregister-ScheduledTask -TaskName 'BoutiquePOS MySQL backup' -Confirm:$false -ErrorAction SilentlyContinue
$svc = Get-Service $Service -ErrorAction SilentlyContinue
if ($svc) {
  if ($svc.Status -ne 'Stopped') { Stop-Service $Service -Force -ErrorAction SilentlyContinue }
  $mysqld = Join-Path $AppDir 'mysql\bin\mysqld.exe'
  if (Test-Path $mysqld) { & $mysqld --remove $Service | Out-Null } else { & sc.exe delete $Service | Out-Null }
}
exit 0
