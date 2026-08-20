# ============================================================
#  RevCopilot - Remote Installer
#  Downloads the latest release from GitHub and installs it.
#  Author: Irfan Irwanuddin
# ============================================================
#
#  One-liner usage (PowerShell):
#    & ([scriptblock]::Create((irm 'https://raw.githubusercontent.com/irfanirw/copilot_revit/main/Install-RevCopilot-Remote.ps1').Content))
#
#  One-liner usage (Command Prompt / any terminal):
#    powershell -ExecutionPolicy Bypass -Command "& ([scriptblock]::Create((Invoke-WebRequest -Uri 'https://raw.githubusercontent.com/irfanirw/copilot_revit/main/Install-RevCopilot-Remote.ps1').Content))"
#
#  To uninstall remotely, pass -Uninstall to the scriptblock invocation:
#    powershell -ExecutionPolicy Bypass -Command "& ([scriptblock]::Create((Invoke-WebRequest -Uri 'https://raw.githubusercontent.com/irfanirw/copilot_revit/main/Install-RevCopilot-Remote.ps1').Content)) -Uninstall"
#
# ============================================================

param(
    [switch]$Uninstall
)

$ErrorActionPreference = "Stop"

# ---- Configuration ----
$RepoOwner     = "irfanirw"
$RepoName      = "copilot_revit"
$Branch        = "main"
$InstallerName = "Install-RevCopilot.ps1"   # lives at repo root

# ---- Banner ----
Write-Host ""
Write-Host "  ======================================" -ForegroundColor Cyan
if ($Uninstall) {
    Write-Host "    RevCopilot - Remote Uninstaller"      -ForegroundColor Cyan
} else {
    Write-Host "    RevCopilot - Remote Installer"        -ForegroundColor Cyan
}
Write-Host "    github.com/$RepoOwner/$RepoName"      -ForegroundColor Cyan
Write-Host "  ======================================" -ForegroundColor Cyan
Write-Host ""

# ---- Check Revit is not running ----
$revit = Get-Process -Name "Revit" -ErrorAction SilentlyContinue |
    Where-Object { $_.MainWindowHandle -ne 0 -or $_.MainWindowTitle }

if ($revit) {
    for ($i = 0; $i -lt 20; $i++) {
        Start-Sleep -Seconds 1
        $revit = Get-Process -Name "Revit" -ErrorAction SilentlyContinue |
            Where-Object { $_.MainWindowHandle -ne 0 -or $_.MainWindowTitle }
        if (-not $revit) { break }
    }
}

if ($revit) {
    Write-Host "  [!] Revit is currently running or has not fully closed yet." -ForegroundColor Yellow
    Write-Host "      Please close Revit and try again." -ForegroundColor Yellow
    Write-Host ""
    Read-Host "  Press Enter to exit"
    exit 1
}

# ---- Prepare temp directory ----
$TempDir = Join-Path $env:TEMP "RevCopilot_Install_$(Get-Random)"
New-Item -ItemType Directory -Path $TempDir -Force | Out-Null

try {
    # ---- Step 1: Download repo archive from GitHub ----
    $ZipUrl  = "https://github.com/$RepoOwner/$RepoName/archive/refs/heads/$Branch.zip"
    $ZipPath = Join-Path $TempDir "repo.zip"

    Write-Host "  [1/3] Downloading package from GitHub..." -ForegroundColor Cyan
    Write-Host "        $ZipUrl" -ForegroundColor DarkGray

    try {
        Invoke-WebRequest -Uri $ZipUrl -OutFile $ZipPath -UseBasicParsing
    } catch {
        Write-Host "  [ERROR] Download failed: $_" -ForegroundColor Red
        Write-Host ""
        Write-Host "  Please check your internet connection or visit:" -ForegroundColor Yellow
        Write-Host "  https://github.com/$RepoOwner/$RepoName" -ForegroundColor White
        Write-Host ""
        Read-Host "  Press Enter to exit"
        exit 1
    }

    Write-Host "  [OK]  Download complete." -ForegroundColor Green

    # ---- Step 2: Extract the archive ----
    Write-Host "  [2/3] Extracting package..." -ForegroundColor Cyan
    Expand-Archive -Path $ZipPath -DestinationPath $TempDir -Force

    $ExtractedRoot   = Join-Path $TempDir "$RepoName-$Branch"
    $InstallerScript = Join-Path $ExtractedRoot $InstallerName

    if (-not (Test-Path $InstallerScript)) {
        Write-Host "  [ERROR] Installer not found after extraction." -ForegroundColor Red
        Write-Host "          Expected: $InstallerScript" -ForegroundColor Red
        Write-Host ""
        Write-Host "  Visit https://github.com/$RepoOwner/$RepoName for manual instructions." -ForegroundColor Yellow
        Write-Host ""
        Read-Host "  Press Enter to exit"
        exit 1
    }

    Write-Host "  [OK]  Extraction complete." -ForegroundColor Green

    # ---- Step 3: Run the installer or uninstaller ----
    Write-Host "  [3/3] Running $(if ($Uninstall) { 'uninstaller' } else { 'installer' })..." -ForegroundColor Cyan
    Write-Host ""

    if ($Uninstall) {
        & powershell.exe -ExecutionPolicy Bypass -File $InstallerScript -Uninstall
    } else {
        & powershell.exe -ExecutionPolicy Bypass -File $InstallerScript
    }

} finally {
    # ---- Cleanup temp files ----
    if (Test-Path $TempDir) {
        Remove-Item $TempDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}
