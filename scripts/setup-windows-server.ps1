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

# Install uv (fast Python package manager)
Write-Host "Installing uv package manager..."
if (-not (Get-Command uv -ErrorAction SilentlyContinue)) {
    # Install uv via PowerShell
    Invoke-WebRequest -Uri "https://astral.sh/uv/install.ps1" -OutFile "$env:TEMP\install-uv.ps1"
    & powershell -ExecutionPolicy Bypass -File "$env:TEMP\install-uv.ps1"
    Remove-Item "$env:TEMP\install-uv.ps1" -Force -ErrorAction SilentlyContinue

    # Add uv to PATH for this session
    $uvPath = "$env:USERPROFILE\.local\bin"
    if (Test-Path $uvPath) {
        $env:Path = "$uvPath;$env:Path"
    }
    # Also check cargo bin path
    $cargoPath = "$env:USERPROFILE\.cargo\bin"
    if (Test-Path $cargoPath) {
        $env:Path = "$cargoPath;$env:Path"
    }
    Write-Success "uv installed"
} else {
    Write-Success "uv already installed: $(uv --version)"
}

# Create virtual environment and install dependencies using uv
Write-Host "Creating virtual environment and installing dependencies with uv..."
if (-not (Test-Path ".venv")) {
    & uv venv .venv
    Write-Success "Virtual environment created"
} else {
    Write-Success "Virtual environment already exists"
}

# Install dependencies using uv (specify the venv python)
& uv pip install --python ".venv\Scripts\python.exe" -e ".[dev,http]"
Write-Success "Python dependencies installed"

# Set paths for later use
$venvPython = "$serverPath\.venv\Scripts\python.exe"
$dwsimMcpCli = "$serverPath\.venv\Scripts\dwsim-mcp.exe"

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

# Build scripts expect config at DwsimWorker/dwsim.config.json
# CLI creates it at dwsim_worker/dwsim.config.json
# We need to create/update at the build location
$configPath = "$workerPath\DwsimWorker\dwsim.config.json"
$cliConfigPath = "$workerPath\dwsim.config.json"

# DWSIM binaries path (downloaded by CLI)
$dwsimBinPath = "$workerPath\dwsim_binaries\x64\Debug"

# Find MSBuild - check multiple common locations
$msbuildPath = ""
$msbuildCandidates = @(
    # VS 2022 Build Tools (most common for servers)
    "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe",
    # VS 2022 editions
    "${env:ProgramFiles}\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe",
    "${env:ProgramFiles}\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe",
    "${env:ProgramFiles}\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe",
    # VS 2019 Build Tools
    "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2019\BuildTools\MSBuild\Current\Bin\MSBuild.exe",
    # VS 2019 editions
    "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2019\Professional\MSBuild\Current\Bin\MSBuild.exe",
    "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2019\Enterprise\MSBuild\Current\Bin\MSBuild.exe",
    "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe"
)

foreach ($candidate in $msbuildCandidates) {
    if (Test-Path $candidate) {
        $msbuildPath = $candidate
        Write-Host "Found MSBuild at: $msbuildPath"
        break
    }
}

# Fallback: try vswhere if no candidate found
if (-not $msbuildPath) {
    $vsWhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path $vsWhere) {
        $foundPath = & $vsWhere -latest -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe" 2>$null | Select-Object -First 1
        if ($foundPath -and (Test-Path $foundPath)) {
            $msbuildPath = $foundPath
            Write-Host "Found MSBuild via vswhere: $msbuildPath"
        }
    }
}

if (-not $msbuildPath) {
    Write-Error "Could not find MSBuild.exe! Install Visual Studio Build Tools."
    exit 1
}

# Create config file for build scripts (at DwsimWorker/dwsim.config.json)
# This is separate from CLI config - build scripts need it here
$config = @{
    dwsim_path = ($dwsimBinPath -replace '\\', '/')
    msbuild_path = ($msbuildPath -replace '\\', '/')
}
$config | ConvertTo-Json -Depth 2 | Set-Content -Path $configPath
Write-Success "Created config at $configPath"
Write-Host "  dwsim_path: $dwsimBinPath"
Write-Host "  msbuild_path: $msbuildPath"

# Also copy to CLI location if it doesn't exist (for dwsim-mcp doctor)
if (-not (Test-Path $cliConfigPath)) {
    $config | ConvertTo-Json -Depth 2 | Set-Content -Path $cliConfigPath
    Write-Host "  Also created at $cliConfigPath"
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
set PYTHONPATH=$repoPath;$serverPath
echo Starting DWSIM MCP Server (HTTP mode on port 8000)...
dwsim-mcp run --transport streamable-http --port 8000
"@
Set-Content -Path "$InstallPath\start-http.bat" -Value $startHttpScript

# Stdio mode start script
$startStdioScript = @"
@echo off
cd /d "$serverPath"
call .venv\Scripts\activate.bat
set PYTHONPATH=$repoPath;$serverPath
echo Starting DWSIM MCP Server (stdio mode)...
dwsim-mcp run
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
  dwsim-mcp run --transport streamable-http --port 8000

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
