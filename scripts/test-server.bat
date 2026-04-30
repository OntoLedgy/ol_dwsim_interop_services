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
setlocal EnableDelayedExpansion
echo ============================================
echo   DWSIM MCP Server - Post-Deploy Test
echo ============================================
echo.

set SERVER_URL=http://localhost:8000
set MCP_ENDPOINT=%SERVER_URL%/mcp/

REM Required headers for streamable-http transport
set CONTENT_TYPE=-H "Content-Type: application/json"
set ACCEPT=-H "Accept: application/json, text/event-stream"

echo [1/4] Testing server connectivity...
curl.exe -s -o NUL -w "HTTP Status: %%{http_code}" %MCP_ENDPOINT% %CONTENT_TYPE% %ACCEPT% > "%TEMP%\http_status.txt"
set /p HTTP_STATUS=<"%TEMP%\http_status.txt"
echo %HTTP_STATUS%
if %ERRORLEVEL% NEQ 0 (
    echo [FAIL] Cannot connect to server at %SERVER_URL%
    echo Make sure the server is running: start-http.bat
    exit /b 1
)
echo [OK] Server is reachable
echo.

echo [2/4] Testing MCP initialize and capturing session ID...
curl.exe -s -D "%TEMP%\mcp_headers.txt" -X POST %MCP_ENDPOINT% %CONTENT_TYPE% %ACCEPT% -d "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{\"protocolVersion\":\"2024-11-05\",\"capabilities\":{},\"clientInfo\":{\"name\":\"test\",\"version\":\"1.0\"}}}" > "%TEMP%\mcp_test.json"
echo Response:
type "%TEMP%\mcp_test.json"
echo.

REM Extract session ID from headers using PowerShell for reliable parsing
for /f "usebackq delims=" %%a in (`powershell -Command "(Get-Content '%TEMP%\mcp_headers.txt' | Select-String 'mcp-session-id' | ForEach-Object { $_.Line -replace '.*mcp-session-id:\s*', '' }).Trim()"`) do (
    set "SESSION_ID=%%a"
)

if defined SESSION_ID (
    echo Session ID: !SESSION_ID!
) else (
    echo [WARN] No session ID found in headers
    echo Headers received:
    type "%TEMP%\mcp_headers.txt"
)
echo.

findstr /C:"serverInfo" "%TEMP%\mcp_test.json" > NUL
if %ERRORLEVEL% NEQ 0 (
    echo [FAIL] MCP initialize failed
    exit /b 1
)
echo [OK] MCP initialize successful
echo.

echo [3/4] Testing tools/list with session ID...
if defined SESSION_ID (
    curl.exe -s -X POST %MCP_ENDPOINT% %CONTENT_TYPE% %ACCEPT% -H "mcp-session-id: !SESSION_ID!" -d "{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"tools/list\",\"params\":{}}" > "%TEMP%\mcp_tools.json"
) else (
    curl.exe -s -X POST %MCP_ENDPOINT% %CONTENT_TYPE% %ACCEPT% -d "{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"tools/list\",\"params\":{}}" > "%TEMP%\mcp_tools.json"
)
echo Response:
type "%TEMP%\mcp_tools.json"
echo.
findstr /C:"tools" "%TEMP%\mcp_tools.json" > NUL
if %ERRORLEVEL% NEQ 0 (
    echo [FAIL] tools/list failed
    exit /b 1
)
echo [OK] tools/list successful
echo.

echo ============================================
echo   All tests passed!
echo ============================================
echo.
echo Server URL: %MCP_ENDPOINT%
if defined SESSION_ID echo Session ID: !SESSION_ID!
echo.

:cleanup
del "%TEMP%\mcp_test.json" 2>NUL
del "%TEMP%\mcp_tools.json" 2>NUL
del "%TEMP%\mcp_headers.txt" 2>NUL
del "%TEMP%\http_status.txt" 2>NUL
endlocal
