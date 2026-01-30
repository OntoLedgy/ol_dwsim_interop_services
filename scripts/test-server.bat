@echo off
echo ============================================
echo   DWSIM MCP Server - Post-Deploy Test
echo ============================================
echo.

set SERVER_URL=http://localhost:8000

echo [1/3] Testing server connectivity...
curl.exe -s -o nul -w "HTTP Status: %%{http_code}\n" %SERVER_URL%/mcp
if %ERRORLEVEL% NEQ 0 (
    echo [FAIL] Cannot connect to server at %SERVER_URL%
    echo Make sure the server is running: start-http.bat
    exit /b 1
)
echo [OK] Server is reachable
echo.

echo [2/3] Testing MCP initialize...
curl.exe -s -X POST %SERVER_URL%/mcp -H "Content-Type: application/json" -d "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{\"protocolVersion\":\"2024-11-05\",\"capabilities\":{},\"clientInfo\":{\"name\":\"test\",\"version\":\"1.0\"}}}" > "%TEMP%\mcp_test.json"
findstr /C:"serverInfo" "%TEMP%\mcp_test.json" > nul
if %ERRORLEVEL% NEQ 0 (
    echo [FAIL] MCP initialize failed
    type "%TEMP%\mcp_test.json"
    exit /b 1
)
echo [OK] MCP initialize successful
echo.

echo [3/3] Testing tools/list...
curl.exe -s -X POST %SERVER_URL%/mcp -H "Content-Type: application/json" -d "{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"tools/list\",\"params\":{}}" > "%TEMP%\mcp_tools.json"
findstr /C:"tools" "%TEMP%\mcp_tools.json" > nul
if %ERRORLEVEL% NEQ 0 (
    echo [FAIL] tools/list failed
    type "%TEMP%\mcp_tools.json"
    exit /b 1
)
echo [OK] tools/list successful
echo.

echo ============================================
echo   All tests passed!
echo ============================================
echo.
echo Server URL: %SERVER_URL%/mcp
echo.
del "%TEMP%\mcp_test.json" 2>nul
del "%TEMP%\mcp_tools.json" 2>nul
