@echo off
REM Build script for DwsimWorker (.NET Framework 4.8)
REM ================================================
REM Configuration - Adjust these paths for your environment
REM ================================================

REM Option 1: Use vswhere to auto-detect Visual Studio (recommended)
REM Option 2: Set MSBUILD_PATH manually if auto-detect fails

REM Try to find MSBuild using vswhere (works for VS 2017+)
set VSWHERE_PATH=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe

if exist "%VSWHERE_PATH%" (
    for /f "usebackq tokens=*" %%i in (`"%VSWHERE_PATH%" -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe`) do (
        set MSBUILD_PATH=%%i
    )
)

REM Fallback: Check common Visual Studio installation paths
if not defined MSBUILD_PATH (
    REM VS 2022 Professional/Enterprise/Community
    if exist "%ProgramFiles%\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe" (
        set "MSBUILD_PATH=%ProgramFiles%\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe"
    ) else if exist "%ProgramFiles%\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe" (
        set "MSBUILD_PATH=%ProgramFiles%\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe"
    ) else if exist "%ProgramFiles%\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" (
        set "MSBUILD_PATH=%ProgramFiles%\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"
    REM VS 2019 Professional/Enterprise/Community
    ) else if exist "%ProgramFiles(x86)%\Microsoft Visual Studio\2019\Professional\MSBuild\Current\Bin\MSBuild.exe" (
        set "MSBUILD_PATH=%ProgramFiles(x86)%\Microsoft Visual Studio\2019\Professional\MSBuild\Current\Bin\MSBuild.exe"
    ) else if exist "%ProgramFiles(x86)%\Microsoft Visual Studio\2019\Enterprise\MSBuild\Current\Bin\MSBuild.exe" (
        set "MSBUILD_PATH=%ProgramFiles(x86)%\Microsoft Visual Studio\2019\Enterprise\MSBuild\Current\Bin\MSBuild.exe"
    ) else if exist "%ProgramFiles(x86)%\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe" (
        set "MSBUILD_PATH=%ProgramFiles(x86)%\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe"
    REM Build Tools (standalone MSBuild)
    ) else if exist "%ProgramFiles(x86)%\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe" (
        set "MSBUILD_PATH=%ProgramFiles(x86)%\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
    ) else if exist "%ProgramFiles(x86)%\Microsoft Visual Studio\2019\BuildTools\MSBuild\Current\Bin\MSBuild.exe" (
        set "MSBUILD_PATH=%ProgramFiles(x86)%\Microsoft Visual Studio\2019\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
    )
)

REM ================================================
REM Manual override - Uncomment and set your path if auto-detect fails
REM ================================================
REM set "MSBUILD_PATH=D:\Apps\Microsoft Visual Studio\18\Professional\MSBuild\Current\Bin\MSBuild.exe"

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
REM Build
REM ================================================
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
