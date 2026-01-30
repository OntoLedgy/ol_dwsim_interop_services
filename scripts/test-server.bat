@echo off
echo ============================================
echo   DWSIM MCP Server - Post-Deploy Test
echo ============================================
echo.

set SERVER_URL=http://localhost:8000
set MCP_ENDPOINT=%SERVER_URL%/mcp/

REM Required headers for streamable-http transport
set HEADERS=-H "Content-Type: application/json" -H "Accept: application/json, text/event-stream"

echo [1/4] Testing server connectivity...
curl.exe -s -o nul -w "HTTP Status: %%{http_code}" %MCP_ENDPOINT% %HEADERS% > "%TEMP%\http_status.txt"
set /p HTTP_STATUS=<"%TEMP%\http_status.txt"
echo %HTTP_STATUS%
if %ERRORLEVEL% NEQ 0 (
    echo [FAIL] Cannot connect to server at %SERVER_URL%
    echo Make sure the server is running: start-http.bat
    exit /b 1
)
echo [OK] Server is reachable
echo.

echo [2/4] Testing MCP initialize...
curl.exe -s -X POST %MCP_ENDPOINT% %HEADERS% -d "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{\"protocolVersion\":\"2024-11-05\",\"capabilities\":{},\"clientInfo\":{\"name\":\"test\",\"version\":\"1.0\"}}}" > "%TEMP%\mcp_test.json"
echo Response:
type "%TEMP%\mcp_test.json"
echo.
findstr /C:"serverInfo" "%TEMP%\mcp_test.json" > nul
if %ERRORLEVEL% NEQ 0 (
    echo [FAIL] MCP initialize failed
    exit /b 1
)
echo [OK] MCP initialize successful
echo.

echo [3/4] Testing tools/list...
curl.exe -s -X POST %MCP_ENDPOINT% %HEADERS% -d "{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"tools/list\",\"params\":{}}" > "%TEMP%\mcp_tools.json"
echo Response:
type "%TEMP%\mcp_tools.json"
echo.
findstr /C:"tools" "%TEMP%\mcp_tools.json" > nul
if %ERRORLEVEL% NEQ 0 (
    echo [FAIL] tools/list failed
    exit /b 1
)
echo [OK] tools/list successful
echo.

echo [4/4] Verbose endpoint test...
echo.
echo --- Testing with -v flag for debugging ---
curl.exe -v -X POST %MCP_ENDPOINT% %HEADERS% -d "{\"jsonrpc\":\"2.0\",\"id\":3,\"method\":\"ping\",\"params\":{}}" 2>&1
echo.
echo --- End verbose test ---
echo.

echo ============================================
echo   Tests completed!
echo ============================================
echo.
echo Server URL: %MCP_ENDPOINT%
echo.
del "%TEMP%\mcp_test.json" 2>nul
del "%TEMP%\mcp_tools.json" 2>nul
del "%TEMP%\http_status.txt" 2>nul
