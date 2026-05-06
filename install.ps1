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
    One-command installer and uninstaller for the DWSIM MCP Server.

.DESCRIPTION
    Installs uv (if needed), installs ol-dwsim-mcp-server as a tool,
    downloads DWSIM binaries (or accepts a local path), and configures
    MCP for Claude Code, OpenAI Codex CLI, and VS Code Copilot.
    When -Uninstall is provided, removes the tool and MCP client entries.

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

.PARAMETER Uninstall
    Remove ol-dwsim-mcp-server and DWSIM MCP client configuration.

.EXAMPLE
    # Full automatic install (recommended)
    irm https://raw.githubusercontent.com/OntoLedgy/ol_dwsim_interop_services/develop/install.ps1 | iex

.EXAMPLE
    # Use local DWSIM install
    .\install.ps1 -DwsimPath "C:\Program Files\DWSIM"

.EXAMPLE
    # Skip uv install (already have it)
    .\install.ps1 -SkipUvInstall

.EXAMPLE
    # Remove installed tool and MCP client configuration
    .\install.ps1 -Uninstall
#>

param(
    [string]$DwsimPath = "",
    [switch]$SkipUvInstall,
    [switch]$UsePipx,
    [switch]$SkipMcpConfig,
    [ValidateSet("Claude", "Codex", "Copilot", "All")]
    [string]$McpClients = "All",
    [switch]$Uninstall
)

$ErrorActionPreference = "Stop"
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$RequiredDwsimAssemblies = @(
    "DWSIM.Interfaces.dll",
    "DWSIM.Thermodynamics.dll",
    "DWSIM.SharedClasses.dll"
)

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

function Write-Utf8NoBom {
    param([string]$Path, [string]$Content)
    $encoding = New-Object System.Text.UTF8Encoding $false
    [System.IO.File]::WriteAllText($Path, $Content, $encoding)
}

function Add-PathPrepend {
    param([string]$DirectoryPath)
    if (-not (Test-Path -LiteralPath $DirectoryPath)) {
        return
    }
    if (($env:PATH -split ';') -notcontains $DirectoryPath) {
        $env:PATH = "$DirectoryPath;$env:PATH"
    }
}

function Invoke-Native {
    param([string]$Description, [scriptblock]$ScriptBlock)
    & $ScriptBlock
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0) {
        Write-Err "$Description failed with exit code $exitCode."
        exit 1
    }
}

function Format-TextWithOneTrailingNewline {
    param([string]$Text)
    if ([string]::IsNullOrEmpty($Text)) {
        return ""
    }
    return ($Text -replace "(\r?\n)+$", "") + "`n"
}

