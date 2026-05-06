# SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
#
# This file is part of the OntoLedgy Thermodynamics Architecture and is
# dual-licensed:
#
#   1. Open source under the GNU Affero General Public License v3.0 or
#      later (AGPL-3.0-or-later). See the LICENSE file in the repository
#      root for the full licence text and NOTICE for attribution.
#   2. Commercial under a separate proprietary licence offered by
#      OntoLedgy Ltd. See COMMERCIAL.md for terms and contact details.
#
# SPDX-License-Identifier: AGPL-3.0-or-later

<#
.SYNOPSIS
    One-command installer for the DWSIM MCP Server.

.DESCRIPTION
    Installs uv (if needed), installs ol-dwsim-mcp-server as a tool,
    downloads DWSIM binaries (or accepts a local path), and configures
    MCP for Claude Code, OpenAI Codex CLI, and VS Code Copilot.

.PARAMETER DwsimPath
    Path to an existing local DWSIM installation. Skips download.
    WARNING: version differences from the tested v9.0.5-mcp may cause issues.

.PARAMETER SkipUvInstall
    Skip installing uv (use if you already have uv or prefer pipx).

.PARAMETER UsePipx
    Use pipx instead of uv for package installation.

.PARAMETER SkipMcpConfig
    Skip automatic MCP client configuration.

.PARAMETER McpClients
    Which MCP clients to configure. Defaults to all detected.
    Valid values: Claude, Codex, Copilot, All

.EXAMPLE
    # Full automatic install (recommended)
    irm https://raw.githubusercontent.com/OntoLedgy/ol_dwsim_interop_services/develop/install.ps1 | iex

.EXAMPLE
    # Use local DWSIM install
    .\install.ps1 -DwsimPath "C:\Program Files\DWSIM"

.EXAMPLE
    # Skip uv install (already have it)
    .\install.ps1 -SkipUvInstall
#>

param(
    [string]$DwsimPath = "",
    [switch]$SkipUvInstall,
    [switch]$UsePipx,
    [switch]$SkipMcpConfig,
    [ValidateSet("Claude", "Codex", "Copilot", "All")]
    [string]$McpClients = "All"
)

$ErrorActionPreference = "Stop"

# ─────────────────────────────────────────────────────────────────────────────
# Helpers
# ─────────────────────────────────────────────────────────────────────────────

function Write-Step {
    param([string]$Message)
    Write-Host ""
    Write-Host "  $Message" -ForegroundColor Cyan
    Write-Host "  $('-' * $Message.Length)" -ForegroundColor DarkCyan
}

function Write-Ok {
    param([string]$Message)
    Write-Host "  [OK] $Message" -ForegroundColor Green
}

function Write-Warn {
    param([string]$Message)
    Write-Host "  [!]  $Message" -ForegroundColor Yellow
}

function Write-Err {
    param([string]$Message)
    Write-Host "  [X]  $Message" -ForegroundColor Red
}

function Test-CommandExists {
    param([string]$Command)
    $null -ne (Get-Command $Command -ErrorAction SilentlyContinue)
}

# ─────────────────────────────────────────────────────────────────────────────
# Banner
# ─────────────────────────────────────────────────────────────────────────────

Write-Host ""
Write-Host "  ╔══════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "  ║   DWSIM MCP Server - One-Command Installer  ║" -ForegroundColor Cyan
Write-Host "  ╚══════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""
Write-Host "  This script will:" -ForegroundColor White
Write-Host "    1. Install uv (Python package manager)" -ForegroundColor Gray
Write-Host "    2. Install ol-dwsim-mcp-server" -ForegroundColor Gray
Write-Host "    3. Download/configure DWSIM binaries" -ForegroundColor Gray
Write-Host "    4. Configure your AI coding assistant(s)" -ForegroundColor Gray
Write-Host ""

# ─────────────────────────────────────────────────────────────────────────────
# Step 1: Install uv
# ─────────────────────────────────────────────────────────────────────────────

Write-Step "Step 1: Python package manager"

