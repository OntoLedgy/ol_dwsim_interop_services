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

# DWSIM MCP Server - Setup Script for Beta Testers
# Run this script from the repository root

param(
    [string]$DwsimPath = "",
    [switch]$DownloadDwsim = $false,
    [string]$DwsimReleaseUrl = "https://github.com/OntoLedgy/dwsim/releases/download/v9.0.5-mcp/dwsim_binaries.zip",
    [switch]$FetchPrebuiltDll,
    [switch]$BuildFromSource,
    [string]$WorkerReleaseUrl = "https://github.com/OntoLedgy/ol_dwsim_interop_services/releases/latest/download/DwsimWorker.zip"
)

Write-Host "====================================" -ForegroundColor Cyan
Write-Host "DWSIM MCP Server Setup" -ForegroundColor Cyan
Write-Host "====================================" -ForegroundColor Cyan
Write-Host ""

if ($FetchPrebuiltDll -and $BuildFromSource) {
    Write-Host "Error: -FetchPrebuiltDll and -BuildFromSource are mutually exclusive. Choose one." -ForegroundColor Red
    exit 1
}

if (-not $FetchPrebuiltDll.IsPresent -and -not $BuildFromSource.IsPresent) {
    $FetchPrebuiltDll = $true
}

# Check if running from repo root
if (-not (Test-Path "mcp_service")) {
    Write-Host "Error: Please run this script from the repository root directory" -ForegroundColor Red
    exit 1
}

# Preflight: .NET Framework 4.8 and MSBuild (Windows)
Write-Host "Checking .NET Framework prerequisites..." -ForegroundColor Yellow

$netFx48Release = 528040
$netFxKey = "HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full"
$netFxInstalled = $false
try {
    $release = (Get-ItemProperty -Path $netFxKey -Name Release -ErrorAction Stop).Release
    if ($release -ge $netFx48Release) { $netFxInstalled = $true }
} catch { }

$msbuildFound = $false
$vsWhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
if (Test-Path $vsWhere) {
    $msbuildPath = & $vsWhere -latest -products * -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe 2>$null | Select-Object -First 1
    if ($msbuildPath) { $msbuildFound = $true }
}

if (-not $netFxInstalled -or -not $msbuildFound) {
    Write-Host "Missing .NET Framework 4.8 and/or MSBuild." -ForegroundColor Yellow
    Write-Host "Install the .NET Framework 4.8 Developer Pack (includes Targeting Pack):" -ForegroundColor Yellow
    Write-Host "  https://dotnet.microsoft.com/download/dotnet-framework/net48" -ForegroundColor Cyan
    Write-Host "Install Visual Studio Build Tools with the '.NET desktop development' workload:" -ForegroundColor Yellow
    Write-Host "  https://visualstudio.microsoft.com/downloads/" -ForegroundColor Cyan
    Write-Host ""
} else {
    Write-Host ".NET Framework 4.8 and MSBuild detected." -ForegroundColor Green
}

# Create target directories
Write-Host "Creating directory structure..." -ForegroundColor Yellow

$workerDir = "mcp_service\dwsim_worker\DwsimWorker\bin\Debug"
$dwsimBinDir = "mcp_service\dwsim_worker\dwsim_binaries\x64\Debug"

New-Item -ItemType Directory -Path $workerDir -Force | Out-Null
New-Item -ItemType Directory -Path $dwsimBinDir -Force | Out-Null

Write-Host "Created: $workerDir" -ForegroundColor Green
Write-Host "Created: $dwsimBinDir" -ForegroundColor Green

function Copy-LocalDwsimWorkerFallback {
    param(
        [string]$SourceDir,
        [string]$DestinationDir
    )

    if (-not (Test-Path $SourceDir)) {
        Write-Host "Warning: $SourceDir not found - you may need to build from source" -ForegroundColor Yellow
        Write-Host "Run: cd mcp_service\dwsim_worker && .\build.bat" -ForegroundColor Yellow
        return $false
    }

    Copy-Item "$SourceDir\*" $DestinationDir -Force
    Write-Host "Copied DwsimWorker DLLs from local prebuilt fallback" -ForegroundColor Green
    return $true
}

