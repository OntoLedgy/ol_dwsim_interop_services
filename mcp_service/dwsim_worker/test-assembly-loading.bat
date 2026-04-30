REM SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
REM
REM SPDX-License-Identifier: AGPL-3.0-or-later

@echo off
REM ====================================================================
REM DWSIM Worker - Assembly Loading Test Script
REM ====================================================================
REM
REM This script tests DWSIM assembly loading by running DwsimWorker.exe
REM and checking the exit code.
REM
REM Exit Codes:
REM   0 - Success: All assemblies loaded and validated
REM   1 - Load Failure: Assembly files not found or failed to load
REM   2 - Validation Failure: Assemblies loaded but validation failed
REM   3 - Configuration Error: Invalid configuration or timeout
REM
REM Usage:
REM   test-assembly-loading.bat                    (uses default paths)
REM   test-assembly-loading.bat "C:\Custom\Path"   (uses custom DWSIM path)
REM
REM ====================================================================

setlocal enabledelayedexpansion

echo.
echo ====================================================================
echo   DWSIM Worker - Assembly Loading Test
echo ====================================================================
echo.

REM Check if custom DWSIM path was provided as argument
if not "%~1"=="" (
    set "DWSIM_PATH=%~1"
    echo Using custom DWSIM path: !DWSIM_PATH!
    echo.
) else (
    REM Check if DWSIM_PATH environment variable is set
    if defined DWSIM_PATH (
        echo Using DWSIM_PATH environment variable: !DWSIM_PATH!
        echo.
    ) else (
        echo No DWSIM_PATH set. Will use default paths or App.config.
        echo Tip: Set DWSIM_PATH or provide path as argument:
        echo   test-assembly-loading.bat "C:\Program Files\DWSIM"
        echo.
    )
)

REM Determine the path to DwsimWorker.exe
set "SCRIPT_DIR=%~dp0"
set "WORKER_EXE=%SCRIPT_DIR%DwsimWorker\bin\Release\DwsimWorker.exe"

REM Check if Release build exists, otherwise try Debug
if not exist "%WORKER_EXE%" (
    set "WORKER_EXE=%SCRIPT_DIR%DwsimWorker\bin\Debug\DwsimWorker.exe"
)

REM Check if executable exists
if not exist "%WORKER_EXE%" (
    echo ERROR: DwsimWorker.exe not found!
    echo.
    echo Expected locations:
    echo   %SCRIPT_DIR%DwsimWorker\bin\Release\DwsimWorker.exe
    echo   %SCRIPT_DIR%DwsimWorker\bin\Debug\DwsimWorker.exe
    echo.
    echo Please build the project first using:
    echo   msbuild DwsimWorker.sln /p:Configuration=Release
    echo.
    pause
    exit /b 1
)

echo Found DwsimWorker.exe: %WORKER_EXE%
echo.
echo --------------------------------------------------------------------
echo Starting assembly loading test...
echo --------------------------------------------------------------------
echo.

REM Run DwsimWorker.exe and capture exit code
"%WORKER_EXE%"
set EXITCODE=%ERRORLEVEL%

echo.
echo --------------------------------------------------------------------
echo Test completed
echo --------------------------------------------------------------------
echo.

REM Interpret exit code
if %EXITCODE% EQU 0 (
    echo [SUCCESS] Exit Code: %EXITCODE%
    echo All DWSIM assemblies loaded and validated successfully.
    echo The worker is ready for operation.
    echo.
) else if %EXITCODE% EQU 1 (
    echo [FAILURE] Exit Code: %EXITCODE% - Assembly Load Failure
    echo.
    echo Assembly files were not found or failed to load.
    echo.
    echo Troubleshooting steps:
    echo   1. Set DWSIM_PATH environment variable:
    echo      set DWSIM_PATH=C:\Program Files\DWSIM
    echo.
    echo   2. Or uncomment DwsimPath in App.config:
    echo      ^<add key="DwsimPath" value="C:\Your\Path\To\DWSIM" /^>
    echo.
    echo   3. Install DWSIM from https://dwsim.org if not installed
    echo.
    echo   4. Check logs\dwsim-worker-*.txt for detailed error messages
    echo.
) else if %EXITCODE% EQU 2 (
    echo [FAILURE] Exit Code: %EXITCODE% - Validation Failure
    echo.
    echo Assemblies loaded but validation failed.
    echo.
    echo Possible causes:
    echo   - Corrupted DWSIM installation
    echo   - Incompatible DWSIM version
    echo   - Missing dependencies
    echo.
    echo Troubleshooting steps:
    echo   1. Reinstall DWSIM
    echo   2. Verify .NET Framework 4.8 is installed
    echo   3. Check logs\dwsim-worker-*.txt for validation error details
    echo.
) else if %EXITCODE% EQU 3 (
    echo [FAILURE] Exit Code: %EXITCODE% - Configuration Error
    echo.
    echo Invalid configuration or timeout during loading.
    echo.
    echo Troubleshooting steps:
    echo   1. Review App.config for configuration errors
    echo   2. Check if assembly loading timeout is too short
    echo   3. Check logs\dwsim-worker-*.txt for details
    echo.
) else (
    echo [ERROR] Exit Code: %EXITCODE% - Unexpected Error
    echo.
    echo An unexpected error occurred.
    echo Check logs\dwsim-worker-*.txt for details.
    echo.
)

echo ====================================================================
echo.

REM Keep window open if run from explorer
if "%~1"=="" (
    pause
)

endlocal
exit /b %EXITCODE%
