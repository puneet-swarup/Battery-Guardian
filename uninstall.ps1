$ErrorActionPreference = 'SilentlyContinue'

Write-Host "Uninstalling Battery Guardian..." -ForegroundColor Cyan

$installDir = Join-Path $env:LOCALAPPDATA 'Programs\BatteryGuardian'

# 1. Stop the running app
Get-Process -Name 'BatteryGuardian' -ErrorAction SilentlyContinue | Stop-Process -Force

# 2. Remove Start Menu shortcut
$shortcutPath = Join-Path ([Environment]::GetFolderPath('Programs')) 'Battery Guardian.lnk'
if (Test-Path $shortcutPath) { Remove-Item $shortcutPath -Force }

# 3. Remove the "Installed apps" registry entry
Remove-Item -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\BatteryGuardian' -Recurse -Force

# 4. Remove auto-start entry (the app adds this on first run)
Remove-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'BatteryGuardian' -ErrorAction SilentlyContinue

# 5. Delete the install directory from a detached process (so it survives our own exit)
$cleanup = "timeout /t 2 /nobreak >nul & rmdir /s /q `"$installDir`""
Start-Process cmd -ArgumentList '/c', $cleanup -WindowStyle Hidden

Write-Host ""
Write-Host "Battery Guardian uninstalled successfully." -ForegroundColor Green
Write-Host ""