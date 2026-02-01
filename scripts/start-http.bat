@echo off
REM DWSIM MCP Server - HTTP Mode Startup Script
REM This script expects to be in the same directory as the repo folder
REM or have DWSIM_MCP_ROOT environment variable set

REM Determine the root directory
if defined DWSIM_MCP_ROOT (
    set "ROOT=%DWSIM_MCP_ROOT%"
) else (
    set "ROOT=%~dp0"
)

REM Remove trailing backslash if present
if "%ROOT:~-1%"=="\" set "ROOT=%ROOT:~0,-1%"

REM Set paths
set "REPO=%ROOT%\dwsim_interop_services"
set "SERVER=%REPO%\mcp_service\server"

REM Check if paths exist
if not exist "%SERVER%\.venv\Scripts\activate.bat" (
    echo ERROR: Virtual environment not found at %SERVER%\.venv
    echo Run the setup script first.
    exit /b 1
)

cd /d "%SERVER%"
call .venv\Scripts\activate.bat
set "PYTHONPATH=%REPO%;%SERVER%"
set "DWSIM_TRANSPORT_MODE=streamable-http"
set "DWSIM_HTTP_PORT=8000"

REM Load OAuth settings from .env.auth if it exists
if exist "%SERVER%\.env.auth" (
    for /f "usebackq tokens=1,* delims==" %%a in ("%SERVER%\.env.auth") do (
        if not "%%a"=="" if not "%%a:~0,1%"=="#" set "%%a=%%b"
    )
    echo [AUTH] Loaded OAuth configuration from .env.auth
) else (
    set "DWSIM_AUTH_ENABLED=false"
    echo [AUTH] No .env.auth found - running without authentication
)

echo ============================================
echo   DWSIM MCP Server - HTTP Mode
echo ============================================
echo Server URL: http://localhost:8000/mcp
if "%DWSIM_AUTH_ENABLED%"=="true" (
    echo Auth: Enabled (Clerk OAuth)
) else (
    echo Auth: Disabled
)
echo Press Ctrl+C to stop
echo ============================================
echo.

dwsim-mcp run
