# SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
#
# This file is part of the OntoLedgy Thermodynamics Architecture and is
# dual-licensed:
#
#   1. Open source under the GNU Affero General Public License v3.0 or
#      later (AGPL-3.0-or-later). See the LICENSE file in the repository
#      root for the full licence text and NOTICE for attribution.
#   2. Commercial under a separate proprietary licence offered by
#      OntoLedgy Ltd. See COMMERCIAL.md for terms and contact details.
#
# SPDX-License-Identifier: AGPL-3.0-or-later

#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Phase 1: Install system prerequisites (requires Administrator)
    Run this ONCE, then use setup-user.ps1 for the rest.

.DESCRIPTION
    Installs:
    - Chocolatey (if not present)
    - Git
    - Python 3.12
    - Visual Studio Build Tools 2022
    - Creates install directory with user permissions

.PARAMETER InstallPath
    Base installation path (default: C:\DwsimMcp)

.PARAMETER Username
    Username to grant full permissions to InstallPath (default: current user)

.EXAMPLE
    # Run as Administrator:
    .\setup-prerequisites.ps1

    # Then run as normal user:
    .\setup-user.ps1 -RepoUrl "https://github.com/your-org/dwsim_interop_services.git"
#>

param(
    [string]$InstallPath = "C:\DwsimMcp",
    [string]$Username
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

function Write-Step { param($msg) Write-Host "`n=== $msg ===" -ForegroundColor Cyan }
function Write-Success { param($msg) Write-Host "[OK] $msg" -ForegroundColor Green }

# Require username to be specified explicitly when running as Administrator
if (-not $Username) {
    Write-Host "[ERROR] You must specify -Username when running as Administrator." -ForegroundColor Red
    Write-Host ""
    Write-Host "Usage:" -ForegroundColor Yellow
    Write-Host "  .\setup-prerequisites.ps1 -Username 'yourusername'" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Example:" -ForegroundColor Yellow
    Write-Host "  .\setup-prerequisites.ps1 -Username 'khanm'" -ForegroundColor Yellow
    Write-Host ""
    exit 1
}

Write-Host @"
=========================================
  DWSIM MCP - Prerequisites Setup
  (Phase 1 - Administrator)
=========================================
"@ -ForegroundColor Magenta

# ============================================
# STEP 1: Install Chocolatey
# ============================================
Write-Step "Installing Chocolatey package manager"
if (-not (Get-Command choco -ErrorAction SilentlyContinue)) {
    Set-ExecutionPolicy Bypass -Scope Process -Force
    [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.ServicePointManager]::SecurityProtocol -bor 3072
    Invoke-Expression ((New-Object System.Net.WebClient).DownloadString('https://community.chocolatey.org/install.ps1'))
    $env:Path = [System.Environment]::GetEnvironmentVariable("Path","Machine") + ";" + [System.Environment]::GetEnvironmentVariable("Path","User")
    Write-Success "Chocolatey installed"
} else {
    Write-Success "Chocolatey already installed"
}

# ============================================
# STEP 2: Install Git
# ============================================
Write-Step "Installing Git"
if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    choco install git -y --no-progress
    $env:Path = [System.Environment]::GetEnvironmentVariable("Path","Machine") + ";" + [System.Environment]::GetEnvironmentVariable("Path","User")
    Write-Success "Git installed"
} else {
    Write-Success "Git already installed: $(git --version)"
}

# ============================================
# STEP 3: Install Python 3.12
# ============================================
Write-Step "Installing Python 3.12"
$pythonPath = "C:\Python312\python.exe"
if (-not (Test-Path $pythonPath)) {
    choco install python312 -y --no-progress --params "/InstallDir:C:\Python312"
    $env:Path = [System.Environment]::GetEnvironmentVariable("Path","Machine") + ";" + [System.Environment]::GetEnvironmentVariable("Path","User")
    Write-Success "Python 3.12 installed"
} else {
    Write-Success "Python already installed"
}

# ============================================
# STEP 4: Install Visual Studio Build Tools
# ============================================
Write-Step "Installing Visual Studio Build Tools 2022"
$vsWhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$vsInstalled = $false

if (Test-Path $vsWhere) {
    $vsPath = & $vsWhere -latest -property installationPath 2>$null
    if ($vsPath) { $vsInstalled = $true }
}

if (-not $vsInstalled) {
    Write-Host "Downloading Visual Studio Build Tools (this takes a while)..."
    choco install visualstudio2022buildtools -y --no-progress --package-parameters "--add Microsoft.VisualStudio.Workload.ManagedDesktopBuildTools --add Microsoft.Net.Component.4.8.SDK --includeRecommended --quiet --wait"
    Write-Success "Visual Studio Build Tools installed"
} else {
    Write-Success "Visual Studio already installed"
}

# Grant read/execute access to VS Build Tools for all users (required for non-admin builds)
Write-Host "Granting read/execute access to VS Build Tools for all users..."
$vsBuildToolsPath = "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools"
$vsInstallerPath = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer"

if (Test-Path $vsBuildToolsPath) {
    & icacls $vsBuildToolsPath /grant "Users:(OI)(CI)RX" /T /Q
    Write-Success "VS Build Tools permissions updated for all users"
}
if (Test-Path $vsInstallerPath) {
    & icacls $vsInstallerPath /grant "Users:(OI)(CI)RX" /T /Q
    Write-Success "VS Installer permissions updated for all users"
}

# ============================================
# STEP 5: Create Install Directory with User Permissions
# ============================================
Write-Step "Creating installation directory with user permissions"

if (-not (Test-Path $InstallPath)) {
    New-Item -ItemType Directory -Path $InstallPath -Force | Out-Null
}

# Grant the specified user full control
$acl = Get-Acl $InstallPath
$permission = "$env:USERDOMAIN\$Username", "FullControl", "ContainerInherit,ObjectInherit", "None", "Allow"
$accessRule = New-Object System.Security.AccessControl.FileSystemAccessRule $permission
$acl.SetAccessRule($accessRule)
Set-Acl $InstallPath $acl

Write-Success "Created $InstallPath with full permissions for $Username"

# ============================================
# STEP 6: Enable Developer Mode for Symlinks (Windows 10+)
# ============================================
Write-Step "Enabling Developer Mode (allows symlinks without admin)"
try {
    $devModeKey = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock"
    if (-not (Test-Path $devModeKey)) {
        New-Item -Path $devModeKey -Force | Out-Null
    }
    Set-ItemProperty -Path $devModeKey -Name "AllowDevelopmentWithoutDevLicense" -Value 1 -Type DWord
    Write-Success "Developer Mode enabled (symlinks now work without admin)"
} catch {
    Write-Host "[WARN] Could not enable Developer Mode - symlinks may require admin" -ForegroundColor Yellow
}

# ============================================
# SUMMARY
# ============================================
Write-Host @"

=========================================
  PREREQUISITES INSTALLED!
=========================================

Installed:
  - Chocolatey
  - Git
  - Python 3.12
  - Visual Studio Build Tools 2022

Directory created:
  $InstallPath (with full permissions for $Username)

NEXT STEP (run as normal user):
  .\setup-user.ps1

"@ -ForegroundColor Green
