# Cài SIS_WORKER thành Windows Service. Chạy bằng PowerShell "Run as Administrator".
# Cách dùng:
#   .\install-service.ps1                  # publish + cài + khởi động
#   .\install-service.ps1 -InstallDir D:\SIS_WORKER
param(
    [string]$InstallDir = "D:\SIS_WORKER",
    [string]$ServiceName = "SIS_WORKER"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $root "SIS_WORKER\SIS_WORKER.csproj"

Write-Host "==> Publish ra $InstallDir"
if (Get-Service $ServiceName -ErrorAction SilentlyContinue) {
    Write-Host "==> Dừng service cũ"
    Stop-Service $ServiceName -Force
    Start-Sleep -Seconds 2
}
dotnet publish $proj -c Release -r win-x64 --self-contained false -o $InstallDir

# appsettings.local.json (secret) không nằm trong publish nếu thiếu → nhắc người dùng
if (-not (Test-Path (Join-Path $InstallDir "appsettings.local.json"))) {
    Write-Warning "Thiếu $InstallDir\appsettings.local.json (Portal:Username/Password, SisApi:ClientSecret). Hãy tạo trước khi chạy."
}

$exe = Join-Path $InstallDir "SIS_WORKER.exe"
if (-not (Get-Service $ServiceName -ErrorAction SilentlyContinue)) {
    Write-Host "==> Tạo service $ServiceName"
    New-Service -Name $ServiceName -BinaryPathName "`"$exe`"" `
        -DisplayName "SIS Worker - Tự động kích hoạt bệnh nhân" `
        -Description "Quét API SIS và kích hoạt tài khoản bệnh nhân trên portal (HTTP, không cần trình duyệt)." `
        -StartupType Automatic | Out-Null
    # Tự khởi động lại khi lỗi (sau 1 phút, tối đa 3 lần liên tiếp)
    sc.exe failure $ServiceName reset= 86400 actions= restart/60000/restart/60000/restart/60000 | Out-Null
}

Write-Host "==> Khởi động service (nhớ đặt Worker:DryRun=false trong appsettings khi chạy thật)"
Start-Service $ServiceName
Get-Service $ServiceName
Write-Host "Log: $InstallDir\logs\"
