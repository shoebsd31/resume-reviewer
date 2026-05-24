#!/usr/bin/env pwsh
# Reset script: drops the ResumeReviewDb, regenerates sample resumes, and re-seeds.
# Usage:  pwsh ./reset.ps1  [-SqlInstance localhost\SQLEXPRESS] [-Count 18] [-Seed 42]
# Stop the API process before running so the DB drop isn't blocked by open connections.

[CmdletBinding()]
param(
    [string]$SqlInstance = 'localhost\SQLEXPRESS',
    [string]$Database = 'ResumeReviewDb',
    [int]$Count = 18,
    [int]$Seed = 42
)

$ErrorActionPreference = 'Stop'
$repoRoot = $PSScriptRoot

Write-Host "==> Dropping database [$Database] on $SqlInstance" -ForegroundColor Cyan
$dropSql = @"
IF DB_ID(N'$Database') IS NOT NULL
BEGIN
    ALTER DATABASE [$Database] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [$Database];
END
"@
& sqlcmd -S $SqlInstance -E -b -Q $dropSql
if ($LASTEXITCODE -ne 0) { throw "sqlcmd failed dropping $Database (is the API still running and holding connections?)" }

$outDir = Join-Path $repoRoot 'samples-resumes/output'
Write-Host "==> Clearing generated resumes in $outDir" -ForegroundColor Cyan
if (Test-Path $outDir) {
    Get-ChildItem -Path $outDir -File -Force | Remove-Item -Force
} else {
    New-Item -ItemType Directory -Path $outDir | Out-Null
}

Write-Host "==> Regenerating $Count sample resumes (seed=$Seed)" -ForegroundColor Cyan
$generatorDir = Join-Path $repoRoot 'samples-resumes/generator'
$templatePath = Join-Path $repoRoot 'template/resumetemplate.dotx'
Push-Location $generatorDir
try {
    & dotnet run -- --template $templatePath --out $outDir --count $Count --seed $Seed
    if ($LASTEXITCODE -ne 0) { throw "Sample generator failed" }
} finally {
    Pop-Location
}

Write-Host "==> Seeding API (re-creates DB schema, ingests all .docx)" -ForegroundColor Cyan
$apiDir = Join-Path $repoRoot 'api'
Push-Location $apiDir
try {
    & dotnet run --project ResumeReview.Api -- seed --path $outDir
    if ($LASTEXITCODE -ne 0) { throw "Seed command failed" }
} finally {
    Pop-Location
}

Write-Host "`n==> Done. Start the API + UI:" -ForegroundColor Green
Write-Host "    cd api;  dotnet run --project ResumeReview.Api"
Write-Host "    cd web;  npm start"
