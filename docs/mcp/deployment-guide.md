<!--
SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.

This file is part of the OntoLedgy Thermodynamics Architecture and is
dual-licensed:

  1. Open source under the GNU Affero General Public License v3.0 or
     later (AGPL-3.0-or-later). See the LICENSE file in the repository
     root for the full licence text and NOTICE for attribution.
  2. Commercial under a separate proprietary licence offered by
     OntoLedgy Ltd. See COMMERCIAL.md for terms and contact details.

SPDX-License-Identifier: AGPL-3.0-or-later
-->

# DWSIM MCP Server Deployment Guide

Complete guide for deploying the DWSIM MCP server with Clerk OAuth authentication.

## Prerequisites

- Python 3.11 or 3.12
- [uv](https://docs.astral.sh/uv/) package manager
- Access to Clerk dashboard (dev and prod)
- DWSIM binaries (see `mcp_service/dwsim_worker/SETUP.md`)

## Part 1: Clerk Configuration

### Step 1: Create JWT Template

Perform these steps in **both** your dev and prod Clerk dashboards.

1. Log into [Clerk Dashboard](https://dashboard.clerk.com)
2. Select your application
3. Navigate to **Configure** → **JWT Templates**
4. Click **New template** → **Blank**
5. Configure:
   - **Name**: `dwsim-mcp`
   - **Claims**:
     ```json
     {
       "aud": "dwsim-mcp"
     }
     ```
6. Click **Save**

### Step 2: Note Your Issuer URLs

From each Clerk dashboard, copy the issuer URL:

| Environment | Issuer URL Format |
|-------------|-------------------|
| Development | `https://your-app-dev.clerk.accounts.dev` |
| Production  | `https://your-app-prod.clerk.accounts.dev` |

Find this under **Configure** → **Settings** → **Issuer URL** (or in the API Keys section).

## Part 2: Server Setup

### Step 1: Clone and Install

```bash
# Clone the repository
git clone <your-repo-url> dwsim_interop_services
cd dwsim_interop_services

# Navigate to MCP server
cd mcp_service/server

# Install dependencies
uv sync --extra dev --extra http
```

### Step 2: Configure DWSIM Binaries

```bash
cd ../dwsim_worker
cp dwsim.config.sample.json DwsimWorker/dwsim.config.json
# Edit dwsim.config.json with your DWSIM installation path
./build.bat
```

### Step 3: Create Environment File

Create `.env` in `mcp_service/server/`:

**For Development:**
```env
# Transport
DWSIM_TRANSPORT_MODE=streamable-http
DWSIM_HTTP_HOST=0.0.0.0
DWSIM_HTTP_PORT=8000

# Authentication
DWSIM_AUTH_ENABLED=true
CLERK_ISSUER_URL=https://your-app-dev.clerk.accounts.dev
CLERK_AUDIENCE=dwsim-mcp
CLERK_REQUIRED_SCOPES=user

# Logging
DWSIM_LOG_LEVEL=DEBUG

# Paths (adjust as needed)
DWSIM_CASE_STORAGE_ROOTS=./cases,/shared/cases
```

**For Production:**
```env
# Transport
DWSIM_TRANSPORT_MODE=streamable-http
DWSIM_HTTP_HOST=0.0.0.0
DWSIM_HTTP_PORT=8000

# Authentication
DWSIM_AUTH_ENABLED=true
CLERK_ISSUER_URL=https://your-app-prod.clerk.accounts.dev
CLERK_AUDIENCE=dwsim-mcp
CLERK_REQUIRED_SCOPES=user

# Logging
DWSIM_LOG_LEVEL=INFO

# Paths
DWSIM_CASE_STORAGE_ROOTS=/data/cases
```

## Part 3: Running the Server

### Option A: Direct Execution

```bash
cd mcp_service/server
uv run python -m dwsim_mcp_server.server
```

### Option B: Using the CLI

```bash
cd mcp_service/server
uv run dwsim-mcp serve --transport http --port 8000
```

### Option C: Docker (Recommended for Production)

Create `Dockerfile` in `mcp_service/server/`:

```dockerfile
FROM python:3.12-slim

WORKDIR /app

# Install uv
RUN pip install uv

# Copy project files
COPY pyproject.toml uv.lock ./
COPY dwsim_mcp_server ./dwsim_mcp_server
COPY prebuilt ./prebuilt

# Install dependencies
RUN uv sync --extra http --no-dev

# Expose port
EXPOSE 8000

# Run server
CMD ["uv", "run", "python", "-m", "dwsim_mcp_server.server"]
```

Build and run:
```bash
docker build -t ol-dwsim-mcp-server .
docker run -p 8000:8000 --env-file .env ol-dwsim-mcp-server
```

### Option D: Docker Compose

Create `docker-compose.yml`:

```yaml
version: '3.8'

services:
  dwsim-mcp:
    build: ./mcp_service/server
    ports:
      - "8000:8000"
    environment:
      - DWSIM_TRANSPORT_MODE=streamable-http
      - DWSIM_HTTP_HOST=0.0.0.0
      - DWSIM_HTTP_PORT=8000
      - DWSIM_AUTH_ENABLED=true
      - CLERK_ISSUER_URL=${CLERK_ISSUER_URL}
      - CLERK_AUDIENCE=dwsim-mcp
      - CLERK_REQUIRED_SCOPES=user
    volumes:
      - ./cases:/data/cases
    restart: unless-stopped
```

Run:
```bash
docker-compose up -d
```

## Part 4: Reverse Proxy Setup (Production)

For production, put the MCP server behind a reverse proxy with HTTPS.

### Nginx Example

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

### Caddy Example (Simpler)

```
mcp.yourdomain.com {
    reverse_proxy localhost:8000
}
```

## Part 5: Verification

### Test 1: Health Check

```bash
curl http://localhost:8000/mcp
# Should return MCP protocol response or auth error
```

### Test 2: OAuth Discovery

```bash
curl http://localhost:8000/.well-known/oauth-protected-resource
# Should return OAuth metadata with Clerk issuer
```

### Test 3: Unauthenticated Request (Should Fail)

```bash
curl -X POST http://localhost:8000/mcp \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"tools/list","id":1}'
# Should return 401 Unauthorized
```

### Test 4: Authenticated Request

```bash
# Get a token from Clerk first (via your frontend or Clerk API)
TOKEN="your-jwt-token-here"

curl -X POST http://localhost:8000/mcp \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{"jsonrpc":"2.0","method":"tools/list","id":1}'
# Should return list of tools
```

## Part 6: Client Configuration

### For mcp-remote

```json
{
  "mcpServers": {
    "dwsim": {
      "command": "npx",
      "args": [
        "mcp-remote",
        "https://mcp.yourdomain.com/mcp"
      ]
    }
  }
}
```

The mcp-remote client will automatically discover OAuth settings and prompt for authentication.

### For ChatGPT Developer Mode

1. In ChatGPT settings, add MCP server
2. URL: `https://mcp.yourdomain.com/mcp`
3. ChatGPT will handle OAuth flow automatically via the discovery endpoint

## Troubleshooting

### 401 Unauthorized

- Check `CLERK_ISSUER_URL` matches your Clerk app
- Verify `CLERK_AUDIENCE` matches the JWT template
- Ensure token is not expired
- Check server logs for specific JWT validation errors

### JWKS Fetch Errors

- Verify network connectivity to Clerk
- Check if `CLERK_ISSUER_URL` is correct
- Try setting `CLERK_JWKS_URL` explicitly:
  ```env
  CLERK_JWKS_URL=https://your-app.clerk.accounts.dev/.well-known/jwks.json
  ```

### Connection Refused

- Verify `DWSIM_HTTP_HOST=0.0.0.0` (not `127.0.0.1`) for Docker
- Check firewall rules
- Verify port is not in use

### Simulation Errors

- Ensure DWSIM binaries are properly installed
- Check `DWSIM_WORKER_ASSEMBLY_PATH` if not using default location
- Review logs for pythonnet initialization errors

## Environment Variable Reference

| Variable | Required | Default | Description |
|----------|----------|---------|-------------|
| `DWSIM_TRANSPORT_MODE` | No | `stdio` | `stdio` or `streamable-http` |
| `DWSIM_HTTP_HOST` | No | `0.0.0.0` | Bind address for HTTP |
| `DWSIM_HTTP_PORT` | No | `8000` | Port for HTTP |
| `DWSIM_AUTH_ENABLED` | No | `false` | Enable OAuth |
| `CLERK_ISSUER_URL` | If auth enabled | - | Clerk issuer URL |
| `CLERK_JWKS_URL` | No | Derived | Override JWKS URL |
| `CLERK_AUDIENCE` | No | - | Required JWT audience |
| `CLERK_REQUIRED_SCOPES` | No | `user` | Comma-separated scopes |
| `DWSIM_LOG_LEVEL` | No | `INFO` | `DEBUG`, `INFO`, `WARNING`, `ERROR` |
| `DWSIM_CASE_STORAGE_ROOTS` | No | `./cases` | Allowed case file paths |

## Security Checklist

- [ ] HTTPS enabled (via reverse proxy)
- [ ] `DWSIM_AUTH_ENABLED=true` in production
- [ ] `CLERK_AUDIENCE` set to prevent token reuse
- [ ] Firewall restricts direct access to port 8000
- [ ] `DWSIM_CASE_STORAGE_ROOTS` limits file access
- [ ] Logs don't contain sensitive tokens (default behavior)
