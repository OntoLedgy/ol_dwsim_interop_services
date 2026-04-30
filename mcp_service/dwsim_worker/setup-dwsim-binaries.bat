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
REM ============================================================================
REM Setup DWSIM Binaries - Wrapper script that calls PowerShell version
REM ============================================================================
REM This script is a simple wrapper that calls the PowerShell version
REM of the setup script for proper JSON parsing.
REM ============================================================================

powershell.exe -ExecutionPolicy Bypass -File "%~dp0setup-dwsim-binaries.ps1"
exit /b %ERRORLEVEL%
