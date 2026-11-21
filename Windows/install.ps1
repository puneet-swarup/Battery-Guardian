$ErrorActionPreference = 'Stop'

Write-Host "=== Battery Guardian Installer ===" -ForegroundColor Cyan
Write-Host ""

$installDir = Join-Path $env:LOCALAPPDATA 'Programs\BatteryGuardian'
$sourceDir  = Split-Path -Parent $MyInvocation.MyCommand.Path

Write-Host "[1] Source dir : $sourceDir"
Write-Host "[2] Install dir: $installDir"
Write-Host ""

# ------------------------------------------------------------------
# STEP 2a — Stop any running instance and wait for file handles to release
# ------------------------------------------------------------------
Write-Host "[2a] Stopping any running Battery Guardian..."
$procs = Get-Process -Name 'BatteryGuardian' -ErrorAction SilentlyContinue
if ($procs) {
    $procs | Stop-Process -Force -ErrorAction SilentlyContinue
    Write-Host "     Stopped $($procs.Count) instance(s). Waiting for file handles to release..."

    # Poll for up to 10 seconds for the process to fully disappear
    $waited = 0
    while ((Get-Process -Name 'BatteryGuardian' -ErrorAction SilentlyContinue) -and $waited -lt 10000) {
        Start-Sleep -Milliseconds 250
        $waited += 250
    }

    # Extra settling time for Windows to release file handles
    Start-Sleep -Milliseconds 500
    Write-Host "     Waited ${waited}ms. Proceeding."
} else {
    Write-Host "     No running instance found."
}

# ------------------------------------------------------------------
# STEP 3 — Create install directory
# ------------------------------------------------------------------
if (-not (Test-Path $installDir)) {
    New-Item -ItemType Directory -Path $installDir -Force | Out-Null
}
Write-Host "[3] Install dir created: $(Test-Path $installDir)"

# ------------------------------------------------------------------
# STEP 4 — Copy files (with retry on lock)
# ------------------------------------------------------------------
Write-Host "[4] Copying files..."
$files = Get-ChildItem -Path $sourceDir -Exclude 'install.bat','install.ps1','uninstall.bat','uninstall.ps1'
Write-Host "    Found $($files.Count) files to copy."

foreach ($file in $files) {
    $attempts = 0
    $maxAttempts = 3
    while ($true) {
        try {
            Copy-Item -Path $file.FullName -Destination $installDir -Recurse -Force -ErrorAction Stop
            break
        }
        catch {
            $attempts++
            if ($attempts -ge $maxAttempts) {
                Write-Host "    FAILED to copy $($file.Name) after $maxAttempts attempts" -ForegroundColor Red
                throw
            }
            Write-Host "    Retry $attempts/$maxAttempts for $($file.Name)..." -ForegroundColor Yellow
            Start-Sleep -Milliseconds 800
        }
    }
}
Write-Host "    Files in install dir: $((Get-ChildItem $installDir -Recurse).Count)"

# Copy uninstall scripts so the app can be removed from the install folder
Copy-Item -Path (Join-Path $sourceDir 'uninstall.bat') -Destination $installDir -Force
Copy-Item -Path (Join-Path $sourceDir 'uninstall.ps1') -Destination $installDir -Force

# ------------------------------------------------------------------
# STEP 5 — Create Start Menu shortcut
# ------------------------------------------------------------------
$exePath = Join-Path $installDir 'BatteryGuardian.exe'
$programsFolder = [Environment]::GetFolderPath('Programs')
$shortcutPath = Join-Path $programsFolder 'Battery Guardian.lnk'

Write-Host "[5] Shortcut target  : $exePath"
Write-Host "    Shortcut location: $shortcutPath"
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
    Write-Host "    Shortcut created: $(Test-Path $shortcutPath)" -ForegroundColor Green
} catch {
    Write-Host "    ERROR creating shortcut: $($_.Exception.Message)" -ForegroundColor Red
}
Write-Host ""

# ------------------------------------------------------------------
# STEP 7 — Register in "Settings -> Apps"
# ------------------------------------------------------------------
$uninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\BatteryGuardian'
New-Item -Path $uninstallKey -Force | Out-Null
Set-ItemProperty -Path $uninstallKey -Name 'DisplayName'     -Value 'Battery Guardian'
Set-ItemProperty -Path $uninstallKey -Name 'DisplayVersion'  -Value '1.11.0'
Set-ItemProperty -Path $uninstallKey -Name 'Publisher'       -Value 'Puneet Swarup'
Set-ItemProperty -Path $uninstallKey -Name 'InstallLocation' -Value $installDir
Set-ItemProperty -Path $uninstallKey -Name 'NoModify'        -Value 1 -Type DWord
Set-ItemProperty -Path $uninstallKey -Name 'NoRepair'        -Value 1 -Type DWord
Set-ItemProperty -Path $uninstallKey -Name 'UninstallString' -Value ('"' + (Join-Path $installDir 'uninstall.bat') + '"')
Write-Host "[7] Registry entry created."
Write-Host ""

# ------------------------------------------------------------------
# STEP 8 — Launch the app (it minimizes to tray on startup)
# ------------------------------------------------------------------
Write-Host "[8] Starting Battery Guardian..."

if (-not (Test-Path $exePath)) {
    Write-Host "    ERROR: $exePath not found." -ForegroundColor Red
    Write-Host "    Install may be incomplete. Try running install.bat again." -ForegroundColor Red
    exit 1
}

try {
    Start-Process -FilePath $exePath -WorkingDirectory $installDir | Out-Null

    # Give it a moment to appear in the tray, then verify it's running
    Start-Sleep -Milliseconds 1500
    $running = Get-Process -Name 'BatteryGuardian' -ErrorAction SilentlyContinue

    if ($running) {
        Write-Host "    Launched successfully. Look for the tray icon." -ForegroundColor Green
    } else {
        Write-Host "    Launched, but the process is not running." -ForegroundColor Yellow
        Write-Host "    You can start it manually from the Start Menu." -ForegroundColor Yellow
    }
}
catch {
    Write-Host "    Could not start the app automatically: $($_.Exception.Message)" -ForegroundColor Yellow
    Write-Host "    You can start it manually from the Start Menu." -ForegroundColor Yellow
}
Write-Host ""

Write-Host "Install complete." -ForegroundColor Green