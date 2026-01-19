@echo off
REM ============================================================================
REM Setup DWSIM Binaries - Wrapper script that calls PowerShell version
REM ============================================================================
REM This script is a simple wrapper that calls the PowerShell version
REM of the setup script for proper JSON parsing.
REM ============================================================================

powershell.exe -ExecutionPolicy Bypass -File "%~dp0setup-dwsim-binaries.ps1"
exit /b %ERRORLEVEL%
