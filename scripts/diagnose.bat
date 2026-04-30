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
