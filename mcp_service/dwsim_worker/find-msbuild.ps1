# SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
#
# SPDX-License-Identifier: AGPL-3.0-or-later

# ============================================================================
# Find MSBuild.exe - Auto-detect or read from config
# ============================================================================
# This script finds MSBuild.exe using multiple strategies:
# 1. Check dwsim.config.json for msbuild_path
# 2. Use vswhere.exe (VS 2017+)
# 3. Check common installation paths
# 4. Scan Windows Registry for VS installations
# ============================================================================

$ErrorActionPreference = "Stop"

$ConfigFile = "DwsimWorker\dwsim.config.json"

# Strategy 1: Check config file
if (Test-Path $ConfigFile) {
    try {
        $config = Get-Content $ConfigFile -Raw | ConvertFrom-Json
        if ($config.msbuild_path -and (Test-Path $config.msbuild_path)) {
            # Only output the path - no other text (batch file captures stdout)
            Write-Output $config.msbuild_path
            exit 0
        }
    } catch {
        # Config exists but couldn't be parsed or doesn't have msbuild_path
    }
}

# Strategy 2: Use vswhere.exe (VS 2017+)
$vswherePath = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
if (Test-Path $vswherePath) {
    try {
        $msbuildPath = & $vswherePath -latest -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe" | Select-Object -First 1
        if ($msbuildPath -and (Test-Path $msbuildPath)) {
            Write-Host "Found MSBuild via vswhere: $msbuildPath" -ForegroundColor Green
            Write-Output $msbuildPath
            exit 0
        }
    } catch {
        # vswhere failed, continue to next strategy
    }
}

# Strategy 3: Check common installation paths
$commonPaths = @(
    # VS 2022
    "${env:ProgramFiles}\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe",
    "${env:ProgramFiles}\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe",
    "${env:ProgramFiles}\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe",
    # VS 2019
    "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2019\Professional\MSBuild\Current\Bin\MSBuild.exe",
    "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2019\Enterprise\MSBuild\Current\Bin\MSBuild.exe",
    "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe",
    # Build Tools
    "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe",
    "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2019\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
)

foreach ($path in $commonPaths) {
    if (Test-Path $path) {
        Write-Host "Found MSBuild at common path: $path" -ForegroundColor Green
        Write-Output $path
        exit 0
    }
}

# Strategy 4: Scan Windows Registry for VS installations
Write-Host "Scanning registry for Visual Studio installations..." -ForegroundColor Gray
try {
    # VS 2017+ uses this registry location
    $vsWhere = Get-ItemProperty -Path "HKLM:\SOFTWARE\WOW6432Node\Microsoft\VisualStudio\SxS\VS7" -ErrorAction SilentlyContinue

    if ($vsWhere) {
        # Get all VS version properties (15.0, 16.0, 17.0, etc.)
        $vsVersions = $vsWhere.PSObject.Properties | Where-Object { $_.Name -match '^\d+\.\d+$' } | Sort-Object Name -Descending

        foreach ($version in $vsVersions) {
            $vsPath = $version.Value
            # Try multiple MSBuild locations within the VS installation
            $msbuildLocations = @(
                "$vsPath\MSBuild\Current\Bin\MSBuild.exe",
                "$vsPath\MSBuild\15.0\Bin\MSBuild.exe",
                "$vsPath\MSBuild\16.0\Bin\MSBuild.exe",
                "$vsPath\MSBuild\17.0\Bin\MSBuild.exe"
            )

            foreach ($location in $msbuildLocations) {
                if (Test-Path $location) {
                    Write-Host "Found MSBuild via registry: $location" -ForegroundColor Green
                    Write-Output $location
                    exit 0
                }
            }
        }
    }
} catch {
    # Registry scan failed, continue
}

# Strategy 5: Try to find MSBuild anywhere on common drives
Write-Host "Searching for MSBuild on common drives (this may take a moment)..." -ForegroundColor Gray
$drives = @("C:", "D:")
foreach ($drive in $drives) {
    if (Test-Path $drive) {
        $found = Get-ChildItem -Path "$drive\*Visual Studio*" -Filter "MSBuild.exe" -Recurse -ErrorAction SilentlyContinue -Depth 6 |
            Where-Object { $_.FullName -match "MSBuild\\.*\\Bin\\MSBuild\.exe$" } |
            Select-Object -First 1

        if ($found) {
            Write-Host "Found MSBuild via filesystem search: $($found.FullName)" -ForegroundColor Green
            Write-Output $found.FullName
            exit 0
        }
    }
}

# Not found
Write-Host "ERROR: Could not find MSBuild.exe" -ForegroundColor Red
Write-Host ""
Write-Host "Please either:" -ForegroundColor Yellow
Write-Host "  1. Install Visual Studio 2019/2022 with .NET desktop development workload" -ForegroundColor Yellow
Write-Host "  2. Add 'msbuild_path' to DwsimWorker\dwsim.config.json:" -ForegroundColor Yellow
Write-Host '     {' -ForegroundColor Gray
Write-Host '       "dwsim_path": "...",'-ForegroundColor Gray
Write-Host '       "msbuild_path": "D:/Apps/Visual Studio/MSBuild/Current/Bin/MSBuild.exe"' -ForegroundColor Gray
Write-Host '     }' -ForegroundColor Gray
Write-Host ""
exit 1
