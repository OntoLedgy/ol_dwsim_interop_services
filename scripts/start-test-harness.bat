REM SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
REM
REM This file is part of the OntoLedgy Thermodynamics Architecture and is
REM dual-licensed:
REM
REM   1. Open source under the GNU Affero General Public License v3.0 or
REM      later (AGPL-3.0-or-later). See the LICENSE file in the repository
REM      root for the full licence text and NOTICE for attribution.
REM   2. Commercial under a separate proprietary licence offered by
REM      OntoLedgy Ltd. See COMMERCIAL.md for terms and contact details.
REM
REM SPDX-License-Identifier: AGPL-3.0-or-later

@echo off
setlocal

REM DWSIM Test Harness - Static File Server
REM Serves the apps directory so the test harness can be opened in a browser.
REM The test harness works in Mock mode without the MCP backend.
REM For Live mode, also run start-http.bat in a separate terminal.

REM Disable QuickEdit mode for this console to prevent freezing when clicked.
REM QuickEdit causes the process to pause when text is selected in the terminal.
powershell -Command "$k='kernel32.dll';$s='[DllImport(\"'+$k+'\")]public static extern IntPtr GetStdHandle(int h);[DllImport(\"'+$k+'\")]public static extern bool GetConsoleMode(IntPtr h,out int m);[DllImport(\"'+$k+'\")]public static extern bool SetConsoleMode(IntPtr h,int m);';$t=Add-Type -M $s -N W -P;$h=$t::GetStdHandle(-10);$m=0;$t::GetConsoleMode($h,[ref]$m);$t::SetConsoleMode($h,$m -band -bnot 0x0040)" >NUL 2>&1

REM Determine the root directory
if defined DWSIM_MCP_ROOT (
    set "ROOT=%DWSIM_MCP_ROOT%"
) else (
    set "ROOT=%~dp0.."
)

REM Remove trailing backslash if present
if "%ROOT:~-1%"=="\" set "ROOT=%ROOT:~0,-1%"

set "APPS_DIR=%ROOT%\mcp_service\server\dwsim_mcp_server\apps"
set "PORT=8080"

REM Allow port override via argument
if not "%~1"=="" set "PORT=%~1"

REM Validate apps directory exists
if not exist "%APPS_DIR%\test-harness\index.html" (
    echo ERROR: Test harness not found at %APPS_DIR%\test-harness\
    echo Expected directory structure: mcp_service\server\dwsim_mcp_server\apps\test-harness\
    exit /b 1
)

set "URL=http://localhost:%PORT%/test-harness/index.html"

echo ============================================
echo   DWSIM Test Harness
echo ============================================
echo Serving:  %APPS_DIR%
echo URL:      %URL%
echo Press Ctrl+C to stop
echo ============================================
echo.

REM Open browser after a short delay (in background)
start "" cmd /c "timeout /t 2 /nobreak >NUL && start %URL%"

REM Serve the apps directory
cd /d "%APPS_DIR%"
python -m http.server %PORT%

endlocal
