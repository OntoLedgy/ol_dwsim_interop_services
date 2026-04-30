# Getting Started with DWSIM MCP Server

This guide walks you through connecting the DWSIM MCP Server to VS Code Copilot and running your first simulation.

## Prerequisites

- Windows 10/11 or Windows Server with Desktop Experience, .NET Framework 4.8
- Python 3.11 or 3.12
- An MCP-capable client (Claude Desktop, VS Code Copilot, or OpenAI Codex CLI)
- DWSIM binaries

## Installation

### 1. Clone and Setup (Windows / PowerShell)

```powershell
git clone https://github.com/OntoLedgy/ol_dwsim_interop_services.git
cd ol_dwsim_interop_services\mcp_service\server
uv sync
```

### 2. Use prebuilt binaries (optional, recommended for quick setup)

If you don't want to build the C# layer, use the prebuilt setup script from the repo root:

```powershell
.\prebuilt\setup.ps1
```

See `prebuilt/README.md` for details on what it installs and configures.

---

## MCP Configuration Options

Choose **one** of the following methods to connect the DWSIM MCP server to your AI assistant.

---

### Option A: VS Code settings.json (Copilot)

Add to your VS Code `settings.json` (press Ctrl+, → click "Open Settings (JSON)" icon):

```json
{
  "github.copilot.chat.mcpServers": {
    "dwsim": {
      "command": "uv",
      "args": ["run", "dwsim-mcp"],
      "cwd": "C:\\path\\to\\dwsim_interop_services\\mcp_service\\server",
      "env": {
        "PYTHONPATH": "C:\\path\\to\\dwsim_interop_services"
      }
    }
  }
}
```

**Important**: Replace `C:\\path\\to\\` with your actual path (use double backslashes `\\` on Windows).

---

### Option B: VS Code mcp.json File (Copilot)

Alternatively, create/edit the file at:
```
%APPDATA%\Code\User\mcp.json
```

Or on macOS/Linux:
```
~/.config/Code/User/mcp.json
```

Add the following configuration:

```json
{
  "servers": {
    "dwsim": {
      "command": "uv",
      "args": ["run", "dwsim-mcp"],
      "cwd": "C:\\path\\to\\dwsim_interop_services\\mcp_service\\server",
      "env": {
        "PYTHONPATH": "C:\\path\\to\\dwsim_interop_services"
      }
    }
  }
}
```

You can also access this via **Settings** → search "MCP" → **"MCP: Add Servers"** which opens the mcp.json file directly.

---

### Option C: Claude Desktop

For **Claude Desktop**, edit the configuration file at:

**Windows:**
```
%APPDATA%\Claude\claude_desktop_config.json
```

**macOS:**
```
~/Library/Application Support/Claude/claude_desktop_config.json
```

Add the DWSIM MCP server configuration:

```json
{
  "mcpServers": {
    "dwsim": {
      "command": "uv",
      "args": ["run", "dwsim-mcp"],
      "cwd": "C:\\path\\to\\dwsim_interop_services\\mcp_service\\server",
      "env": {
        "PYTHONPATH": "C:\\path\\to\\dwsim_interop_services"
      }
    }
  }
}
```

**Note**: Claude Desktop requires a full restart (not just reload) after config changes.

---

### Option D: OpenAI Codex CLI

Codex CLI uses **TOML** (not JSON). Create or edit the configuration file at:

**Windows:**
```
%USERPROFILE%\.codex\config.toml
```

**macOS/Linux:**
```
~/.codex/config.toml
```

Append the MCP server entry as a TOML table:

```toml
[mcp_servers.dwsim]
command = "uv"
args = ["run", "dwsim-mcp", "run"]
cwd = "C:\\path\\to\\ol_dwsim_interop_services\\mcp_service\\server"

[mcp_servers.dwsim.env]
PYTHONPATH = "C:\\path\\to\\ol_dwsim_interop_services"
```

If you installed via `pipx` or `uv tool install`, the entry simplifies to:

```toml
[mcp_servers.dwsim]
command = "dwsim-mcp"
args = ["run"]
```

Codex CLI picks up MCP servers automatically on next launch — no flag or environment variable needed.

---

## Path Configuration Tips

### Windows Paths
Use double backslashes in JSON:
```json
"cwd": "C:\\Users\\YourName\\projects\\dwsim_interop_services\\mcp_service\\server"
```

### macOS/Linux Paths
Use forward slashes:
```json
"cwd": "/home/yourname/projects/dwsim_interop_services/mcp_service/server"
```

