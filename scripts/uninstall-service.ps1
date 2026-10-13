# Gỡ Windows Service SIS_WORKER. Chạy bằng PowerShell "Run as Administrator".
param([string]$ServiceName = "SIS_WORKER")

if (Get-Service $ServiceName -ErrorAction SilentlyContinue) {
    Stop-Service $ServiceName -Force -ErrorAction SilentlyContinue
    sc.exe delete $ServiceName | Out-Null
    Write-Host "Đã gỡ service $ServiceName"
} else {
    Write-Host "Service $ServiceName không tồn tại."
}
