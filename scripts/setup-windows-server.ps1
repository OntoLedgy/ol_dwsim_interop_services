#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Automated setup script for DWSIM MCP Server on Windows Server 2022

.DESCRIPTION
    This script installs all prerequisites and configures the DWSIM MCP Server:
    - Chocolatey (package manager)
    - Git
    - Python 3.12
    - Visual Studio Build Tools 2022
    - DWSIM 9.x
    - Clones and builds the repository
    - Configures the MCP server

.PARAMETER RepoUrl
    Git repository URL for dwsim_interop_services

.PARAMETER InstallPath
    Base installation path (default: C:\DwsimMcp)

.PARAMETER SkipReboot
    Skip the reboot prompt at the end

.EXAMPLE
    .\setup-windows-server.ps1 -RepoUrl "https://github.com/your-org/dwsim_interop_services.git"
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$RepoUrl,

    [string]$InstallPath = "C:\DwsimMcp",

    [switch]$SkipReboot
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"  # Speeds up downloads

# Colors for output
function Write-Step { param($msg) Write-Host "`n=== $msg ===" -ForegroundColor Cyan }
function Write-Success { param($msg) Write-Host "[OK] $msg" -ForegroundColor Green }
function Write-Warning { param($msg) Write-Host "[WARN] $msg" -ForegroundColor Yellow }
function Write-Error { param($msg) Write-Host "[ERROR] $msg" -ForegroundColor Red }

# Check if running as admin
if (-NOT ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
    Write-Error "This script must be run as Administrator!"
    exit 1
}

Write-Host @"
=========================================
  DWSIM MCP Server - Windows Setup
=========================================
Install Path: $InstallPath
Repository:   $RepoUrl
DWSIM:        v9.0.5-mcp (downloaded via CLI)
=========================================
"@ -ForegroundColor Magenta

# Create install directory
Write-Step "Creating installation directory"
if (-not (Test-Path $InstallPath)) {
    New-Item -ItemType Directory -Path $InstallPath -Force | Out-Null
}
Set-Location $InstallPath
Write-Success "Created $InstallPath"

# ============================================
# STEP 1: Install Chocolatey
# ============================================
Write-Step "Installing Chocolatey package manager"
if (-not (Get-Command choco -ErrorAction SilentlyContinue)) {
    Set-ExecutionPolicy Bypass -Scope Process -Force
    [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.ServicePointManager]::SecurityProtocol -bor 3072
    Invoke-Expression ((New-Object System.Net.WebClient).DownloadString('https://community.chocolatey.org/install.ps1'))

    # Refresh environment
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
    # Refresh environment
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
    # Refresh environment
    $env:Path = [System.Environment]::GetEnvironmentVariable("Path","Machine") + ";" + [System.Environment]::GetEnvironmentVariable("Path","User")
    Write-Success "Python 3.12 installed"
} else {
    Write-Success "Python already installed: $(& $pythonPath --version)"
}

# Verify Python
$pythonExe = "C:\Python312\python.exe"
if (-not (Test-Path $pythonExe)) {
    $pythonExe = (Get-Command python -ErrorAction SilentlyContinue).Source
}
Write-Success "Using Python: $pythonExe"

# ============================================
# STEP 4: Install Visual Studio Build Tools
# ============================================
Write-Step "Installing Visual Studio Build Tools 2022"
$vsWhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$vsInstalled = $false

if (Test-Path $vsWhere) {
    $vsPath = & $vsWhere -latest -property installationPath 2>$null
    if ($vsPath) {
        $vsInstalled = $true
        Write-Success "Visual Studio already installed at: $vsPath"
    }
}

if (-not $vsInstalled) {
    Write-Host "Downloading Visual Studio Build Tools (this may take a while)..."
    choco install visualstudio2022buildtools -y --no-progress --package-parameters "--add Microsoft.VisualStudio.Workload.ManagedDesktopBuildTools --add Microsoft.Net.Component.4.8.SDK --add Microsoft.NetCore.Component.Runtime.8.0 --add Microsoft.NetCore.Component.SDK --includeRecommended --quiet --wait"
    Write-Success "Visual Studio Build Tools installed"
}

