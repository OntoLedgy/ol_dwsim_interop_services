@echo off
REM Build script for DwsimWorker (.NET Framework 4.8)
REM ================================================
REM Configuration - MSBuild is auto-detected via find-msbuild.ps1
REM ================================================
REM The script attempts to find MSBuild using:
REM 1. dwsim.config.json msbuild_path setting (if specified)
REM 2. vswhere.exe (VS 2017+)
REM 3. Common installation paths
REM 4. Windows Registry scan
REM 5. Filesystem search (C: and D: drives)
REM
REM To override, add "msbuild_path" to DwsimWorker\dwsim.config.json:
REM {
REM   "dwsim_path": "...",
REM   "msbuild_path": "D:/Apps/Visual Studio/MSBuild/Current/Bin/MSBuild.exe"
REM }
REM ================================================

REM Find MSBuild using PowerShell script
echo Locating MSBuild.exe...
for /f "usebackq delims=" %%i in (`powershell.exe -ExecutionPolicy Bypass -File "%~dp0find-msbuild.ps1"`) do (
    set MSBUILD_PATH=%%i
)

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo MSBuild detection failed. See error above.
    exit /b 1
)

REM ================================================
REM Build Configuration
REM ================================================
set SOLUTION_FILE=DwsimWorker.sln
set CONFIGURATION=Debug
set BUILD_TARGETS=Restore,Build

REM ================================================
REM Validate MSBuild path
REM ================================================
if not defined MSBUILD_PATH (
    echo ERROR: Could not find MSBuild.exe
    echo.
    echo Please either:
    echo   1. Install Visual Studio 2019/2022 with .NET desktop development workload
    echo   2. Install Visual Studio Build Tools
    echo   3. Edit this script and set MSBUILD_PATH manually
    echo.
    exit /b 1
)

if not exist "%MSBUILD_PATH%" (
    echo ERROR: MSBuild not found at: %MSBUILD_PATH%
    echo Please edit this script and set the correct MSBUILD_PATH
    exit /b 1
)

REM ================================================
REM Setup DWSIM Binaries
REM ================================================
echo.
echo Step 1: Setting up DWSIM binaries...
echo ================================================
call "%~dp0setup-dwsim-binaries.bat"
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo SETUP FAILED - Cannot proceed with build
    exit /b %ERRORLEVEL%
)

REM ================================================
REM Build
REM ================================================
echo.
echo Step 2: Building solution...
echo ================================================
echo Using MSBuild: %MSBUILD_PATH%
echo Building: %SOLUTION_FILE% [%CONFIGURATION%]
echo.

"%MSBUILD_PATH%" %SOLUTION_FILE% /p:Configuration=%CONFIGURATION% /t:%BUILD_TARGETS%

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo BUILD FAILED with error code %ERRORLEVEL%
    exit /b %ERRORLEVEL%
)

echo.
echo BUILD SUCCEEDED