function Fetch-DwsimWorkerAsset {
    param(
        [string]$DestinationDir,
        [string]$ReleaseUrl,
        [string]$LocalFallbackDir
    )

    $targetDllPath = Join-Path $DestinationDir "DwsimWorker.dll"
    if (Test-Path $targetDllPath) {
        Write-Host "DwsimWorker.dll already present - skipping fetch/build" -ForegroundColor Green
        return $true
    }

    $zipPath = Join-Path $env:TEMP "DwsimWorker.zip"
    $extractDir = Join-Path $env:TEMP ("DwsimWorker_" + [guid]::NewGuid().ToString("N"))

    try {
        Write-Host "Fetching prebuilt DwsimWorker.zip from latest release..." -ForegroundColor Yellow
        Write-Host "Downloading from: $ReleaseUrl" -ForegroundColor Cyan
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        $ProgressPreference = 'SilentlyContinue'
        Invoke-WebRequest -Uri $ReleaseUrl -OutFile $zipPath -UseBasicParsing
        $ProgressPreference = 'Continue'

        New-Item -ItemType Directory -Path $extractDir -Force | Out-Null
        Expand-Archive -Path $zipPath -DestinationPath $extractDir -Force
        Copy-Item (Join-Path $extractDir "*") $DestinationDir -Force

        if (Test-Path $targetDllPath) {
            Write-Host "Fetched prebuilt DwsimWorker DLLs" -ForegroundColor Green
            return $true
        }

        throw "DwsimWorker.dll was not present after extracting $ReleaseUrl"
    } catch {
        $ProgressPreference = 'Continue'
        Write-Host "Warning: Failed to fetch prebuilt DwsimWorker asset - $_" -ForegroundColor Yellow
        Write-Host "Falling back to local prebuilt\DwsimWorker copy..." -ForegroundColor Yellow
        return (Copy-LocalDwsimWorkerFallback -SourceDir $LocalFallbackDir -DestinationDir $DestinationDir)
    } finally {
        if (Test-Path $zipPath) {
            Remove-Item $zipPath -Force -ErrorAction SilentlyContinue
        }
        if (Test-Path $extractDir) {
            Remove-Item $extractDir -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}

$distWorkerDir = "prebuilt\DwsimWorker"

if (-not $BuildFromSource) {
    Write-Host ""
    Write-Host "Fetching prebuilt DwsimWorker DLLs..." -ForegroundColor Yellow
    [void](Fetch-DwsimWorkerAsset -DestinationDir $workerDir -ReleaseUrl $WorkerReleaseUrl -LocalFallbackDir $distWorkerDir)
}

# Copy DwsimWorker DLLs from prebuilt folder
Write-Host ""
Write-Host "Copying DwsimWorker DLLs..." -ForegroundColor Yellow

$workerDllPath = Join-Path $workerDir "DwsimWorker.dll"
if (Test-Path $workerDllPath) {
    Write-Host "DwsimWorker.dll already present - skipping local copy" -ForegroundColor Green
}
elseif (Test-Path $distWorkerDir) {
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

# If no path/switch was provided, ask the user which option to use
if (-not $DwsimPath -and -not $DownloadDwsim) {
    Write-Host "Choose DWSIM binaries source:" -ForegroundColor Yellow
    Write-Host "  [1] Use prebuilt binaries (download)" -ForegroundColor Cyan
    Write-Host "  [2] Use a custom local DWSIM build" -ForegroundColor Cyan
    $choice = Read-Host "Enter 1 or 2"
    if ($choice -eq "2") {
        $DwsimPath = Read-Host "Enter full path to DWSIM binaries (e.g. D:\\path\\to\\DWSIM\\bin\\x64\\Debug)"
    } else {
        $DownloadDwsim = $true
    }
}

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

# Write DWSIM config with binaries path
Write-Host ""
Write-Host "Writing DWSIM config..." -ForegroundColor Yellow

$configPath = "mcp_service\dwsim_worker\dwsim.config.json"
try {
    $effectivePath = if ($DwsimPath) { $DwsimPath } else { $dwsimBinDir }
    $absDwsimBinDir = (Resolve-Path $effectivePath).Path -replace '\\', '/'
    $cfg = @{}
    if (Test-Path $configPath) {
        try {
            $cfg = Get-Content $configPath -Raw | ConvertFrom-Json
        } catch {
            $cfg = @{}
        }
    }
    $cfg.dwsim_path = $absDwsimBinDir
    $cfg | ConvertTo-Json -Depth 4 | Set-Content $configPath -Encoding UTF8
    Write-Host "Wrote: $configPath" -ForegroundColor Green
    Write-Host "  dwsim_path = $absDwsimBinDir" -ForegroundColor Gray
} catch {
    Write-Host "Warning: Could not write $configPath - $_" -ForegroundColor Yellow
}

# Update App.config files with absolute DwsimPath (for worker/assembly config)
Write-Host ""
Write-Host "Updating App.config with DwsimPath..." -ForegroundColor Yellow

$configFiles = @(
    "mcp_service\dwsim_worker\DwsimWorker\App.config",
    "mcp_service\dwsim_worker\DwsimWorker.Tests\App.config",
    "mcp_service\dwsim_worker\DwsimWorker\bin\Debug\DwsimWorker.dll.config"
)

foreach ($cfgFile in $configFiles) {
    if (-not (Test-Path $cfgFile)) { continue }
    try {
        [xml]$xml = Get-Content $cfgFile
        $appSettings = $xml.configuration.appSettings
        if (-not $appSettings) {
            $appSettings = $xml.CreateElement("appSettings")
            $xml.configuration.AppendChild($appSettings) | Out-Null
        }
        $dwsimPathSetting = $appSettings.add | Where-Object { $_.key -eq "DwsimPath" }
        if (-not $dwsimPathSetting) {
            $dwsimPathSetting = $xml.CreateElement("add")
            $dwsimPathSetting.SetAttribute("key", "DwsimPath")
            $dwsimPathSetting.SetAttribute("value", $absDwsimBinDir)
            $appSettings.AppendChild($dwsimPathSetting) | Out-Null
        } else {
            $dwsimPathSetting.value = $absDwsimBinDir
        }
        $xml.Save($cfgFile)
        Write-Host "  Updated: $cfgFile" -ForegroundColor Gray
    } catch {
        Write-Host "  Warning: Could not update $cfgFile - $_" -ForegroundColor Yellow
    }
}

# Set DWSIM_PATH for this session (helps immediate test runs)
$env:DWSIM_PATH = $absDwsimBinDir
Write-Host "Set DWSIM_PATH for current session: $absDwsimBinDir" -ForegroundColor Gray

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