# ============================================
# STEP 5: Verify .NET Framework 4.8
# ============================================
Write-Step "Verifying .NET Framework 4.8"
$netRelease = (Get-ItemProperty "HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full" -ErrorAction SilentlyContinue).Release
if ($netRelease -ge 528040) {
    Write-Success ".NET Framework 4.8 is installed (Release: $netRelease)"
} else {
    Write-Warning ".NET Framework 4.8 may need to be installed manually"
}

# ============================================
# STEP 6: Skip DWSIM Installation (handled by CLI)
# ============================================
Write-Step "DWSIM binaries will be downloaded via CLI"
Write-Host "The 'dwsim-mcp setup --download' command will download DWSIM binaries"
Write-Host "from: https://github.com/OntoLedgy/dwsim/releases/download/v9.0.5-mcp/dwsim_binaries.zip"
Write-Success "DWSIM download deferred to Step 10"

# ============================================
# STEP 7: Clone Repository
# ============================================
Write-Step "Cloning repository"
$repoPath = "$InstallPath\dwsim_interop_services"

if (-not (Test-Path "$repoPath\.git")) {
    Set-Location $InstallPath
    git clone $RepoUrl dwsim_interop_services
    Write-Success "Repository cloned to $repoPath"
} else {
    Write-Success "Repository already exists at $repoPath"
    Set-Location $repoPath
    git pull
    Write-Success "Repository updated"
}

# ============================================
# STEP 8: Setup Python Environment (needed for CLI)
# ============================================
Write-Step "Setting up Python environment"
$serverPath = "$repoPath\mcp_service\server"
$workerPath = "$repoPath\mcp_service\dwsim_worker"
Set-Location $serverPath

# Create virtual environment
if (-not (Test-Path ".venv")) {
    & $pythonExe -m venv .venv
    Write-Success "Virtual environment created"
} else {
    Write-Success "Virtual environment already exists"
}

# Activate and install dependencies
$venvPython = "$serverPath\.venv\Scripts\python.exe"
$venvPip = "$serverPath\.venv\Scripts\pip.exe"

Write-Host "Installing Python dependencies..."
& $venvPip install --upgrade pip
& $venvPip install -e ".[dev,http]"
Write-Success "Python dependencies installed"

# ============================================
# STEP 9: Download DWSIM binaries via CLI
# ============================================
Write-Step "Downloading DWSIM binaries via CLI"

# Set PYTHONPATH for the CLI to find models module
$env:PYTHONPATH = "$repoPath;$serverPath"

# Run the CLI setup command to download DWSIM binaries
# This downloads to dwsim_worker/dwsim_binaries/x64/Debug and creates dwsim.config.json
Write-Host "Running: dwsim-mcp setup --download"
Write-Host "Source: https://github.com/OntoLedgy/dwsim/releases/download/v9.0.5-mcp/dwsim_binaries.zip"
$dwsimMcpCli = "$serverPath\.venv\Scripts\dwsim-mcp.exe"
& $dwsimMcpCli setup --download

if ($LASTEXITCODE -eq 0) {
    Write-Success "DWSIM binaries downloaded successfully"
} else {
    Write-Error "DWSIM download failed - check output above"
    Write-Host "You can retry manually with: dwsim-mcp setup --download"
    exit 1
}

# ============================================
# STEP 10: Configure MSBuild path in config
# ============================================
Write-Step "Configuring MSBuild path"
$configPath = "$workerPath\DwsimWorker\dwsim.config.json"

# Find MSBuild
$msbuildPath = ""
$vsWhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
if (Test-Path $vsWhere) {
    $vsPath = & $vsWhere -latest -property installationPath 2>$null
    if ($vsPath) {
        $msbuildPath = "$vsPath\MSBuild\Current\Bin\MSBuild.exe"
    }
}

# Update config file with MSBuild path (CLI already created it with dwsim_path)
if (Test-Path $configPath) {
    $config = Get-Content $configPath -Raw | ConvertFrom-Json
    $config | Add-Member -NotePropertyName "msbuild_path" -NotePropertyValue ($msbuildPath -replace '\\', '/') -Force
    $config | ConvertTo-Json -Depth 2 | Set-Content -Path $configPath
    Write-Success "Updated config with MSBuild path"
    Write-Host "  msbuild_path: $msbuildPath"
} else {
    Write-Warning "Config file not found at $configPath - CLI download may have failed"
}

