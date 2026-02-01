@echo off
REM DWSIM MCP Server - Production HTTP Mode (OAuth Required)
REM This script requires .env.auth to be configured with Clerk credentials

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

REM Check for OAuth configuration
if not exist "%SERVER%\.env.auth" (
    echo ERROR: OAuth configuration not found!
    echo.
    echo Production mode requires .env.auth with Clerk credentials.
    echo.
    echo To set up:
    echo   1. Copy .env.auth.template to .env.auth
    echo   2. Set DWSIM_AUTH_ENABLED=true
    echo   3. Configure CLERK_ISSUER_URL with your Clerk app URL
    echo.
    echo For development without auth, use start-http.bat instead.
    exit /b 1
)

cd /d "%SERVER%"
call .venv\Scripts\activate.bat
set "PYTHONPATH=%REPO%;%SERVER%"
set "DWSIM_TRANSPORT_MODE=streamable-http"
set "DWSIM_HTTP_PORT=8000"

REM Load OAuth settings from .env.auth
for /f "usebackq tokens=1,* delims==" %%a in ("%SERVER%\.env.auth") do (
    if not "%%a"=="" if not "%%a:~0,1%"=="#" set "%%a=%%b"
)

REM Verify auth is enabled
if not "%DWSIM_AUTH_ENABLED%"=="true" (
    echo ERROR: DWSIM_AUTH_ENABLED is not set to 'true' in .env.auth
    echo Production mode requires authentication to be enabled.
    exit /b 1
)

echo ============================================
echo   DWSIM MCP Server - PRODUCTION Mode
echo ============================================
echo Server URL: http://localhost:8000/mcp
echo Auth: ENABLED (Clerk OAuth)
echo Issuer: %CLERK_ISSUER_URL%
echo.
echo WARNING: Ensure this server is behind HTTPS
echo          in production (use nginx/caddy).
echo.
echo Press Ctrl+C to stop
echo ============================================
echo.

dwsim-mcp run
