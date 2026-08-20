# ============================================================
#  RevAI Platform - Unified Remote Installer
#  Installs or uninstalls RevCode, RevAI, and/or RevCopilot from GitHub.
#  Author: Irfan Irwanuddin
# ============================================================
#
#  One-liner usage (PowerShell):
#    iex (irm 'https://raw.githubusercontent.com/irfanirw/copilot_revit/main/Install-All-Remote.ps1')
#
#  One-liner usage (Command Prompt / any terminal):
#    powershell -ExecutionPolicy Bypass -Command "iex (irm 'https://raw.githubusercontent.com/irfanirw/copilot_revit/main/Install-All-Remote.ps1')"
#
#  To uninstall remotely: use the same script and pass -Uninstall.
#
# ============================================================

param(
    [switch]$Uninstall
)

$ErrorActionPreference = 'Stop'

# ---- Configuration ----
$RepoOwner = 'irfanirw'
$RepoName  = 'copilot_revit'
$Branch    = 'main'

# Map plugin name -> installer script path (relative to extracted repo root)
$Plugins = [ordered]@{
    'RevCode'    = 'RevCode_v1.0.0\Install-RevCode.ps1'
    'RevAI'      = 'Install-RevAI.ps1'
    'RevCopilot' = 'Install-RevCopilot.ps1'
}

function Test-RevitNotRunning {
    $revit = Get-Process -Name 'Revit' -ErrorAction SilentlyContinue
    if (-not $revit) {
        return $true
    }

    $active = $revit | Where-Object { $_.MainWindowHandle -ne 0 -or $_.MainWindowTitle }
    if (-not $active) {
        return $true
    }

    for ($i = 0; $i -lt 20; $i++) {
        Start-Sleep -Seconds 1
        $revit = Get-Process -Name 'Revit' -ErrorAction SilentlyContinue
        $active = $revit | Where-Object { $_.MainWindowHandle -ne 0 -or $_.MainWindowTitle }
        if (-not $active) {
            return $true
        }
    }

    return $false
}

# ---- Banner ----
Write-Host ''
Write-Host '  ============================================' -ForegroundColor Cyan
if ($Uninstall) {
    Write-Host '    RevAI Platform - Remote Uninstaller' -ForegroundColor Cyan
} else {
    Write-Host '    RevAI Platform - Remote Installer' -ForegroundColor Cyan
}
Write-Host '    github.com/$RepoOwner/$RepoName' -ForegroundColor Cyan
Write-Host '  ============================================' -ForegroundColor Cyan
Write-Host ''

# ---- Check Revit is not running ----
if (-not (Test-RevitNotRunning)) {
    Write-Host '  [!] Revit is currently running or has not fully closed yet.' -ForegroundColor Yellow
    Write-Host '      Please close Revit and try again.' -ForegroundColor Yellow
    Write-Host ''
    Read-Host '  Press Enter to exit'
    exit 1
}

# ---- Plugin selection menu ----
Write-Host '  Select plugins to install:' -ForegroundColor White
Write-Host ''
Write-Host '    [1]  RevCode    - C# Code Editor for Revit' -ForegroundColor White
Write-Host '    [2]  RevAI      - AI Assistant (multi-provider)' -ForegroundColor White
Write-Host '    [3]  RevCopilot - Microsoft 365 Copilot integration' -ForegroundColor White
Write-Host '    [4]  All        - Install all three plugins' -ForegroundColor White
Write-Host ''

$choice = Read-Host '  Enter your choice (1/2/3/4)'
Write-Host ''

$ToInstall = switch ($choice.Trim()) {
    '1' { @('RevCode') }
    '2' { @('RevAI') }
    '3' { @('RevCopilot') }
    '4' { @('RevCode', 'RevAI', 'RevCopilot') }
    default {
        Write-Host '  [!] Invalid choice. Please run the script again and enter 1, 2, 3, or 4.' -ForegroundColor Yellow
        Write-Host ''
        Read-Host '  Press Enter to exit'
        exit 1
    }
}

Write-Host "  Installing: $($ToInstall -join ', ')" -ForegroundColor Cyan
Write-Host ''

# ---- Prepare temp directory ----
$TempDir = Join-Path $env:TEMP "RevAIPlatform_Install_$(Get-Random)"
New-Item -ItemType Directory -Path $TempDir -Force | Out-Null

try {
    # ---- Step 1: Download repo archive ----
    $ZipUrl  = "https://github.com/$RepoOwner/$RepoName/archive/refs/heads/$Branch.zip"
    $ZipPath = Join-Path $TempDir 'repo.zip'

    Write-Host '  [1/3] Downloading package from GitHub...' -ForegroundColor Cyan
    Write-Host "        $ZipUrl" -ForegroundColor DarkGray

    try {
        Invoke-WebRequest -Uri $ZipUrl -OutFile $ZipPath -UseBasicParsing
    }
    catch {
        Write-Host "  [ERROR] Download failed: $_" -ForegroundColor Red
        Write-Host ''
        Write-Host '  Please check your internet connection or visit:' -ForegroundColor Yellow
        Write-Host "  https://github.com/$RepoOwner/$RepoName" -ForegroundColor White
        Write-Host ''
        Read-Host '  Press Enter to exit'
        exit 1
    }

    Write-Host '  [OK]  Download complete.' -ForegroundColor Green

    # ---- Step 2: Extract the archive ----
    Write-Host '  [2/3] Extracting package...' -ForegroundColor Cyan
    Expand-Archive -Path $ZipPath -DestinationPath $TempDir -Force

    $ExtractedRoot = Join-Path $TempDir "$RepoName-$Branch"
    Write-Host '  [OK]  Extraction complete.' -ForegroundColor Green
    Write-Host ''

    # ---- Step 3: Run selected installers ----
    Write-Host '  [3/3] Running installer(s)...' -ForegroundColor Cyan
    Write-Host ''

    foreach ($PluginName in $ToInstall) {
        $RelativePath = $Plugins[$PluginName]
        $InstallerScript = Join-Path $ExtractedRoot $RelativePath

        if (-not (Test-Path $InstallerScript)) {
            Write-Host "  [WARN] Installer for $PluginName not found, skipping." -ForegroundColor Yellow
            Write-Host "         Expected: $InstallerScript" -ForegroundColor DarkGray
            continue
        }

        if ($Uninstall) {
            Write-Host "  ---- Uninstalling $PluginName ----" -ForegroundColor Cyan
            & powershell.exe -ExecutionPolicy Bypass -File $InstallerScript -Silent -Uninstall
        } else {
            Write-Host "  ---- Installing $PluginName ----" -ForegroundColor Cyan
            & powershell.exe -ExecutionPolicy Bypass -File $InstallerScript -Silent
        }
        Write-Host ''
    }

    # ---- Summary ----
    Write-Host '  ============================================' -ForegroundColor Green
    if ($Uninstall) {
        Write-Host '    All selected plugins uninstalled!' -ForegroundColor Green
    } else {
        Write-Host '    All selected plugins installed!' -ForegroundColor Green
    }
    Write-Host '  ============================================' -ForegroundColor Green
    Write-Host ''
    if (-not $Uninstall) {
        Write-Host '  Launch Revit 2025 — plugins appear under the' -ForegroundColor White
        Write-Host "  'Code & Automations' ribbon tab." -ForegroundColor White
        Write-Host ''
    }
}
finally {
    # ---- Cleanup temp files ----
    if (Test-Path $TempDir) {
        Remove-Item $TempDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Read-Host '  Press Enter to exit'