### Using Environment Variables
You can use environment variable expansion where supported:
```json
"cwd": "${DWSIM_MCP_PATH}/mcp_service/server"
```

---

## Verify Connection

### VS Code Copilot

1. Reload VS Code: `Ctrl+Shift+P` → "Developer: Reload Window"
2. Open Copilot Chat (`Ctrl+Alt+I`)
3. Click the 🔧 tools icon in the chat input
4. You should see 26 DWSIM tools listed

### Claude Desktop

1. Fully restart Claude Desktop
2. Start a new conversation
3. Ask: "What DWSIM tools do you have available?"

### Codex CLI

Restart `codex` so it re-reads `config.toml`, then ask:

```
List available DWSIM tools
```

## Your First Simulation

Ask Copilot to run a simulation:

> "Create a DWSIM session, add methane and water compounds, set Peng-Robinson property package, then close the session"

Or be more specific:

> "Run a three-phase separator simulation with a feed of methane, water, and n-decane at 300K and 1 atm"

## Available Tools (26 total)

### Session Management
| Tool | Description |
|------|-------------|
| `create_session` | Create new simulation workspace |
| `close_session` | Close and cleanup session |
| `save_case` | Save to DWSIM file |
| `load_case` | Load existing file |

### Flowsheet Building
| Tool | Description |
|------|-------------|
| `add_compound` | Add chemical compound |
| `set_property_package` | Set thermodynamic model |
| `set_binary_interaction_parameter` | Set BIP for compound pair |
| `add_stream` | Create material stream |
| `flash_stream` | Flash calculation on stream |
| `add_unit` | Add unit operation |
| `connect` | Connect stream to unit port |
| `list_objects` | List all flowsheet objects |
| `set_object_parameter` | Modify object property |
| `delete_object` | Remove flowsheet object |

### Simulation
| Tool | Description |
|------|-------------|
| `run` | Execute simulation |
| `get_status` | Check simulation status |
| `get_results` | Get stream properties |

### Thermodynamics
| Tool | Description |
|------|-------------|
| `flash_tp` | T-P flash calculation |
| `flash_ph` | P-H flash calculation |
| `flash_ps` | P-S flash calculation |

### Analysis
| Tool | Description |
|------|-------------|
| `sensitivity_analysis` | Parameter sensitivity study |
| `parameter_sweep` | Multi-variable sweep |
| `optimize` | Process optimization |

## Key Concepts

### Source vs Sink Streams

- **Source streams** (`is_source: true`): Feed streams with known conditions (T, P, flow, composition)
- **Sink streams** (`is_source: false`): Outlet streams calculated by DWSIM

### Property Packages

Common thermodynamic models:
- `Peng-Robinson` - General hydrocarbons
- `SRK` - Soave-Redlich-Kwong
- `NRTL` - Non-ideal liquid mixtures
- `UNIQUAC` - Activity coefficient model

### Binary Interaction Parameters (BIPs)

For accurate phase equilibrium with mixtures, set BIPs:
```json
{"tool": "set_binary_interaction_parameter", "arguments": {
  "session_id": "...", 
  "compound1": "Water", 
  "compound2": "Methane", 
  "value": 0.5
}}
```

### SI Units

All values use SI units:
- Temperature: K (Kelvin)
- Pressure: Pa (Pascal)
- Flow: mol/s or kg/s
- Composition: mole fraction (0-1)

## Troubleshooting

### Server won't start

1. Check PYTHONPATH is set correctly
2. Ensure `uv sync` completed successfully
3. Kill any stuck processes: `taskkill /F /IM dwsim-mcp.exe`

### Tools not appearing

1. Reload VS Code window
2. Check settings.json syntax (valid JSON)
3. Verify paths use double backslashes on Windows

### Simulation fails to converge

1. Ensure all outlets are connected
2. Flash feed stream before running
3. Set appropriate BIPs for your mixture
4. Check compound names match exactly (case-sensitive)

### Process locked error

Kill the stuck process and reload:
```powershell
taskkill /F /IM dwsim-mcp.exe
```
Then reload VS Code window.

## Next Steps

- Read [unit-operations](resource://docs/unit-operations) for available equipment
- Check [property-packages](resource://docs/property-packages) for thermodynamic models
- Browse [compounds](resource://docs/compounds) for available chemicals
- Try the complete [three-phase separator example](resource://docs/index) in the documentation index
