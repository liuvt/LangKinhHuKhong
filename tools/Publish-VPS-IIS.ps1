# Lang Kinh Hu Khong - Publish Windows IIS (V1.9.4.1)
# Run on a Windows computer with .NET 9 SDK.
# Produces a framework-dependent, win-x64 release. Does not deploy or delete data.
param(
    [string]$OutputDir = ""
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $projectRoot 'LangKinhHuKhong.Web\LangKinhHuKhong.Web.csproj'

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path $projectRoot 'publish\lkh-web'
}

Write-Host '=== Lang Kinh Hu Khong - Windows IIS Publish ===' -ForegroundColor Cyan
Write-Host "Project : $project"
Write-Host "Output  : $OutputDir"

if (-not (Test-Path -LiteralPath $project)) {
    throw "Project file not found: $project"
}
if (-not (Get-Command 'dotnet' -ErrorAction SilentlyContinue)) {
    throw '.NET 9 SDK was not found. Install SDK 9 first.'
}

Write-Host '[1/3] Restoring NuGet packages for win-x64...' -ForegroundColor Cyan
& dotnet restore $project --runtime win-x64
if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed (exit code $LASTEXITCODE)" }

Write-Host '[2/3] Publishing Release for win-x64...' -ForegroundColor Cyan
& dotnet publish $project --configuration Release --runtime win-x64 --self-contained false --no-restore --output $OutputDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed (exit code $LASTEXITCODE)" }

Write-Host '[3/3] Checking publish output...' -ForegroundColor Cyan
$dll = Join-Path $OutputDir 'LangKinhHuKhong.Web.dll'
$webConfig = Join-Path $OutputDir 'web.config'
if (-not (Test-Path -LiteralPath $dll)) { throw "Missing DLL after publish: $dll" }
if (-not (Test-Path -LiteralPath $webConfig)) { throw "Missing IIS web.config: $webConfig" }

Write-Host ('PUBLISH SUCCESS: ' + (Resolve-Path -LiteralPath $OutputDir).Path) -ForegroundColor Green
Write-Host 'Copy the CONTENTS of the publish folder to the IIS website folder.'
Write-Host 'Do NOT overwrite the existing production database, secrets or R2 credentials.'
