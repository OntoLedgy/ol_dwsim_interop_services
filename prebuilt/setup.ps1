# DWSIM MCP Server - Setup Script for Beta Testers
# Run this script from the repository root

param(
    [string]$DwsimPath = "",
    [switch]$DownloadDwsim = $false,
    [string]$DwsimReleaseUrl = "https://github.com/OntoLedgy/dwsim/releases/download/v9.0.5-mcp/dwsim_binaries.zip"
)

Write-Host "====================================" -ForegroundColor Cyan
Write-Host "DWSIM MCP Server Setup" -ForegroundColor Cyan
Write-Host "====================================" -ForegroundColor Cyan
Write-Host ""

# Check if running from repo root
if (-not (Test-Path "mcp_service")) {
    Write-Host "Error: Please run this script from the repository root directory" -ForegroundColor Red
    exit 1
}

# Create target directories
Write-Host "Creating directory structure..." -ForegroundColor Yellow

$workerDir = "mcp_service\dwsim_worker\DwsimWorker\bin\Debug"
$dwsimBinDir = "mcp_service\dwsim_worker\dwsim_binaries\x64\Debug"

New-Item -ItemType Directory -Path $workerDir -Force | Out-Null
New-Item -ItemType Directory -Path $dwsimBinDir -Force | Out-Null

Write-Host "Created: $workerDir" -ForegroundColor Green
Write-Host "Created: $dwsimBinDir" -ForegroundColor Green

# Copy DwsimWorker DLLs from prebuilt folder
Write-Host ""
Write-Host "Copying DwsimWorker DLLs..." -ForegroundColor Yellow

$distWorkerDir = "prebuilt\DwsimWorker"
if (Test-Path $distWorkerDir) {
    Copy-Item "$distWorkerDir\*" $workerDir -Force
    Write-Host "Copied DwsimWorker DLLs" -ForegroundColor Green
} else {
    Write-Host "Warning: prebuilt\DwsimWorker not found - you may need to build from source" -ForegroundColor Yellow
    Write-Host "Run: cd mcp_service\dwsim_worker && .\build.bat" -ForegroundColor Yellow
}

# Handle DWSIM binaries
Write-Host ""
Write-Host "Setting up DWSIM binaries..." -ForegroundColor Yellow
Write-Host ""
Write-Host "This MCP server requires DWSIM v9.0.5+ (includes headless UpdateInterface fix)" -ForegroundColor Cyan
Write-Host ""

# Check if binaries already exist
$existingFiles = Get-ChildItem $dwsimBinDir -ErrorAction SilentlyContinue
if ($existingFiles.Count -gt 100) {
    Write-Host "DWSIM binaries already present ($($existingFiles.Count) files)" -ForegroundColor Green
}
elseif ($DwsimPath) {
    # Use provided path (must be patched DWSIM!)
    Write-Host "Using DWSIM from: $DwsimPath" -ForegroundColor Yellow
    Write-Host "WARNING: Ensure this is the PATCHED build, not vanilla DWSIM!" -ForegroundColor Magenta
    
    if (-not (Test-Path $DwsimPath)) {
        Write-Host "Error: DWSIM path not found: $DwsimPath" -ForegroundColor Red
        exit 1
    }
    
    # Try to create symbolic link (requires admin on older Windows)
    try {
        if (Test-Path $dwsimBinDir) {
            Remove-Item $dwsimBinDir -Force -Recurse
        }
        cmd /c mklink /D $dwsimBinDir $DwsimPath 2>&1 | Out-Null
        if (Test-Path $dwsimBinDir) {
            Write-Host "Created symbolic link to DWSIM binaries" -ForegroundColor Green
        } else {
            throw "Link creation failed"
        }
    } catch {
        Write-Host "Could not create symbolic link (requires admin). Copying files instead..." -ForegroundColor Yellow
        New-Item -ItemType Directory -Path $dwsimBinDir -Force | Out-Null
        Copy-Item "$DwsimPath\*" $dwsimBinDir -Recurse -Force
        Write-Host "Copied DWSIM binaries" -ForegroundColor Green
    }
}
else {
    # Download from OntoLedgy/dwsim releases
    Write-Host "Downloading patched DWSIM binaries from OntoLedgy/dwsim releases..." -ForegroundColor Yellow
    
    $zipPath = "prebuilt\dwsim_binaries.zip"
    
    # Check if zip already exists
    if (-not (Test-Path $zipPath)) {
        Write-Host "Downloading from: $DwsimReleaseUrl" -ForegroundColor Cyan
        Write-Host "This is ~280MB, please wait..." -ForegroundColor Yellow
        
        try {
            # Use TLS 1.2 for GitHub
            [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
            
            # Download with progress
            $ProgressPreference = 'SilentlyContinue'  # Faster download
            Invoke-WebRequest -Uri $DwsimReleaseUrl -OutFile $zipPath -UseBasicParsing
            $ProgressPreference = 'Continue'
            
            Write-Host "Download complete!" -ForegroundColor Green
        }
        catch {
            Write-Host "Error downloading DWSIM binaries: $_" -ForegroundColor Red
            Write-Host ""
            Write-Host "Please download manually from:" -ForegroundColor Yellow
            Write-Host "  $DwsimReleaseUrl" -ForegroundColor Cyan
            Write-Host "And extract to: $dwsimBinDir" -ForegroundColor Yellow
            exit 1
        }
    } else {
        Write-Host "Using cached zip: $zipPath" -ForegroundColor Green
    }
    
    # Extract
    Write-Host "Extracting DWSIM binaries (this may take a minute)..." -ForegroundColor Yellow
    Expand-Archive -Path $zipPath -DestinationPath $dwsimBinDir -Force
    Write-Host "Extracted DWSIM binaries" -ForegroundColor Green
}

# Install Python dependencies
Write-Host ""
Write-Host "Installing Python dependencies..." -ForegroundColor Yellow

Push-Location "mcp_service\server"
try {
    uv sync
    Write-Host "Python dependencies installed" -ForegroundColor Green
} catch {
    Write-Host "Warning: uv not found. Install with: pip install uv" -ForegroundColor Yellow
    Write-Host "Then run: cd mcp_service\server && uv sync" -ForegroundColor Yellow
}
Pop-Location

# Generate MCP config
Write-Host ""
Write-Host "====================================" -ForegroundColor Cyan
Write-Host "Setup Complete!" -ForegroundColor Green
Write-Host "====================================" -ForegroundColor Cyan
Write-Host ""

$repoPath = (Get-Location).Path -replace '\\', '\\\\'
$serverPath = "$repoPath\\\\mcp_service\\\\server"

Write-Host "Add this to your VS Code settings.json:" -ForegroundColor Yellow
Write-Host ""
Write-Host @"
{
  "github.copilot.chat.mcpServers": {
    "dwsim": {
      "command": "uv",
      "args": ["run", "dwsim-mcp"],
      "cwd": "$serverPath",
      "env": {
        "PYTHONPATH": "$repoPath"
      }
    }
  }
}
"@ -ForegroundColor White

Write-Host ""
Write-Host "Or add to mcp.json at: $env:APPDATA\Code\User\mcp.json" -ForegroundColor Yellow
Write-Host ""
Write-Host "For Claude Desktop, add to: $env:APPDATA\Claude\claude_desktop_config.json" -ForegroundColor Yellow
Write-Host ""
Write-Host "See docs\resources\getting-started.md for detailed configuration options." -ForegroundColor Cyan
