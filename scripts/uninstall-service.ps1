# Go Windows Service SIS_WORKER. Chay bang PowerShell "Run as Administrator".
param([string]$ServiceName = "SIS_WORKER")

if (Get-Service $ServiceName -ErrorAction SilentlyContinue) {
    Write-Host "Dang dung service $ServiceName..."
    Stop-Service $ServiceName -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
    sc.exe delete $ServiceName | Out-Null
    Write-Host "Da go service $ServiceName thanh cong."
} else {
    Write-Host "Service $ServiceName khong ton tai."
}
