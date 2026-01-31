# ============================================================================
# Setup DWSIM Binaries - Copy DWSIM assemblies to local repo folder
# ============================================================================
# This script copies DWSIM binaries from your DWSIM build location
# (specified in dwsim.config.json) to the local dwsim_binaries folder.
# This is needed for tests and to avoid dependency on external DWSIM paths.
# ============================================================================

$ErrorActionPreference = "Stop"

$ConfigFile = "DwsimWorker\dwsim.config.json"
$TargetDir = "dwsim_binaries\x64\Debug"

Write-Host ""
Write-Host "============================================================================" -ForegroundColor Cyan
Write-Host "DWSIM Binaries Setup" -ForegroundColor Cyan
Write-Host "============================================================================" -ForegroundColor Cyan
Write-Host ""

# Check if config file exists
if (-not (Test-Path $ConfigFile)) {
    Write-Host "ERROR: Configuration file not found: $ConfigFile" -ForegroundColor Red
    Write-Host ""
    Write-Host "Please create dwsim.config.json from dwsim.config.json.sample" -ForegroundColor Yellow
    Write-Host "and set the dwsim_path to your DWSIM build output directory." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Example:" -ForegroundColor Yellow
    Write-Host "{" -ForegroundColor Gray
    Write-Host '  "dwsim_path": "d:/S/C#/dwsim/DWSIM/bin/x64/Debug"' -ForegroundColor Gray
    Write-Host "}" -ForegroundColor Gray
    Write-Host ""
    exit 1
}

# Read and parse JSON config
Write-Host "Reading source path from $ConfigFile..." -ForegroundColor Gray
try {
    $config = Get-Content $ConfigFile -Raw | ConvertFrom-Json
    $sourcePath = $config.dwsim_path

    if ([string]::IsNullOrWhiteSpace($sourcePath)) {
        throw "dwsim_path is empty or not found in config"
    }

    # Convert forward slashes to backslashes for Windows
    $sourcePath = $sourcePath -replace '/', '\'

    # Handle relative paths - resolve relative to config file's directory
    if (-not [System.IO.Path]::IsPathRooted($sourcePath)) {
        $configDir = Split-Path -Parent (Resolve-Path $ConfigFile)
        $sourcePath = Join-Path $configDir $sourcePath | Resolve-Path
    }

} catch {
    Write-Host "ERROR: Could not read dwsim_path from $ConfigFile" -ForegroundColor Red
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
    Write-Host "Please ensure the config file contains:" -ForegroundColor Yellow
    Write-Host "{" -ForegroundColor Gray
    Write-Host '  "dwsim_path": "path/to/dwsim/DWSIM/bin/x64/Debug"' -ForegroundColor Gray
    Write-Host "}" -ForegroundColor Gray
    Write-Host ""
    exit 1
}

Write-Host "Source path: $sourcePath" -ForegroundColor Green
Write-Host ""

# Check if source directory exists
if (-not (Test-Path $sourcePath -PathType Container)) {
    Write-Host "ERROR: Source DWSIM directory does not exist: $sourcePath" -ForegroundColor Red
    Write-Host ""
    Write-Host "Please:" -ForegroundColor Yellow
    Write-Host "  1. Build DWSIM first in the sibling dwsim repository" -ForegroundColor Yellow
    Write-Host "  2. Update dwsim.config.json with the correct path" -ForegroundColor Yellow
    Write-Host ""
    exit 1
}

# Create target directory if it doesn't exist
if (-not (Test-Path $TargetDir)) {
    Write-Host "Creating target directory: $TargetDir" -ForegroundColor Gray
    New-Item -ItemType Directory -Path $TargetDir -Force | Out-Null
}

Write-Host "Copying DWSIM binaries..." -ForegroundColor Cyan
Write-Host "From: $sourcePath" -ForegroundColor Gray
Write-Host "To:   $TargetDir" -ForegroundColor Gray
Write-Host ""

# Use robocopy for efficient copying (only copies changed files)
# /E = copy subdirectories including empty ones
# /NJH = no job header
# /NJS = no job summary
# /NP = no progress
# /R:2 = retry 2 times
# /W:1 = wait 1 second between retries
$robocopyArgs = @(
    "`"$sourcePath`"",
    "`"$TargetDir`"",
    "/E",
    "/NJH",
    "/NJS",
    "/NP",
    "/R:2",
    "/W:1"
)

$robocopyCmd = "robocopy $($robocopyArgs -join ' ')"
Write-Host "Running: $robocopyCmd" -ForegroundColor DarkGray

