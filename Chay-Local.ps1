$ErrorActionPreference = 'Stop'
# Optional local private credentials (excluded from ZIP/git). Do not commit secrets.
$r2Config = Join-Path $PSScriptRoot 'R2-Local.ps1'
if (Test-Path $r2Config) {
    . $r2Config
    Write-Host 'Da tai cau hinh R2 tu R2-Local.ps1' -ForegroundColor Cyan
}
Set-Location (Join-Path $PSScriptRoot 'LangKinhHuKhong.Web')
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host 'CHUA CAI .NET 9 SDK: https://dotnet.microsoft.com/download/dotnet/9.0' -ForegroundColor Red
    exit 1
}
Write-Host '=== LANG KINH HU KHONG - BLAZOR SERVER .NET 9 ===' -ForegroundColor Cyan
dotnet --version
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
if (-not (Get-Command ffmpeg -ErrorAction SilentlyContinue) -or -not (Get-Command ffprobe -ErrorAction SilentlyContinue)) {
    Write-Host 'FFmpeg/ffprobe chua co trong PATH. Upload MP3 se khong hoat dong cho den khi cai dat.' -ForegroundColor Yellow
}
# Development seed uses embedded PBKDF2 hash. No password is printed or stored as plaintext.
# To customize first-run credentials, set BOTH LKH_ADMIN_USERNAME and LKH_ADMIN_PASSWORD before running this script.
$env:ASPNETCORE_ENVIRONMENT = 'Development'
Write-Host 'Development: Admin tu khoi tao khi database chua co Admin. Khong ghi de Admin da ton tai.' -ForegroundColor Cyan
try {
    dotnet restore
    if ($LASTEXITCODE -ne 0) { throw 'dotnet restore that bai' }
    dotnet build --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'dotnet build that bai' }
    Write-Host 'Mo web: http://127.0.0.1:5110' -ForegroundColor Green
    dotnet run --no-build --no-restore --urls 'http://127.0.0.1:5110'
} finally {
    Remove-Item Env:LKH_ADMIN_USERNAME -ErrorAction SilentlyContinue
    Remove-Item Env:LKH_ADMIN_PASSWORD -ErrorAction SilentlyContinue
}