function Get-DwsimCodexSection {
    return "[mcp_servers.dwsim]`ncommand = `"dwsim-mcp`"`nargs = [`"run`"]`n"
}

function Test-JsonProperty {
    param([object]$JsonObject, [string]$PropertyName)
    if ($null -eq $JsonObject) {
        return $false
    }
    $null -ne $JsonObject.PSObject.Properties[$PropertyName]
}

function Set-JsonProperty {
    param([object]$JsonObject, [string]$PropertyName, [object]$Value)
    if (Test-JsonProperty $JsonObject $PropertyName) {
        $JsonObject.PSObject.Properties[$PropertyName].Value = $Value
        return
    }
    $JsonObject | Add-Member -NotePropertyName $PropertyName -NotePropertyValue $Value
}

function Remove-JsonProperty {
    param([object]$JsonObject, [string]$PropertyName)
    if (-not (Test-JsonProperty $JsonObject $PropertyName)) {
        return $false
    }
    $JsonObject.PSObject.Properties.Remove($PropertyName)
    return $true
}

function Get-VsCodeMcpConfig {
    param([string]$ConfigPath)
    if (-not (Test-Path -LiteralPath $ConfigPath)) {
        return [PSCustomObject]@{}
    }
    $content = Get-Content -LiteralPath $ConfigPath -Raw -Encoding UTF8
    if ([string]::IsNullOrWhiteSpace($content)) {
        return [PSCustomObject]@{}
    }
    return ($content | ConvertFrom-Json)
}

function Set-DwsimCopilotConfig {
    param([string]$ConfigPath)
    $mcpConfig = Get-VsCodeMcpConfig $ConfigPath
    if ((-not (Test-JsonProperty $mcpConfig "mcpServers")) -or ($null -eq $mcpConfig.mcpServers)) {
        Set-JsonProperty $mcpConfig "mcpServers" ([PSCustomObject]@{})
    }
    $serverConfig = [PSCustomObject]@{ command = "dwsim-mcp"; args = @("run") }
    Set-JsonProperty $mcpConfig.mcpServers "dwsim" $serverConfig
    Write-Utf8NoBom $ConfigPath (($mcpConfig | ConvertTo-Json -Depth 5) + "`n")
}

function Remove-DwsimCopilotConfig {
    param([string]$ConfigPath)
    if (-not (Test-Path -LiteralPath $ConfigPath)) {
        return $false
    }
    $mcpConfig = Get-VsCodeMcpConfig $ConfigPath
    if (-not (Test-JsonProperty $mcpConfig "mcpServers")) {
        return $false
    }
    if (-not (Remove-JsonProperty $mcpConfig.mcpServers "dwsim")) {
        return $false
    }
    Write-Utf8NoBom $ConfigPath (($mcpConfig | ConvertTo-Json -Depth 5) + "`n")
    return $true
}

function Add-DwsimCodexConfig {
    param([string]$ConfigPath)
    $tomlContent = ""
    if (Test-Path -LiteralPath $ConfigPath) {
        $tomlContent = Get-Content -LiteralPath $ConfigPath -Raw -Encoding UTF8
    }
    if ($tomlContent -match '(?m)^\[mcp_servers\.dwsim\]\s*$') {
        return $false
    }
    $updatedContent = (Format-TextWithOneTrailingNewline $tomlContent) + (Get-DwsimCodexSection)
    Write-Utf8NoBom $ConfigPath $updatedContent
    return $true
}

function Remove-DwsimCodexConfig {
    param([string]$ConfigPath)
    if (-not (Test-Path -LiteralPath $ConfigPath)) {
        return $false
    }
    $tomlContent = Get-Content -LiteralPath $ConfigPath -Raw -Encoding UTF8
    $pattern = '(?m)^[ \t]*\[mcp_servers\.dwsim\][^\r\n]*(\r?\n(?![ \t]*\[).*)*(\r?\n)?'
    $updatedContent = [regex]::Replace($tomlContent, $pattern, "")
    if ($updatedContent -eq $tomlContent) {
        return $false
    }
    Write-Utf8NoBom $ConfigPath (Format-TextWithOneTrailingNewline $updatedContent)
    return $true
}

function Get-MissingDwsimAssemblies {
    param([string]$DwsimDirectory)
    $missingAssemblies = @()
    foreach ($assemblyName in $RequiredDwsimAssemblies) {
        $assemblyPath = Join-Path $DwsimDirectory $assemblyName
        if (-not (Test-Path -LiteralPath $assemblyPath -PathType Leaf)) {
            $missingAssemblies += $assemblyName
        }
    }
    return $missingAssemblies
}

function Uninstall-UvTool {
    if (-not (Test-CommandExists "uv")) {
        Write-Warn "uv not found; skipping uv tool uninstall."
        return $false
    }
    & uv tool uninstall ol-dwsim-mcp-server
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0) {
        Write-Warn "uv did not remove ol-dwsim-mcp-server (exit code $exitCode). It may not be installed."
        return $false
    }
    return $true
}

function Uninstall-PipxTool {
    if (-not (Test-CommandExists "pipx")) {
        Write-Warn "pipx not found; skipping pipx uninstall."
        return $false
    }
    & pipx uninstall ol-dwsim-mcp-server
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0) {
        Write-Warn "pipx did not remove ol-dwsim-mcp-server (exit code $exitCode). It may not be installed."
        return $false
    }
    return $true
}