# Execute robocopy
$process = Start-Process -FilePath "robocopy.exe" -ArgumentList $robocopyArgs -Wait -PassThru -NoNewWindow

# Robocopy exit codes: 0=no change, 1=files copied, 2=extra files/dirs, 3=files+extra
# 4=mismatches, 5=files+mismatches, 6=extra+mismatches, 7=all three
# 8+ = errors
$exitCode = $process.ExitCode
if ($exitCode -ge 8) {
    Write-Host ""
    Write-Host "ERROR: Failed to copy DWSIM binaries (exit code: $exitCode)" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "============================================================================" -ForegroundColor Green
Write-Host "DWSIM binaries copied successfully!" -ForegroundColor Green
Write-Host "============================================================================" -ForegroundColor Green
Write-Host ""

# Verify critical files exist
$missingFiles = $false
Write-Host "Verifying critical DWSIM assemblies..." -ForegroundColor Cyan

$criticalFiles = @(
    @{ Name = "DWSIM.Interfaces.dll"; Critical = $true },
    @{ Name = "DWSIM.Thermodynamics.dll"; Critical = $true },
    @{ Name = "DWSIM.SharedClasses.dll"; Critical = $true },
    @{ Name = "DWSIM.UnitOperations.dll"; Critical = $true },
    @{ Name = "CapeOpen.dll"; Critical = $false }
)

foreach ($file in $criticalFiles) {
    $filePath = Join-Path $TargetDir $file.Name
    if (Test-Path $filePath) {
        Write-Host "  [OK] $($file.Name)" -ForegroundColor Green
    } else {
        if ($file.Critical) {
            Write-Host "  [MISSING] $($file.Name)" -ForegroundColor Red
            $missingFiles = $true
        } else {
            Write-Host "  [WARNING] $($file.Name) (optional)" -ForegroundColor Yellow
        }
    }
}

Write-Host ""

if ($missingFiles) {
    Write-Host "ERROR: Some critical DWSIM assemblies are missing!" -ForegroundColor Red
    Write-Host "Please ensure DWSIM is built correctly at: $sourcePath" -ForegroundColor Yellow
    exit 1
}

Write-Host "All critical assemblies verified successfully!" -ForegroundColor Green
Write-Host ""

# Update configuration files with absolute path to dwsim_binaries
Write-Host "Updating configuration files..." -ForegroundColor Cyan
$absoluteDwsimPath = (Resolve-Path $TargetDir).Path.Replace('\', '/')

$configFiles = @(
    # Only update deployed configs, not source App.config (will be overwritten on rebuild)
    "DwsimWorker\bin\Debug\DwsimWorker.dll.config",
    "DwsimWorker\bin\Release\DwsimWorker.dll.config",
    "DwsimWorker\dwsim.config.json"  # Used by C# tests via TestConfiguration
)

foreach ($configFile in $configFiles) {
    if (Test-Path $configFile) {
        try {
            $content = Get-Content $configFile -Raw

            if ($configFile -like "*.json") {
                # Update JSON config
                $json = $content | ConvertFrom-Json
                $json.dwsim_path = $absoluteDwsimPath
                $content = $json | ConvertTo-Json
                Set-Content -Path $configFile -Value $content -NoNewline
                Write-Host "  Updated: $configFile" -ForegroundColor Gray
            } elseif ($configFile -like "*.config") {
                # Update XML config
                [xml]$xml = $content
                $dwsimPathSetting = $xml.configuration.appSettings.add | Where-Object { $_.key -eq "DwsimPath" }
                $dwsimDepsSetting = $xml.configuration.appSettings.add | Where-Object { $_.key -eq "DwsimDependencyPaths" }

                if ($dwsimPathSetting) {
                    $dwsimPathSetting.value = $absoluteDwsimPath
                    Write-Host "  Updated DwsimPath in: $configFile" -ForegroundColor Gray
                }

                if ($dwsimDepsSetting) {
                    $dwsimDepsSetting.value = $absoluteDwsimPath
                    Write-Host "  Updated DwsimDependencyPaths in: $configFile" -ForegroundColor Gray
                }

                $xml.Save((Resolve-Path $configFile))
            }
        } catch {
            Write-Host "  Warning: Could not update $configFile : $($_.Exception.Message)" -ForegroundColor Yellow
        }
    }
}

Write-Host ""
Write-Host "Configuration files updated with absolute path: $absoluteDwsimPath" -ForegroundColor Green
Write-Host ""

exit 0
