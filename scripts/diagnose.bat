@echo off
REM DWSIM MCP Server - Diagnostic Script
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

dwsim-mcp doctor
