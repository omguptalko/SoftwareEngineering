<#
  HIS — control-plane (HIS_Platform) migration + seed runner
  Applies db/platform/P*.sql (lexical order) to the platform database.
  Scripts are idempotent, so re-running is safe.

  Usage:
    pwsh db/platform/run-platform-migrations.ps1                       # (localdb)\MSSQLLocalDB, DB "HIS_Platform"
    pwsh db/platform/run-platform-migrations.ps1 -Server "myserver" -Database "HIS_Platform"
#>
param(
    [string]$Server   = "(localdb)\MSSQLLocalDB",
    [string]$Database = "HIS_Platform"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path

Write-Host "Ensuring control-plane database [$Database] exists on [$Server] ..."
sqlcmd -S "$Server" -d "master" -b -Q "IF DB_ID('$Database') IS NULL CREATE DATABASE [$Database];"

Write-Host "Applying control-plane migrations + seed ..."
Get-ChildItem -Path $root -Filter "P*.sql" | Sort-Object Name | ForEach-Object {
    Write-Host "  -> $($_.Name)"
    sqlcmd -S "$Server" -d "$Database" -b -i "$($_.FullName)"
    if ($LASTEXITCODE -ne 0) { throw "Failed on $($_.Name)" }
}

Write-Host "Done."
