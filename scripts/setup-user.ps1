<#
.SYNOPSIS
    Phase 2: Setup DWSIM MCP Server (NO Administrator required)
    Run this after setup-prerequisites.ps1

.DESCRIPTION
    This script can be run as a normal user. It:
    - Clones the repository
    - Sets up Python virtual environment
    - Downloads DWSIM binaries
    - Builds DwsimWorker
    - Configures the server

.PARAMETER RepoUrl
    Git repository URL (default: git@github.com:OntoLedgy/dwsim_interop_services)

.PARAMETER InstallPath
    Base installation path (default: C:\DwsimMcp)

.EXAMPLE
    # Use defaults:
    .\setup-user.ps1

    # Or specify custom repo:
    .\setup-user.ps1 -RepoUrl "https://github.com/your-org/dwsim_interop_services.git"
#>

param(
    [string]$RepoUrl = "git@github.com:OntoLedgy/dwsim_interop_services",
    [string]$InstallPath = "C:\DwsimMcp"
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

function Write-Step { param($msg) Write-Host "`n=== $msg ===" -ForegroundColor Cyan }
function Write-Success { param($msg) Write-Host "[OK] $msg" -ForegroundColor Green }
function Write-Warning { param($msg) Write-Host "[WARN] $msg" -ForegroundColor Yellow }

Write-Host @"
=========================================
  DWSIM MCP Server - User Setup
  (Phase 2 - No Admin Required)
=========================================
Install Path: $InstallPath
Repository:   $RepoUrl
=========================================
"@ -ForegroundColor Magenta

# Verify prerequisites
Write-Step "Verifying prerequisites"

$missing = @()
if (-not (Get-Command git -ErrorAction SilentlyContinue)) { $missing += "Git" }
if (-not (Get-Command python -ErrorAction SilentlyContinue)) { $missing += "Python" }

# Try multiple methods to find MSBuild
$msbuildPath = $null

# Method 1: Try vswhere
$vsWhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
if (Test-Path $vsWhere) {
    $msbuildPath = & $vsWhere -latest -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe" 2>$null | Select-Object -First 1
}

# Method 2: Try known paths directly
if (-not $msbuildPath) {
    $knownPaths = @(
        "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe",
        "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\amd64\MSBuild.exe",
        "C:\Program Files\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe",
        "C:\Program Files (x86)\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe",
        "C:\Program Files (x86)\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe",
        "C:\Program Files (x86)\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"
    )
    foreach ($path in $knownPaths) {
        if (Test-Path $path) {
            $msbuildPath = $path
            break
        }
    }
}

if (-not $msbuildPath) { $missing += "MSBuild" }

if ($missing.Count -gt 0) {
    Write-Host "Missing prerequisites: $($missing -join ', ')" -ForegroundColor Red
    Write-Host "Run setup-prerequisites.ps1 as Administrator first." -ForegroundColor Yellow
    exit 1
}
Write-Success "All prerequisites found"

# Create install directory if it doesn't exist
if (-not (Test-Path $InstallPath)) {
    try {
        New-Item -ItemType Directory -Path $InstallPath -Force | Out-Null
        Write-Success "Created install directory: $InstallPath"
    } catch {
        Write-Host "Cannot create install path: $InstallPath" -ForegroundColor Red
        Write-Host "Either create it manually or run setup-prerequisites.ps1 as Administrator." -ForegroundColor Yellow
        exit 1
    }
}

# Test write access
$testFile = "$InstallPath\.write-test-$(Get-Random)"
try {
    "test" | Set-Content $testFile
    Remove-Item $testFile
    Write-Success "Write access to $InstallPath confirmed"
} catch {
    Write-Host "No write access to $InstallPath" -ForegroundColor Red
    Write-Host "Run setup-prerequisites.ps1 as Administrator to fix permissions." -ForegroundColor Yellow
    exit 1
}

Set-Location $InstallPath

# ============================================
# STEP 1: Clone Repository
# ============================================
Write-Step "Cloning repository"
$repoPath = "$InstallPath\dwsim_interop_services"

if (-not (Test-Path "$repoPath\.git")) {
    git clone $RepoUrl dwsim_interop_services
    Write-Success "Repository cloned to $repoPath"
} else {
    Write-Success "Repository already exists"
    Set-Location $repoPath
    git pull
    Write-Success "Repository updated"
}

# ============================================
# STEP 2: Setup Python Environment
# ============================================
Write-Step "Setting up Python environment"
$serverPath = "$repoPath\mcp_service\server"
$workerPath = "$repoPath\mcp_service\dwsim_worker"
Set-Location $serverPath

# Install uv if needed
if (-not (Get-Command uv -ErrorAction SilentlyContinue)) {
    Write-Host "Installing uv package manager..."
    Invoke-WebRequest -Uri "https://astral.sh/uv/install.ps1" -OutFile "$env:TEMP\install-uv.ps1"
    & powershell -ExecutionPolicy Bypass -File "$env:TEMP\install-uv.ps1"
    Remove-Item "$env:TEMP\install-uv.ps1" -Force -ErrorAction SilentlyContinue

    # Add uv to PATH for this session
    $uvPaths = @("$env:USERPROFILE\.local\bin", "$env:USERPROFILE\.cargo\bin")
    foreach ($p in $uvPaths) {
        if (Test-Path $p) { $env:Path = "$p;$env:Path" }
    }
    Write-Success "uv installed"
} else {
    Write-Success "uv already installed"
}

# Create venv and install dependencies
if (-not (Test-Path ".venv")) {
    & uv venv .venv
    Write-Success "Virtual environment created"
}

& uv pip install --python ".venv\Scripts\python.exe" -e ".[dev,http]"
Write-Success "Python dependencies installed"

$dwsimMcpCli = "$serverPath\.venv\Scripts\dwsim-mcp.exe"

# ============================================
# STEP 3: Download DWSIM Binaries
# ============================================
Write-Step "Downloading DWSIM binaries via CLI"
$env:PYTHONPATH = "$repoPath;$serverPath"

& $dwsimMcpCli setup --download

if ($LASTEXITCODE -eq 0) {
    Write-Success "DWSIM binaries downloaded"
} else {
    Write-Warning "DWSIM download may have had issues - check output above"
}

# ============================================
# STEP 4: Configure MSBuild Path
# ============================================
Write-Step "Configuring build paths"

# The CLI's --download flag now creates dwsim.config.json with relative path
# We only need to add msbuild_path if not already present

$configPath = "$workerPath\dwsim.config.json"

# Find MSBuild
$msbuildPath = & $vsWhere -latest -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe" 2>$null | Select-Object -First 1

# Read existing config (created by CLI) and add msbuild_path
if (Test-Path $configPath) {
    $config = Get-Content $configPath -Raw | ConvertFrom-Json
    $config | Add-Member -NotePropertyName "msbuild_path" -NotePropertyValue ($msbuildPath -replace '\\', '/') -Force
    $config | ConvertTo-Json -Depth 2 | Set-Content -Path $configPath
    Write-Success "Updated config with msbuild_path at $configPath"
} else {
    # Create config with relative path (fallback if CLI didn't create it)
    $config = @{
        dwsim_path = "./dwsim_binaries/x64/Debug"
        msbuild_path = ($msbuildPath -replace '\\', '/')
    }
    $config | ConvertTo-Json -Depth 2 | Set-Content -Path $configPath
    Write-Success "Created config at $configPath"
}

# Create config for DwsimWorker subfolder (with adjusted relative path)
$dwsimWorkerConfig = @{
    dwsim_path = "../dwsim_binaries/x64/Debug"
    msbuild_path = ($msbuildPath -replace '\\', '/')
}
$dwsimWorkerConfig | ConvertTo-Json -Depth 2 | Set-Content -Path "$workerPath\DwsimWorker\dwsim.config.json"
Write-Host "  Also created DwsimWorker\dwsim.config.json (with adjusted relative path)"

# ============================================
# STEP 5: Build DwsimWorker
# ============================================
Write-Step "Building DwsimWorker"
Set-Location $workerPath

$buildResult = & cmd /c "build.bat 2>&1"
$buildResult | ForEach-Object { Write-Host $_ }

if ($LASTEXITCODE -eq 0) {
    Write-Success "DwsimWorker built successfully"
} else {
    Write-Warning "Build may have had issues - check output above"
}

# ============================================
# STEP 6: Create Environment File
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

# Create OAuth configuration template
$envAuthContent = @"
# OAuth Configuration (Clerk)
# Copy this file and fill in your Clerk credentials
# The start-http.bat script will load this automatically

# Enable/disable authentication (set to true for production)
DWSIM_AUTH_ENABLED=false

# Clerk Configuration
# Get these values from your Clerk dashboard
CLERK_ISSUER_URL=https://your-app.clerk.accounts.dev
CLERK_AUDIENCE=dwsim-mcp
CLERK_REQUIRED_SCOPES=user

# Optional: Override JWKS URL (normally derived from issuer)
# CLERK_JWKS_URL=
"@

Set-Content -Path "$serverPath\.env.auth.template" -Value $envAuthContent
Write-Success "Created .env.auth.template (copy to .env.auth and configure for OAuth)"

# Create simulations directory
$simPath = "$InstallPath\simulations"
if (-not (Test-Path $simPath)) {
    New-Item -ItemType Directory -Path $simPath -Force | Out-Null
    Write-Success "Created simulations directory"
}

# ============================================
# STEP 7: Copy Convenience Scripts
# ============================================
Write-Step "Copying convenience scripts"

$scriptsToInstall = @("start-http.bat", "start-stdio.bat", "diagnose.bat", "test-server.bat")
foreach ($script in $scriptsToInstall) {
    $src = "$repoPath\scripts\$script"
    if (Test-Path $src) {
        Copy-Item -Path $src -Destination "$InstallPath\$script" -Force
        Write-Host "  Copied $script"
    }
}
Write-Success "Scripts installed"

# ============================================
# STEP 8: Run Diagnostics
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

Quick Start:
  $InstallPath\start-http.bat

OAuth Setup (Optional):
  1. Copy .env.auth.template to .env.auth
  2. Set DWSIM_AUTH_ENABLED=true
  3. Configure CLERK_ISSUER_URL with your Clerk app URL
  4. Restart the server
  See: docs/mcp/deployment-guide.md

Manual Start:
  cd "$serverPath"
  .\.venv\Scripts\Activate.ps1
  `$env:DWSIM_TRANSPORT_MODE="streamable-http"
  dwsim-mcp run

Test:
  $InstallPath\test-server.bat

"@ -ForegroundColor Green