function Remove-ClaudeDwsimConfig {
    if (-not (Test-CommandExists "claude")) {
        Write-Host "  Claude Code CLI not detected (skipping)" -ForegroundColor Gray
        return $false
    }
    try { & claude mcp remove --scope user dwsim *> $null } catch { }
    if ($LASTEXITCODE -ne 0) {
        Write-Warn "Claude Code dwsim entry was not removed (exit code $LASTEXITCODE). It may not be present."
        return $false
    }
    return $true
}

function Invoke-DwsimUninstall {
    Write-Step "Uninstall DWSIM MCP Server"
    $removedItems = @()
    if ($UsePipx) {
        if (Uninstall-PipxTool) { $removedItems += "pipx tool" }
    } else {
        if (Uninstall-UvTool) { $removedItems += "uv tool" }
    }
    if (Remove-ClaudeDwsimConfig) { $removedItems += "Claude Code config" }
    $codexConfigFile = Join-Path (Join-Path $env:USERPROFILE ".codex") "config.toml"
    if (Remove-DwsimCodexConfig $codexConfigFile) { $removedItems += "Codex config" }
    $vscodeMcpFile = Join-Path $env:APPDATA "Code\User\mcp.json"
    if (Remove-DwsimCopilotConfig $vscodeMcpFile) { $removedItems += "VS Code Copilot config" }
    Write-Step "Uninstall summary"
    if ($removedItems.Count -eq 0) { Write-Warn "No installed DWSIM MCP entries were found."; return }
    foreach ($item in $removedItems) { Write-Ok "Removed $item" }
}

if ($Uninstall) {
    Invoke-DwsimUninstall
    exit 0
}

# ─────────────────────────────────────────────────────────────────────────────
# Banner
# ─────────────────────────────────────────────────────────────────────────────