# ============================================
# STEP 11: Build DwsimWorker
# ============================================
Write-Step "Building DwsimWorker"
Set-Location $workerPath

# Run build (setup-dwsim-binaries will be a no-op since source = target)
$buildResult = & cmd /c "build.bat 2>&1"
$buildResult | ForEach-Object { Write-Host $_ }

if ($LASTEXITCODE -eq 0) {
    Write-Success "DwsimWorker built successfully"
} else {
    Write-Warning "Build may have had issues - check output above"
}

# ============================================
# STEP 12: Create Environment File
# ============================================
Write-Step "Creating environment configuration"
$workerDll = "$workerPath\DwsimWorker\bin\Debug\DwsimWorker.dll"
$envContent = @"
# DWSIM MCP Server Configuration
DWSIM_WORKER_ASSEMBLY_PATH=$workerDll
DWSIM_CASE_STORAGE_ROOTS=$InstallPath\simulations
PYTHONPATH=$repoPath;$serverPath
"@

Set-Content -Path "$serverPath\.env" -Value $envContent
Write-Success "Created .env file"

# Create simulations directory
$simPath = "$InstallPath\simulations"
if (-not (Test-Path $simPath)) {
    New-Item -ItemType Directory -Path $simPath -Force | Out-Null
    Write-Success "Created simulations directory: $simPath"
}

# ============================================
# STEP 13: Create Start Script
# ============================================
Write-Step "Creating start scripts"

# HTTP mode start script
$startHttpScript = @"
@echo off
cd /d "$serverPath"
call .venv\Scripts\activate.bat
echo Starting DWSIM MCP Server (HTTP mode on port 8000)...
python -m dwsim_mcp_server --transport streamable-http --port 8000
"@
Set-Content -Path "$InstallPath\start-http.bat" -Value $startHttpScript

# Stdio mode start script
$startStdioScript = @"
@echo off
cd /d "$serverPath"
call .venv\Scripts\activate.bat
echo Starting DWSIM MCP Server (stdio mode)...
python -m dwsim_mcp_server
"@
Set-Content -Path "$InstallPath\start-stdio.bat" -Value $startStdioScript

# Diagnostic script
$diagScript = @"
@echo off
cd /d "$serverPath"
call .venv\Scripts\activate.bat
dwsim-mcp doctor
"@
Set-Content -Path "$InstallPath\diagnose.bat" -Value $diagScript

Write-Success "Created start scripts in $InstallPath"

# ============================================
# STEP 14: Run Diagnostics
# ============================================
Write-Step "Running diagnostics"
Set-Location $serverPath
& $dwsimMcpCli doctor

# ============================================
# SUMMARY
# ============================================
Write-Host @"

=========================================
  INSTALLATION COMPLETE!
=========================================
"@ -ForegroundColor Green

Write-Host @"
Installation Summary:
---------------------
  Install Path:    $InstallPath
  Repository:      $repoPath
  DWSIM Path:      $dwsimBinPath
  Python venv:     $serverPath\.venv
  Worker DLL:      $workerDll

Quick Start:
------------
  Run HTTP server:   $InstallPath\start-http.bat
  Run diagnostics:   $InstallPath\diagnose.bat

Manual Commands:
----------------
  cd "$serverPath"
  .\.venv\Scripts\Activate.ps1
  python -m dwsim_mcp_server --transport streamable-http --port 8000

Firewall (if needed):
---------------------
  netsh advfirewall firewall add rule name="DWSIM MCP" dir=in action=allow protocol=tcp localport=8000

"@ -ForegroundColor Cyan

if (-not $SkipReboot) {
    Write-Host "`nA reboot is recommended to ensure all environment variables are loaded." -ForegroundColor Yellow
    $reboot = Read-Host "Reboot now? (y/N)"
    if ($reboot -eq 'y' -or $reboot -eq 'Y') {
        Restart-Computer
    }
}

Write-Host "`nSetup complete!" -ForegroundColor Green
