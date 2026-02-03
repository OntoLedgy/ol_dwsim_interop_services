# DWSIM MCP Server - Production Deployment

Quick production deployment guide for the DWSIM MCP server with Clerk OAuth.

## Prerequisites

- Production server with Python 3.11+
- [uv](https://docs.astral.sh/uv/) package manager installed
- Clerk production account configured
- HTTPS reverse proxy (nginx, caddy, etc.)

## Step 1: Pull Latest Code

```bash
cd /path/to/dwsim_interop_services
git pull origin develop
cd mcp_service/server
uv sync --extra http
```

## Step 2: Configure Clerk for Production

In your **production** Clerk dashboard (https://dashboard.clerk.com):

### 2a. Create JWT Template

1. Go to **Configure** → **JWT Templates**
2. Click **New template** → **Blank**
3. Configure:
   - **Name**: `dwsim-mcp`
   - **Claims**:
     ```json
     {
       "aud": "dwsim-mcp"
     }
     ```
4. Click **Save**

### 2b. Create OAuth Application

1. Go to **Configure** → **SSO Connections** → **OAuth Applications**
2. Click **Create OAuth Application**
3. Configure:
   - **Name**: `DWSIM MCP Client`
   - **Redirect URIs**: Add your callback URL (e.g., `https://your-domain.com/callback`)
   - **Scopes**: Enable `profile` and `email`
4. After creating, save the **Client ID** and **Client Secret**

### 2c. Note Your Issuer URL

Find your production issuer URL under **Configure** → **Settings** → **Issuer URL**
Format: `https://your-app.clerk.accounts.dev`

## Step 3: Create Production Environment File

Create `mcp_service/server/.env.auth` on the production server:

```env
# OAuth and Server Configuration - Production

# Public MCP URL (REQUIRED when behind a reverse proxy)
# This is the full URL clients use to reach the MCP endpoint
DWSIM_PUBLIC_BASE_URL=https://your-domain.com/dwsim/mcp

# OAuth Configuration
DWSIM_AUTH_ENABLED=true
CLERK_ISSUER_URL=https://YOUR-PROD-ISSUER.clerk.accounts.dev
CLERK_AUDIENCE=dwsim-mcp
CLERK_REQUIRED_SCOPES=["user"]
```

Replace:
- `your-domain.com/dwsim` with your actual public URL (including any path prefix from your reverse proxy)
- `YOUR-PROD-ISSUER` with your actual Clerk production issuer URL

## Step 4: Start the Server

### Option A: Using the production script (Windows)

```bash
cd /path/to/dwsim_interop_services
scripts/start-http-prod.bat
```

### Option B: Direct execution

```bash
cd mcp_service/server
# Load environment
source .env.auth  # Linux
# OR set variables manually on Windows

DWSIM_TRANSPORT_MODE=streamable-http uv run python -m dwsim_mcp_server.server
```

### Option C: As a Windows Service

Use NSSM or similar to run as a service:
```bash
nssm install DwsimMcp "C:\path\to\python.exe" "-m dwsim_mcp_server.server"
nssm set DwsimMcp AppDirectory "C:\path\to\mcp_service\server"
nssm set DwsimMcp AppEnvironmentExtra "DWSIM_TRANSPORT_MODE=streamable-http" "DWSIM_AUTH_ENABLED=true" ...
```

## Step 5: Set Up HTTPS Reverse Proxy

The MCP server runs on HTTP (port 8000). Use a reverse proxy for HTTPS.

### Nginx Configuration

```nginx
server {
    listen 443 ssl http2;
    server_name mcp.yourdomain.com;

    ssl_certificate /etc/letsencrypt/live/mcp.yourdomain.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/mcp.yourdomain.com/privkey.pem;

    location / {
        proxy_pass http://127.0.0.1:8000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;

        # Longer timeouts for simulation operations
        proxy_read_timeout 300s;
        proxy_connect_timeout 75s;
    }
}
```

### Caddy Configuration (simpler)

```
mcp.yourdomain.com {
    reverse_proxy localhost:8000
}
```

### IIS Configuration (Windows)

Use URL Rewrite and ARR (Application Request Routing):
1. Install ARR and URL Rewrite modules
2. Create reverse proxy rule pointing to `http://localhost:8000`
3. Configure SSL certificate in IIS

## Step 6: Verify Deployment

### Test OAuth Discovery

```bash
curl https://mcp.yourdomain.com/.well-known/oauth-protected-resource
```

Expected response:
```json
{
  "resource": "https://mcp.yourdomain.com/mcp",
  "authorization_servers": ["https://your-app.clerk.accounts.dev/"],
  "scopes_supported": ["user"]
}
```

### Test Authentication Required

```bash
curl https://mcp.yourdomain.com/mcp
```

Expected: 401 Unauthorized response

### Test MCP Endpoint (with token)

```bash
TOKEN="your-jwt-token"
curl -X POST https://mcp.yourdomain.com/mcp \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{"jsonrpc":"2.0","method":"tools/list","id":1}'
```

## Step 7: Configure MCP Clients

### Claude Desktop Configuration

Edit `%APPDATA%\Claude\claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "dwsim": {
      "command": "npx",
      "args": [
        "mcp-remote",
        "https://mcp.yourdomain.com/mcp",
        "--client-id", "YOUR_CLERK_CLIENT_ID",
        "--client-secret", "YOUR_CLERK_CLIENT_SECRET"
      ]
    }
  }
}
```

Replace:
- `mcp.yourdomain.com` with your actual domain
- `YOUR_CLERK_CLIENT_ID` with the OAuth Application client ID from Step 2b
- `YOUR_CLERK_CLIENT_SECRET` with the OAuth Application client secret from Step 2b

### ChatGPT Configuration

1. In ChatGPT settings, add MCP server
2. URL: `https://mcp.yourdomain.com/mcp`
3. ChatGPT handles OAuth flow automatically via discovery endpoint

## Troubleshooting

### 401 Unauthorized

- Verify `CLERK_ISSUER_URL` matches your Clerk production app
- Check `CLERK_AUDIENCE` matches the JWT template (`dwsim-mcp`)
- Ensure token is not expired
- Check server logs for JWT validation errors

### JWKS Fetch Errors

- Verify network connectivity from server to Clerk
- Check firewall allows outbound HTTPS to `*.clerk.accounts.dev`
- Try setting `CLERK_JWKS_URL` explicitly:
  ```env
  CLERK_JWKS_URL=https://your-app.clerk.accounts.dev/.well-known/jwks.json
  ```

### Connection Refused

- Verify server is running: `netstat -an | grep 8000`
- Check `DWSIM_HTTP_HOST=0.0.0.0` (not `127.0.0.1`)
- Verify firewall allows traffic on port 8000 (internal) and 443 (external)

### OAuth Flow Not Working

- Verify OAuth Application redirect URI matches what mcp-remote uses
- Check Client ID and Secret are correct
- Ensure OAuth Application has required scopes enabled

## Security Checklist

- [ ] HTTPS enabled via reverse proxy
- [ ] `DWSIM_PUBLIC_BASE_URL` set to your public HTTPS URL
- [ ] `DWSIM_AUTH_ENABLED=true` set
- [ ] `CLERK_AUDIENCE=dwsim-mcp` set (prevents token reuse from other apps)
- [ ] Firewall blocks direct access to port 8000 from external
- [ ] `.env.auth` file has restricted permissions (not world-readable)
- [ ] Production Clerk app is separate from development

## Environment Variables Reference

| Variable | Required | Default | Description |
|----------|----------|---------|-------------|
| `DWSIM_TRANSPORT_MODE` | Yes | `stdio` | Set to `streamable-http` for HTTP |
| `DWSIM_HTTP_HOST` | No | `0.0.0.0` | Bind address |
| `DWSIM_HTTP_PORT` | No | `8000` | HTTP port |
| `DWSIM_PUBLIC_BASE_URL` | Yes* | - | Full public MCP URL behind reverse proxy (e.g., `https://example.com/dwsim/mcp`) |
| `DWSIM_AUTH_ENABLED` | Yes | `false` | Must be `true` for production |
| `CLERK_ISSUER_URL` | Yes | - | Your Clerk issuer URL |
| `CLERK_AUDIENCE` | Recommended | - | JWT audience claim |
| `CLERK_REQUIRED_SCOPES` | No | `["user"]` | Required OAuth scopes |
| `DWSIM_LOG_LEVEL` | No | `INFO` | Logging level |

*Required when running behind a reverse proxy (HTTPS, path prefix, etc.)