if ($UsePipx) {
    if (-not (Test-CommandExists "pipx")) {
        Write-Err "pipx not found. Install pipx first: https://pipx.pypa.io/stable/installation/"
        Write-Host "  Or remove -UsePipx to use uv instead (recommended)." -ForegroundColor Gray
        exit 1
    }
    Write-Ok "pipx is available"
    $PackageManager = "pipx"
} elseif ($SkipUvInstall) {
    if (-not (Test-CommandExists "uv")) {
        Write-Err "uv not found and -SkipUvInstall was specified."
        Write-Host "  Install uv manually: https://docs.astral.sh/uv/getting-started/installation/" -ForegroundColor Gray
        exit 1
    }
    Write-Ok "uv is available (skipped install)"
    $PackageManager = "uv"
} else {
    if (Test-CommandExists "uv") {
        Write-Ok "uv is already installed"
    } else {
        Write-Host "  Installing uv..." -ForegroundColor Gray
        try {
            [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
            $ProgressPreference = 'SilentlyContinue'
            Invoke-RestMethod https://astral.sh/uv/install.ps1 | Invoke-Expression
            $ProgressPreference = 'Continue'

            # Refresh PATH for current session
            $uvPath = "$env:USERPROFILE\.local\bin"
            if (Test-Path $uvPath) {
                $env:PATH = "$uvPath;$env:PATH"
            }
            $cargoUvPath = "$env:USERPROFILE\.cargo\bin"
            if (Test-Path $cargoUvPath) {
                $env:PATH = "$cargoUvPath;$env:PATH"
            }

            if (-not (Test-CommandExists "uv")) {
                Write-Err "uv installed but not found in PATH. Please restart your terminal and re-run."
                exit 1
            }
            Write-Ok "uv installed successfully"
        } catch {
            Write-Err "Failed to install uv: $_"
            Write-Host "  Install manually: https://docs.astral.sh/uv/getting-started/installation/" -ForegroundColor Gray
            exit 1
        }
    }
    $PackageManager = "uv"
}

# ─────────────────────────────────────────────────────────────────────────────
# Step 2: Install ol-dwsim-mcp-server
# ─────────────────────────────────────────────────────────────────────────────

Write-Step "Step 2: Install ol-dwsim-mcp-server"

try {
    if ($PackageManager -eq "uv") {
        & uv tool install ol-dwsim-mcp-server 2>&1 | ForEach-Object { Write-Host "  $_" -ForegroundColor Gray }
    } else {
        & pipx install ol-dwsim-mcp-server 2>&1 | ForEach-Object { Write-Host "  $_" -ForegroundColor Gray }
    }

    if (-not (Test-CommandExists "dwsim-mcp")) {
        # Try refreshing PATH
        $env:PATH = [System.Environment]::GetEnvironmentVariable("PATH", "User") + ";" + $env:PATH
        if (-not (Test-CommandExists "dwsim-mcp")) {
            Write-Err "dwsim-mcp command not found after install. You may need to restart your terminal."
            exit 1
        }
    }
    Write-Ok "ol-dwsim-mcp-server installed"
} catch {
    Write-Err "Failed to install ol-dwsim-mcp-server: $_"
    exit 1
}

# ─────────────────────────────────────────────────────────────────────────────
# Step 3: DWSIM binaries
# ─────────────────────────────────────────────────────────────────────────────

Write-Step "Step 3: DWSIM binaries"

if ($DwsimPath) {
    # User provided a local DWSIM path
    if (-not (Test-Path $DwsimPath)) {
        Write-Err "Provided DWSIM path does not exist: $DwsimPath"
        exit 1
    }

    Write-Warn "Using local DWSIM installation: $DwsimPath"
    Write-Warn "This server is tested against DWSIM v9.0.5-mcp."
    Write-Warn "Version differences may cause runtime errors or incorrect results."
    Write-Warn "If you encounter issues, re-run without -DwsimPath to use the tested build."
    Write-Host ""

    try {
        & dwsim-mcp setup --dwsim-path $DwsimPath 2>&1 | ForEach-Object { Write-Host "  $_" -ForegroundColor Gray }
        Write-Ok "Configured with local DWSIM at: $DwsimPath"
    } catch {
        Write-Err "Setup failed: $_"
        exit 1
    }
} else {
    # Download the tested DWSIM build
    Write-Host "  Downloading DWSIM v9.0.5-mcp binaries (~280 MB)..." -ForegroundColor Gray
    Write-Host "  This may take a few minutes on slower connections." -ForegroundColor Gray

    try {
        & dwsim-mcp setup --download 2>&1 | ForEach-Object { Write-Host "  $_" -ForegroundColor Gray }
        Write-Ok "DWSIM binaries downloaded and configured"
    } catch {
        Write-Err "Download failed: $_"
        Write-Host "  Check your network connection and retry." -ForegroundColor Gray
        Write-Host "  Alternatively, provide a local DWSIM path: .\install.ps1 -DwsimPath 'C:\DWSIM'" -ForegroundColor Gray
        exit 1
    }
}

# Verify installation
Write-Host ""
Write-Host "  Running doctor checks..." -ForegroundColor Gray
$doctorOutput = & dwsim-mcp doctor 2>&1
$doctorExit = $LASTEXITCODE
$doctorOutput | ForEach-Object { Write-Host "  $_" -ForegroundColor Gray }

if ($doctorExit -ne 0) {
    Write-Warn "Doctor reported issues. The server may not function correctly."
    Write-Warn "Run 'dwsim-mcp doctor --verbose' for remediation details."
} else {
    Write-Ok "All doctor checks passed"
}

# ─────────────────────────────────────────────────────────────────────────────
# Step 4: MCP client configuration
# ─────────────────────────────────────────────────────────────────────────────

Write-Step "Step 4: Configure MCP clients"

if ($SkipMcpConfig) {
    Write-Host "  Skipped (-SkipMcpConfig). See manual configuration below." -ForegroundColor Gray
} else {
    $configuredAny = $false

    # --- Claude Code ---
    if ($McpClients -eq "All" -or $McpClients -eq "Claude") {
        if (Test-CommandExists "claude") {
            Write-Host "  Configuring Claude Code..." -ForegroundColor Gray
            try {
                & claude mcp remove dwsim 2>$null
                & claude mcp add dwsim -- dwsim-mcp run 2>&1 | Out-Null
                Write-Ok "Claude Code configured (server: dwsim)"
                $configuredAny = $true
            } catch {
                Write-Warn "Could not configure Claude Code automatically: $_"
            }
        } else {
            Write-Host "  Claude Code CLI not detected (skipping)" -ForegroundColor Gray
        }
    }

    # --- OpenAI Codex CLI ---
    if ($McpClients -eq "All" -or $McpClients -eq "Codex") {
        $codexConfigDir = Join-Path $env:USERPROFILE ".codex"
        $codexConfigFile = Join-Path $codexConfigDir "config.toml"

        if (Test-CommandExists "codex") {
            Write-Host "  Configuring Codex CLI..." -ForegroundColor Gray
            try {
                if (-not (Test-Path $codexConfigDir)) {
                    New-Item -ItemType Directory -Path $codexConfigDir -Force | Out-Null
                }

                # Read existing config or start fresh
                $tomlContent = ""
                if (Test-Path $codexConfigFile) {
                    $tomlContent = Get-Content $codexConfigFile -Raw
                }

                # Check if dwsim section already exists
                if ($tomlContent -match '\[mcp_servers\.dwsim\]') {
                    Write-Ok "Codex CLI already configured (dwsim section exists)"
                } else {
                    $dwsimSection = @"

[mcp_servers.dwsim]
command = "dwsim-mcp"
args = ["run"]
"@
                    Add-Content -Path $codexConfigFile -Value $dwsimSection -Encoding UTF8
                    Write-Ok "Codex CLI configured ($codexConfigFile)"
                }
                $configuredAny = $true
            } catch {
                Write-Warn "Could not configure Codex CLI: $_"
            }
        } else {
            Write-Host "  Codex CLI not detected (skipping)" -ForegroundColor Gray
        }
    }

    # --- VS Code Copilot ---
    if ($McpClients -eq "All" -or $McpClients -eq "Copilot") {
        $vscodeMcpFile = Join-Path $env:APPDATA "Code\User\mcp.json"
        $vscodeSettingsDir = Join-Path $env:APPDATA "Code\User"

        if (Test-Path $vscodeSettingsDir) {
            Write-Host "  Configuring VS Code Copilot..." -ForegroundColor Gray
            try {
                $mcpConfig = @{}
                if (Test-Path $vscodeMcpFile) {
                    $mcpConfig = Get-Content $vscodeMcpFile -Raw | ConvertFrom-Json -AsHashtable
                }

                if (-not $mcpConfig.ContainsKey("mcpServers")) {
                    $mcpConfig["mcpServers"] = @{}
                }

                $mcpConfig["mcpServers"]["dwsim"] = @{
                    command = "dwsim-mcp"
                    args    = @("run")
                }

                $mcpConfig | ConvertTo-Json -Depth 5 | Set-Content $vscodeMcpFile -Encoding UTF8
                Write-Ok "VS Code Copilot configured ($vscodeMcpFile)"
                $configuredAny = $true
            } catch {
                Write-Warn "Could not configure VS Code Copilot: $_"
            }
        } else {
            Write-Host "  VS Code user settings not found (skipping)" -ForegroundColor Gray
        }
    }

    if (-not $configuredAny) {
        Write-Warn "No MCP clients were auto-configured."
    }
}

# ─────────────────────────────────────────────────────────────────────────────
# Summary & manual config for other clients
# ─────────────────────────────────────────────────────────────────────────────

Write-Host ""
Write-Host "  ╔══════════════════════════════════════════════╗" -ForegroundColor Green
Write-Host "  ║         Installation Complete!               ║" -ForegroundColor Green
Write-Host "  ╚══════════════════════════════════════════════╝" -ForegroundColor Green
Write-Host ""
Write-Host "  Quick test:  dwsim-mcp doctor" -ForegroundColor White
Write-Host ""
Write-Host "  ── Manual MCP Configuration ──" -ForegroundColor Yellow
Write-Host ""
Write-Host "  For any MCP-compatible client, the server command is:" -ForegroundColor White
Write-Host ""
Write-Host "    dwsim-mcp run" -ForegroundColor White
Write-Host ""
Write-Host "  Claude Desktop  ($env:APPDATA\Claude\claude_desktop_config.json):" -ForegroundColor Yellow
Write-Host @"
    {
      "mcpServers": {
        "dwsim": {
          "command": "dwsim-mcp",
          "args": ["run"]
        }
      }
    }
"@ -ForegroundColor Gray
Write-Host ""
Write-Host "  Claude Code (CLI):" -ForegroundColor Yellow
Write-Host "    claude mcp add dwsim -- dwsim-mcp run" -ForegroundColor Gray
Write-Host ""
Write-Host "  Codex CLI  (~/.codex/config.toml):" -ForegroundColor Yellow
Write-Host @"
    [mcp_servers.dwsim]
    command = "dwsim-mcp"
    args = ["run"]
"@ -ForegroundColor Gray
Write-Host ""
Write-Host "  VS Code Copilot  ($env:APPDATA\Code\User\mcp.json):" -ForegroundColor Yellow
Write-Host @"
    {
      "mcpServers": {
        "dwsim": {
          "command": "dwsim-mcp",
          "args": ["run"]
        }
      }
    }
"@ -ForegroundColor Gray
Write-Host ""
Write-Host "  For HTTP/SSE transport (remote/Docker), see:" -ForegroundColor Gray
Write-Host "    https://github.com/OntoLedgy/ol_dwsim_interop_services#deployment" -ForegroundColor Gray
Write-Host ""
