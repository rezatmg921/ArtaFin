[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($env:ConnectionStrings__ArtaFin)) {
    throw "Set ConnectionStrings__ArtaFin in the server's protected environment."
}

if ([string]::IsNullOrWhiteSpace($env:BootstrapAdmin__Email) -or
    [string]::IsNullOrEmpty($env:BootstrapAdmin__Password)) {
    throw "Set BootstrapAdmin__Email and BootstrapAdmin__Password in the server's protected environment."
}

dotnet tool restore
if ($LASTEXITCODE -ne 0) { throw "dotnet tool restore failed." }

dotnet ef database update `
    --project src/ArtaFin.Infrastructure `
    --startup-project src/ArtaFin.Api `
    --context ArtaFinDbContext
if ($LASTEXITCODE -ne 0) { throw "Database migration failed." }

dotnet run --project src/ArtaFin.Api -- --seed-initial-data
if ($LASTEXITCODE -ne 0) { throw "Initial data seeding failed." }

Write-Host "ArtaFin database schema and initial data are ready."
