# Cai SIS_WORKER thanh Windows Service. Chay bang PowerShell "Run as Administrator".
param(
    [string]$InstallDir = "D:\SIS_WORKER",
    [string]$ServiceName = "SIS_WORKER"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $root "SIS_WORKER\SIS_WORKER.csproj"

Write-Host "==> Publish ra $InstallDir"
if (Get-Service $ServiceName -ErrorAction SilentlyContinue) {
    Write-Host "==> Dung service cu"
    Stop-Service $ServiceName -Force
    Start-Sleep -Seconds 2
}
dotnet publish $proj -c Release -r win-x64 --self-contained false -o $InstallDir

# appsettings.local.json (secret) khong nam trong publish neu thieu -> nhac nguoi dung
if (-not (Test-Path (Join-Path $InstallDir "appsettings.local.json"))) {
    Write-Warning "Thieu $InstallDir\appsettings.local.json (Portal:Username/Password, SisApi:ClientSecret). Hay tao truoc khi chay."
}

$exe = Join-Path $InstallDir "SIS_WORKER.exe"
if (-not (Get-Service $ServiceName -ErrorAction SilentlyContinue)) {
    Write-Host "==> Tao service $ServiceName"
    New-Service -Name $ServiceName -BinaryPathName "`"$exe`"" `
        -DisplayName "SIS Patient Activation Worker" `
        -Description "Automatically activates patient portal accounts via HTTP and sends notification emails." `
        -StartupType Automatic | Out-Null
    # Tu khoi dong lai khi loi (sau 1 phut, toi da 3 lan lien tiep)
    sc.exe failure $ServiceName reset= 86400 actions= restart/60000/restart/60000/restart/60000 | Out-Null
} else {
    # Cap nhat thong tin hien thi neu service da ton tai
    sc.exe config $ServiceName DisplayName= "SIS Patient Activation Worker" | Out-Null
    sc.exe description $ServiceName "Automatically activates patient portal accounts via HTTP and sends notification emails." | Out-Null
}

Write-Host "==> Khoi dong service"
Start-Service $ServiceName
Get-Service $ServiceName
Write-Host "Log: $InstallDir\logs\"