Write-Host ""
Write-Host "  +----------------------------------------------+" -ForegroundColor Cyan
Write-Host "  |   DWSIM MCP Server - One-Command Installer   |" -ForegroundColor Cyan
Write-Host "  +----------------------------------------------+" -ForegroundColor Cyan
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
            Add-PathPrepend $uvPath
            $cargoUvPath = "$env:USERPROFILE\.cargo\bin"
            Add-PathPrepend $cargoUvPath

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
        Invoke-Native -Description "uv tool install ol-dwsim-mcp-server" -ScriptBlock {
            & uv tool install ol-dwsim-mcp-server
        }
        Add-PathPrepend "$env:USERPROFILE\.local\bin"
    } else {
        Invoke-Native -Description "pipx install ol-dwsim-mcp-server" -ScriptBlock {
            & pipx install ol-dwsim-mcp-server
        }
    }

    if (-not (Test-CommandExists "dwsim-mcp")) {
        # Try refreshing PATH
        $env:PATH = [System.Environment]::GetEnvironmentVariable("PATH", "User") + ";" + $env:PATH
        Add-PathPrepend "$env:USERPROFILE\.local\bin"
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
    if (-not (Test-Path -LiteralPath $DwsimPath -PathType Container)) {
        Write-Err "Provided DWSIM path does not exist: $DwsimPath"
        exit 1
    }

    $missingAssemblies = Get-MissingDwsimAssemblies $DwsimPath
    if ($missingAssemblies.Count -gt 0) {
        Write-Err "Provided DWSIM path is missing required assemblies: $($missingAssemblies -join ', ')"
        Write-Host "  See the README DWSIM-version note. This server is tested against DWSIM v9.0.5-mcp." -ForegroundColor Gray
        exit 1
    }

    Write-Warn "Using local DWSIM installation: $DwsimPath"
    Write-Warn "This server is tested against DWSIM v9.0.5-mcp."
    Write-Warn "Version differences may cause runtime errors or incorrect results."
    Write-Warn "If you encounter issues, re-run without -DwsimPath to use the tested build."
    Write-Host ""

    try {
        Invoke-Native -Description "dwsim-mcp setup --dwsim-path" -ScriptBlock {
            & dwsim-mcp setup --dwsim-path $DwsimPath
        }
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
        Invoke-Native -Description "dwsim-mcp setup --download" -ScriptBlock {
            & dwsim-mcp setup --download
        }
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
& dwsim-mcp doctor
$doctorExit = $LASTEXITCODE

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
                # Best-effort cleanup of any prior registration; ignore failure.
                try { & claude mcp remove --scope user dwsim *> $null } catch { }
                & claude mcp add --scope user dwsim -- dwsim-mcp run *> $null
                $claudeExit = $LASTEXITCODE
                if ($claudeExit -eq 0) {
                    Write-Ok "Claude Code configured (server: dwsim, scope: user)"
                    $configuredAny = $true
                } else {
                    Write-Warn "Could not configure Claude Code automatically (exit code $claudeExit)."
                }
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

                if (Add-DwsimCodexConfig $codexConfigFile) {
                    Write-Ok "Codex CLI configured ($codexConfigFile)"
                } else {
                    Write-Ok "Codex CLI already configured (dwsim section exists)"
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
                Set-DwsimCopilotConfig $vscodeMcpFile
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
Write-Host "  +----------------------------------------------+" -ForegroundColor Green
Write-Host "  |           Installation Complete!             |" -ForegroundColor Green
Write-Host "  +----------------------------------------------+" -ForegroundColor Green
Write-Host ""
Write-Host "  Quick test:  dwsim-mcp doctor" -ForegroundColor White
Write-Host ""
Write-Host "  -- Manual MCP Configuration --" -ForegroundColor Yellow
Write-Host ""
Write-Host "  For any MCP-compatible client, the server command is:" -ForegroundColor White
Write-Host ""
Write-Host "    dwsim-mcp run" -ForegroundColor White
Write-Host ""
$claudeDesktopConfig = @(
    '    {',
    '      "mcpServers": {',
    '        "dwsim": {',
    '          "command": "dwsim-mcp",',
    '          "args": ["run"]',
    '        }',
    '      }',
    '    }'
) -join "`n"
$claudeDesktopPath = Join-Path $env:APPDATA "Claude\claude_desktop_config.json"

Write-Host ("  Claude Desktop  ({0}):" -f $claudeDesktopPath) -ForegroundColor Yellow
Write-Host $claudeDesktopConfig -ForegroundColor Gray
Write-Host ""
Write-Host "  Claude Code (CLI):" -ForegroundColor Yellow
Write-Host "    claude mcp add --scope user dwsim -- dwsim-mcp run" -ForegroundColor Gray
Write-Host ""
Write-Host "  Codex CLI  (~/.codex/config.toml):" -ForegroundColor Yellow
$codexConfig = @(
    '    [mcp_servers.dwsim]',
    '    command = "dwsim-mcp"',
    '    args = ["run"]'
) -join "`n"

Write-Host $codexConfig -ForegroundColor Gray
Write-Host ""
$vscodeMcpConfig = @(
    '    {',
    '      "mcpServers": {',
    '        "dwsim": {',
    '          "command": "dwsim-mcp",',
    '          "args": ["run"]',
    '        }',
    '      }',
    '    }'
) -join "`n"
$vscodeMcpPath = Join-Path $env:APPDATA "Code\User\mcp.json"

Write-Host ("  VS Code Copilot  ({0}):" -f $vscodeMcpPath) -ForegroundColor Yellow
Write-Host $vscodeMcpConfig -ForegroundColor Gray
Write-Host ""
Write-Host "  For HTTP/SSE transport (remote/Docker), see:" -ForegroundColor Gray
Write-Host "    https://github.com/OntoLedgy/ol_dwsim_interop_services#deployment" -ForegroundColor Gray
Write-Host ""
