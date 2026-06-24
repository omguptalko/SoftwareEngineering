<#
  HIS — migration + seed runner
  Applies db/migrations/*.sql then db/seed/*.sql (lexical order) to the target DB.
  Scripts are idempotent, so re-running is safe.

  Usage:
    pwsh db/run-migrations.ps1                         # uses (localdb)\MSSQLLocalDB, DB "HIS"
    pwsh db/run-migrations.ps1 -Server "myserver" -Database "HIS"
#>
param(
    [string]$Server   = "(localdb)\MSSQLLocalDB",
    [string]$Database = "HIS"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path

Write-Host "Ensuring database [$Database] exists on [$Server] ..."
sqlcmd -S "$Server" -d "master" -b -Q "IF DB_ID('$Database') IS NULL CREATE DATABASE [$Database];"

function Invoke-SqlFolder($folder) {
    Get-ChildItem -Path $folder -Filter *.sql | Sort-Object Name | ForEach-Object {
        Write-Host "  -> $($_.Name)"
        sqlcmd -S "$Server" -d "$Database" -b -i "$($_.FullName)"
        if ($LASTEXITCODE -ne 0) { throw "Failed on $($_.Name)" }
    }
}

Write-Host "Applying migrations ..."
Invoke-SqlFolder (Join-Path $root "migrations")

Write-Host "Applying seed data ..."
Invoke-SqlFolder (Join-Path $root "seed")

Write-Host "Done."
