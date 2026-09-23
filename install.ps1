$ErrorActionPreference = 'Stop'

Write-Host "=== Battery Guardian Installer ===" -ForegroundColor Cyan
Write-Host ""

$installDir = Join-Path $env:LOCALAPPDATA 'Programs\BatteryGuardian'
$sourceDir  = Split-Path -Parent $MyInvocation.MyCommand.Path

Write-Host "[1] Source dir : $sourceDir"
Write-Host "[2] Install dir: $installDir"
Write-Host ""

# Create install directory
if (-not (Test-Path $installDir)) {
    New-Item -ItemType Directory -Path $installDir -Force | Out-Null
}
Write-Host "[3] Install dir created: $(Test-Path $installDir)"

# Copy app files
Write-Host "[4] Copying files..."
$files = Get-ChildItem -Path $sourceDir -Exclude 'install.bat','install.ps1','uninstall.bat','uninstall.ps1'
Write-Host "    Found $($files.Count) files to copy."
$files | ForEach-Object {
    Copy-Item -Path $_.FullName -Destination $installDir -Recurse -Force
}
Write-Host "    Files in install dir: $((Get-ChildItem $installDir -Recurse).Count)"
Write-Host ""

# Shortcut creation
$exePath = Join-Path $installDir 'BatteryGuardian.exe'
$programsFolder = [Environment]::GetFolderPath('Programs')
$shortcutPath = Join-Path $programsFolder 'Battery Guardian.lnk'

Write-Host "[5] Shortcut target  : $exePath"
Write-Host "    Shortcut location: $shortcutPath"
Write-Host "    Programs folder exists: $(Test-Path $programsFolder)"
Write-Host "    Target exe exists: $(Test-Path $exePath)"
Write-Host ""

Write-Host "[6] Creating shortcut..."
try {
    $ws = New-Object -ComObject WScript.Shell
    $sc = $ws.CreateShortcut($shortcutPath)
    $sc.TargetPath       = $exePath
    $sc.WorkingDirectory = $installDir
    $sc.Description      = 'Monitor and protect your laptop battery health'
    $sc.IconLocation     = "$exePath,0"
    $sc.Save()

    Write-Host "    Save() completed." -ForegroundColor Green
    Write-Host "    Shortcut file exists: $(Test-Path $shortcutPath)" -ForegroundColor Green
} catch {
    Write-Host "    ERROR creating shortcut:" -ForegroundColor Red
    Write-Host "    $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "    Full error: $_" -ForegroundColor Red
}
Write-Host ""

# Registry
$uninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\BatteryGuardian'
New-Item -Path $uninstallKey -Force | Out-Null
Set-ItemProperty -Path $uninstallKey -Name 'DisplayName'     -Value 'Battery Guardian'
Set-ItemProperty -Path $uninstallKey -Name 'DisplayVersion'  -Value '1.7.1'
Set-ItemProperty -Path $uninstallKey -Name 'Publisher'       -Value 'Puneet Swarup'
Set-ItemProperty -Path $uninstallKey -Name 'InstallLocation' -Value $installDir
Set-ItemProperty -Path $uninstallKey -Name 'NoModify'        -Value 1 -Type DWord
Set-ItemProperty -Path $uninstallKey -Name 'NoRepair'        -Value 1 -Type DWord
Set-ItemProperty -Path $uninstallKey -Name 'UninstallString' -Value ('"' + (Join-Path $installDir 'uninstall.bat') + '"')
Write-Host "[7] Registry entry created."
Write-Host ""
Write-Host "Install complete." -ForegroundColor Green