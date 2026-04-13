# ============================================================
#  RevAI - Revit 2025 AI Assistant  |  No-Admin Installer
#  Author: Irfan Irwanuddin
# ============================================================
#
#  Usage:   Right-click -> "Run with PowerShell"
#           or:  powershell -ExecutionPolicy Bypass -File Install-RevAI.ps1
#
#  To uninstall:  powershell -ExecutionPolicy Bypass -File Install-RevAI.ps1 -Uninstall
#
# ============================================================

param(
    [switch]$Uninstall,
    [switch]$Silent
)

$ErrorActionPreference = "Stop"

# ---- Configuration ----
$AppName        = "RevAI"
$RevitVersion   = "2025"
$AddinsRoot     = Join-Path $env:APPDATA "Autodesk\Revit\Addins\$RevitVersion"
$PluginFolder   = Join-Path $AddinsRoot $AppName
$AddinManifest  = Join-Path $AddinsRoot "$AppName.addin"
$ScriptDir      = Split-Path -Parent $MyInvocation.MyCommand.Definition
$SourceDir      = Join-Path $ScriptDir "src\RevAI\bin\Release"

# ---- Helper ----
function Write-Banner {
    Write-Host ""
    Write-Host "  ======================================" -ForegroundColor Cyan
    Write-Host "    RevAI - AI-Powered Revit Assistant"   -ForegroundColor Cyan
    Write-Host "    Installer for Revit $RevitVersion"    -ForegroundColor Cyan
    Write-Host "  ======================================" -ForegroundColor Cyan
    Write-Host ""
}

function Test-RevitRunning {
    $revit = Get-Process -Name "Revit" -ErrorAction SilentlyContinue
    if ($revit) {
        Write-Host "  [!] Revit is currently running." -ForegroundColor Yellow
        Write-Host "      Please close Revit and try again." -ForegroundColor Yellow
        Write-Host ""
        if (-not $Silent) {
            Read-Host "  Press Enter to exit"
        }
        exit 1
    }
}

# ---- Uninstall ----
if ($Uninstall) {
    Write-Banner
    Write-Host "  Uninstalling $AppName..." -ForegroundColor Yellow
    Test-RevitRunning

    $removed = $false

    if (Test-Path $PluginFolder) {
        Remove-Item $PluginFolder -Recurse -Force
        Write-Host "  [OK] Removed plugin folder" -ForegroundColor Green
        $removed = $true
    }

    if (Test-Path $AddinManifest) {
        Remove-Item $AddinManifest -Force
        Write-Host "  [OK] Removed addin manifest" -ForegroundColor Green
        $removed = $true
    }

    if ($removed) {
        Write-Host ""
        Write-Host "  $AppName has been uninstalled successfully." -ForegroundColor Green
    } else {
        Write-Host "  $AppName was not installed." -ForegroundColor Yellow
    }

    Write-Host ""
    if (-not $Silent) { Read-Host "  Press Enter to exit" }
    exit 0
}

# ---- Install ----
Write-Banner

# Validate source exists
if (-not (Test-Path (Join-Path $SourceDir "$AppName.dll"))) {
    Write-Host "  [ERROR] Build output not found at:" -ForegroundColor Red
    Write-Host "          $SourceDir" -ForegroundColor Red
    Write-Host ""
    Write-Host "  Please build the solution first:" -ForegroundColor Yellow
    Write-Host "    dotnet build RevAI.sln -c Release" -ForegroundColor White
    Write-Host ""
    if (-not $Silent) { Read-Host "  Press Enter to exit" }
    exit 1
}

# Check for running Revit
Test-RevitRunning

# Show install info
Write-Host "  Install location:"
Write-Host "    Plugin:   $PluginFolder" -ForegroundColor Gray
Write-Host "    Manifest: $AddinManifest" -ForegroundColor Gray
Write-Host ""

if (-not $Silent) {
    $confirm = Read-Host "  Proceed with installation? (Y/n)"
    if ($confirm -and $confirm -notin @("Y", "y", "yes", "")) {
        Write-Host "  Installation cancelled." -ForegroundColor Yellow
        exit 0
    }
    Write-Host ""
}

# Step 1: Create addins directory if needed
if (-not (Test-Path $AddinsRoot)) {
    New-Item -ItemType Directory -Path $AddinsRoot -Force | Out-Null
    Write-Host "  [OK] Created Revit addins directory" -ForegroundColor Green
}

# Step 2: Remove old installation if present
if (Test-Path $PluginFolder) {
    Remove-Item $PluginFolder -Recurse -Force
    Write-Host "  [OK] Removed previous installation" -ForegroundColor Green
}

# Step 3: Copy plugin files
New-Item -ItemType Directory -Path $PluginFolder -Force | Out-Null
Copy-Item "$SourceDir\*" -Destination $PluginFolder -Recurse -Force
Write-Host "  [OK] Copied plugin files" -ForegroundColor Green

# Step 4: Copy .addin manifest to addins root
$sourceAddin = Join-Path $ScriptDir "src\RevAI\RevAI.addin"
if (Test-Path $sourceAddin) {
    Copy-Item $sourceAddin -Destination $AddinManifest -Force
} else {
    # Fallback: use the one from bin output
    $binAddin = Join-Path $SourceDir "$AppName.addin"
    if (Test-Path $binAddin) {
        Copy-Item $binAddin -Destination $AddinManifest -Force
    }
}
Write-Host "  [OK] Installed addin manifest" -ForegroundColor Green

# Step 5: Sign the DLL if code signing cert is available
$cert = Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert -ErrorAction SilentlyContinue |
    Where-Object { $_.Subject -like "*Irfan Irwanuddin*" } |
    Select-Object -First 1

if ($cert) {
    $dllPath = Join-Path $PluginFolder "$AppName.dll"
    $sig = Set-AuthenticodeSignature -FilePath $dllPath -Certificate $cert -HashAlgorithm SHA256 -TimestampServer "http://timestamp.digicert.com" -ErrorAction SilentlyContinue
    if ($sig.Status -eq "Valid") {
        Write-Host "  [OK] Code-signed $AppName.dll" -ForegroundColor Green
    } else {
        Write-Host "  [--] Code signing skipped (cert not fully trusted)" -ForegroundColor DarkGray
    }
} else {
    Write-Host "  [--] Code signing skipped (no certificate found)" -ForegroundColor DarkGray
}

# Done
Write-Host ""
Write-Host "  ======================================" -ForegroundColor Green
Write-Host "    Installation complete!" -ForegroundColor Green
Write-Host "  ======================================" -ForegroundColor Green
Write-Host ""
Write-Host "  Launch Revit $RevitVersion to use RevAI." -ForegroundColor White
Write-Host "  The plugin will appear under the 'Code & Automations' tab." -ForegroundColor White
Write-Host ""

if (-not $Silent) { Read-Host "  Press Enter to exit" }
