<!--
SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.

SPDX-License-Identifier: AGPL-3.0-or-later
-->

# OntoLedgy DWSIM Interop Services

> **Windows only.** Runs DWSIM in-process via pythonnet, so it requires a DWSIM build with the full Windows desktop runtime (Windows 10/11 or Windows Server with Desktop Experience). macOS support is being explored. Linux is not currently supported.

Exposes [DWSIM](https://dwsim.org) as a [Model Context Protocol](https://modelcontextprotocol.io) (MCP) server. AI assistants and other MCP clients can call canonical thermodynamics operations — flash calculations, phase properties, compound lookups — against a live DWSIM engine. Licensed under **AGPL-3.0-or-later**.

Implements the `SimulatorAdapter` protocol defined by [`ol-simulator-interop-services`](https://github.com/OntoLedgy/ol_simulator_interop_services), making this one adapter among several planned for the same protocol (DWSIM now; Rust thermo kernel, HYSYS, UniSim planned).

---

## Install

### From PyPI (recommended for end users)

Install via [`pipx`](https://pipx.pypa.io/) or [`uv tool`](https://docs.astral.sh/uv/guides/tools/) so the `dwsim-mcp` command lands on the system `PATH` — MCP clients like Claude Desktop need to be able to launch it without inheriting a venv:

```powershell
pipx install ol-dwsim-mcp-server
# or
uv tool install ol-dwsim-mcp-server
```

Plain `pip install ol-dwsim-mcp-server` into a venv also works, but you'll need to reference the venv's `dwsim-mcp.exe` by absolute path when configuring MCP clients (see [Connecting to AI assistants](#connecting-to-ai-assistants)).

All three install modes pull the prebuilt `DwsimWorker.dll` bundled in the wheel. DWSIM's own engine binaries can't be redistributed — you supply those yourself:

1. **Use an existing DWSIM install.** Set `DWSIM_PATH` to your DWSIM directory (e.g. `C:\Program Files\DWSIM9`) and run `dwsim-mcp run`.
2. **Build DWSIM from source.** Clone [DanWBR/dwsim](https://github.com/DanWBR/dwsim), build, and point `DWSIM_PATH` at the build output.

Full setup in [`mcp_service/dwsim_worker/SETUP.md`](mcp_service/dwsim_worker/SETUP.md).

### From a GitHub Release

Each tag publishes three assets on the [Releases page](https://github.com/OntoLedgy/ol_dwsim_interop_services/releases):

| Asset | Use when |
|---|---|
| `ol_dwsim_mcp_server-<version>-py3-none-any.whl` | Standard install — `pip install <wheel-path>` |
| `ol-dwsim-mcp-server-<version>-source.zip` | Inspecting or redistributing source (AGPL §13) |
| `DwsimWorker-<version>.zip` | Standalone .NET worker payload without the Python wrapper |

The DWSIM version each release links against is listed in the release notes as a compatibility row.

### From a cloned checkout (development)

Run from a local clone if you're modifying the server, stress-testing before a release, or can't use `pipx`/`uv tool`. The server depends on the sibling [`ol_simulator_interop_services`](https://github.com/OntoLedgy/ol_simulator_interop_services) package; `uv` installs it as an editable local dependency via [`[tool.uv.sources]`](mcp_service/server/pyproject.toml) pointing at a sibling checkout.

**1. Clone both repos side-by-side.** The relative path `../../../ol_simulator_interop_services` from `mcp_service/server/pyproject.toml` must resolve to the interop package's repo root:

```powershell
# A parent directory for both
mkdir C:\dev\ontoledgy ; cd C:\dev\ontoledgy

git clone https://github.com/OntoLedgy/ol_simulator_interop_services.git
git clone https://github.com/OntoLedgy/ol_dwsim_interop_services.git
```

This gives you `C:\dev\ontoledgy\ol_simulator_interop_services` and `C:\dev\ontoledgy\ol_dwsim_interop_services` — the uv source path resolves correctly.

**2. Set up the Python environment:**

```powershell
cd ol_dwsim_interop_services\mcp_service\server
uv sync
```

`uv sync` installs all runtime deps plus an editable install of the local `ol-simulator-interop-services`. It also registers the `dwsim-mcp` entry point in `.venv\Scripts\`.

**3. Build the .NET worker** (full .NET prerequisites listed under [Development](#development)):

```powershell
cd ..\dwsim_worker
.\build.bat
```

**4. Configure DWSIM.** Copy [`mcp_service/dwsim_worker/dwsim.config.json.sample`](mcp_service/dwsim_worker/dwsim.config.json.sample) to `dwsim.config.json` (gitignored) and set `dwsim_path` to your DWSIM install or build folder — see [`mcp_service/dwsim_worker/SETUP.md`](mcp_service/dwsim_worker/SETUP.md) for detail.

**5. Verify and run:**

```powershell
cd ..\server
.\.venv\Scripts\dwsim-mcp.exe doctor
.\.venv\Scripts\dwsim-mcp.exe run
```

**6. Point an MCP client at the checkout.** Use the absolute-path variant in [Connecting to AI assistants](#connecting-to-ai-assistants), with `command` set to `C:\dev\ontoledgy\ol_dwsim_interop_services\mcp_service\server\.venv\Scripts\dwsim-mcp.exe`.

Edits to Python source are picked up immediately (editable install). Edits to `shared/property_packages.toml` or C# code require `build.bat` again so the new artifacts land in `DwsimWorker/bin/Debug/`.

For deeper contributor workflow (tests, linting, release flow), see the [Development](#development) section below.

---

## Quick start

### Verify the install

```powershell
dwsim-mcp doctor
```

Reports Python version, pythonnet status, DWSIM discovery, and missing prerequisites.

### Run the MCP server

```powershell
dwsim-mcp run
```

Defaults to stdio transport — the shape Claude Desktop, VS Code Copilot, and Codex CLI expect. For HTTP/SSE, pass `--transport http` or `--transport sse`.

### See the release info

```powershell
dwsim-mcp version
```

Prints package name, version, commit SHA, source URL, and license — the AGPL §13 source offer, also exposed at runtime via the `release://info` MCP resource.

---

## Connecting to AI assistants

| Platform | Configuration file | Format |
|---|---|---|
| **Claude Desktop** | `%APPDATA%\Claude\claude_desktop_config.json` | JSON |
| **VS Code Copilot** | `settings.json` or `mcp.json` | JSON |
| **OpenAI Codex CLI** | `%USERPROFILE%\.codex\config.toml` (or `~/.codex/config.toml`) | TOML |

The config form depends on how you installed the server. Codex CLI uses TOML; Claude Desktop and VS Code use JSON.

**If installed with `pipx` or `uv tool install`** — `dwsim-mcp` is on PATH and the host can launch it by name.

Claude Desktop / VS Code Copilot (JSON):

```json
{
  "mcpServers": {
    "dwsim": {
      "command": "dwsim-mcp",
      "args": ["run"]
    }
  }
}
```

OpenAI Codex CLI (TOML, append to `~/.codex/config.toml`):

```toml
[mcp_servers.dwsim]
command = "dwsim-mcp"
args = ["run"]
```

**If installed with `pip install` into a venv, or running from a cloned checkout** — use the absolute path to the venv's `dwsim-mcp.exe` so the host's subprocess doesn't need a PATH entry or venv activation.

Claude Desktop / VS Code Copilot (JSON):

```json
{
  "mcpServers": {
    "dwsim": {
      "command": "C:\\path\\to\\your\\.venv\\Scripts\\dwsim-mcp.exe",
      "args": ["run"]
    }
  }
}
```

OpenAI Codex CLI (TOML):

```toml
[mcp_servers.dwsim]
command = "C:\\path\\to\\your\\.venv\\Scripts\\dwsim-mcp.exe"
args = ["run"]
```

Detailed per-platform setup in [`docs/resources/getting-started.md`](docs/resources/getting-started.md).

---

## Architecture

This adapter is the concrete DWSIM implementation of the canonical [`SimulatorAdapter`](https://github.com/OntoLedgy/ol_simulator_interop_services/blob/main/src/ol_simulator_interop_services/domain/protocols/simulator_adapter.py) protocol. DWSIM's .NET Framework engine is loaded **in-process** via pythonnet — no separate worker process, no HTTP hop.

```
LLM agent / MCP client
        |
        v
+--[ ol_thermodynamics_agent_services ]--+
|   MCP tools, routing, provenance       |
+----------------------------------------+
        |
        v
+--[ ol_simulator_interop_services ]-----+
|   canonical domain model + protocol    |
+----------------------------------------+
        |
   +----+----+----------+---------+
   v         v          v         v
 [Rust]   [DWSIM]   [HYSYS]   [UniSim]
 kernel   adapter   adapter    adapter
            ^
            |
         this repo
```

**Polyglot layout:**
- `mcp_service/server/` — Python MCP server (FastMCP + pythonnet CLR loader).
- `mcp_service/dwsim_worker/` — .NET Framework 4.8 worker hosting the DWSIM engine.
- `shared/property_packages.toml` — single source of truth for the property-package inventory consumed by both Python and .NET sides.

### Project structure

```
ol_dwsim_interop_services/
├── shared/                           # Cross-language configuration
│   └── property_packages.toml        # Canonical property-package inventory
├── mcp_service/
│   ├── server/                       # Python MCP server (ol-dwsim-mcp-server wheel)
│   │   ├── dwsim_mcp_server/         # Main Python package
│   │   └── tests/                    # unit / integration / smoke / cli tests
│   └── dwsim_worker/                 # .NET Framework 4.8 worker
│       ├── DwsimWorker/              # C# class library (DwsimWorker.dll)
│       └── DwsimWorker.Tests/        # xUnit test project
├── prebuilt/                         # Beta-tester setup script + bundled binaries
├── docs/                             # MCP tool reference, architecture, deployment
└── scripts/                          # Build + packaging helpers
```

---

## Key features

- **Model Context Protocol** — standard stdio, HTTP, and SSE transports via FastMCP.
- **CAPE-OPEN** — phase properties surfaced with the same semantics other chemical-engineering tools expect.
- **In-process DWSIM** — pythonnet loads DwsimWorker.dll into the server process; sub-millisecond overhead per call.
- **Session-based** — each MCP session gets an isolated DWSIM flowsheet; sessions are cleanly torn down on disconnect.
- **Capability-gated tool surface** — adapter declares `SUPPORTED_TOOLS`; the agent layer only advertises what's actually wired (no lying about `create_session` or `get_diagnostics`).
- **Single source of truth for property packages** — edit `shared/property_packages.toml`, rebuild; Python and .NET both pick up the change.
- **AGPL §13 source offer** — every running instance self-identifies commit SHA, source URL, and license via CLI, startup log, and `release://info` MCP resource.
- **Observability** — structured logs, OpenTelemetry traces at every tool boundary, Prometheus-style metrics.

---

## Development

### Prerequisites

- Windows 10/11 or Windows Server with **Desktop Experience** (DWSIM depends on Eto.Forms/WinForms — Server Core is not supported).
- **Python 3.11 or 3.12** + [`uv`](https://docs.astral.sh/uv/).
- **.NET Framework 4.8 Developer Pack** + **Visual Studio Build Tools** with the ".NET desktop development" workload.
  - .NET Framework 4.8 download: https://dotnet.microsoft.com/download/dotnet-framework/net48
  - VS Build Tools: https://visualstudio.microsoft.com/downloads/ (scroll to "Build Tools for Visual Studio")
- A DWSIM build (x64 Debug or Release) — either a user install or a local clone of [DanWBR/dwsim](https://github.com/DanWBR/dwsim) you've built.

### Install uv (Windows, PowerShell)

```powershell
irm https://astral.sh/uv/install.ps1 | iex
```

If execution policy blocks the pipe:

```powershell
Set-ExecutionPolicy -Scope CurrentUser -ExecutionPolicy RemoteSigned
$uv = "$env:TEMP\uv-install.ps1"
irm https://astral.sh/uv/install.ps1 -OutFile $uv
Unblock-File $uv
& $uv
```

Verify: `uv --version`.

### Build from source

```powershell
git clone https://github.com/OntoLedgy/ol_dwsim_interop_services.git
cd ol_dwsim_interop_services

# Python environment
cd mcp_service\server
uv sync
.\.venv\Scripts\Activate.ps1

# .NET worker
cd ..\dwsim_worker
.\build.bat
```

`build.bat` handles framework/SDK versioning — use it instead of `dotnet build` directly to avoid mismatch errors.

### DWSIM binaries setup

The worker needs DWSIM's compiled engine DLLs next to it. Copy from a local DWSIM build:

```powershell
$src = "..\..\dwsim\DWSIM\bin\x64\Debug"    # adjust to your DWSIM clone
$dest = "mcp_service\dwsim_worker\dwsim_binaries\x64\Debug"
New-Item -ItemType Directory -Path $dest -Force | Out-Null
Copy-Item "$src\*.dll" $dest -Force
Copy-Item "$src\data" "$dest\data" -Recurse -Force
Copy-Item "$src\ThermoCS" "$dest\ThermoCS" -Recurse -Force
```

Or copy [`mcp_service/dwsim_worker/dwsim.config.json.sample`](mcp_service/dwsim_worker/dwsim.config.json.sample) to `dwsim.config.json` (gitignored) and set `dwsim_path` to your DWSIM build folder.

Full step-by-step in [`mcp_service/dwsim_worker/SETUP.md`](mcp_service/dwsim_worker/SETUP.md).

### Running tests

```powershell
# Python
cd mcp_service\server
.\.venv\Scripts\python.exe -m pytest tests/unit tests/cli -q

# Live-DWSIM integration (requires DwsimWorker.dll built + DWSIM binaries)
.\.venv\Scripts\python.exe -m pytest tests/integration -q -m live_dwsim

# C# (from mcp_service\dwsim_worker after build.bat)
# Always use vstest via build.bat's output — never `dotnet test` directly.
```

### Adding a property package

Edit [`shared/property_packages.toml`](shared/property_packages.toml), then `.\build.bat`. Both the Python adapter (`tomllib` at import time) and the .NET worker (Tomlyn in a static constructor) pick the change up at next startup. A live-DWSIM alignment test asserts the canonical, worker, and runtime inventories agree.

---

## Related repositories

| Repository | Layer | Role |
|---|---|---|
| [`ol_thermodynamics_agent_services`](https://github.com/OntoLedgy/ol_thermodynamics_agent_services) | Agent (top) | MCP tool schemas, backend routing, provenance |
| [`ol_simulator_interop_services`](https://github.com/OntoLedgy/ol_simulator_interop_services) | Interop (middle) | Canonical domain model, `SimulatorAdapter` protocol, registries |
| `ol_thermodynamics_kernel` *(planned)* | Adapter | Native Rust thermo kernel via PyO3 |

---

## Architecture reference

- [Solution Architecture on Confluence](https://ontoledgy.atlassian.net/wiki/spaces/ACE/pages/6425018388/Solution+Architecture) — full architectural description.
- [`docs/mcp/mcp-tools.md`](docs/mcp/mcp-tools.md) — MCP tool reference and API specifications.
- [`docs/resources/`](docs/resources/) — quickstart, configuration, troubleshooting.
- [`docs/architecture/`](docs/architecture/) — system design, security, observability.

---

## AGPL-3.0 source offer

Every running `ol-dwsim-mcp-server` instance exposes the exact source metadata for that deployment: package name, version, commit SHA, source repository URL, and license. Three surfaces advertise this to satisfy AGPL §13:

1. **MCP resource `release://info`** — any connected client can fetch it.
2. **CLI `dwsim-mcp version`** — operator-facing.
3. **Stderr startup event** — a single JSON line emitted at server boot, event name `dwsim_mcp_server_started`.

See [LICENSE](LICENSE) for the full AGPL-3.0-or-later text.

---

## License

OntoLedgy's contributions in this repository are dual-licensed:

1. **Open source** — **AGPL-3.0-or-later**. See [LICENSE](LICENSE) for the full text and [NOTICE](NOTICE) for copyright and attribution.
2. **Commercial** — a separate proprietary license is available from OntoLedgy Ltd. **for OntoLedgy's contributions only**. See [COMMERCIAL.md](COMMERCIAL.md) for scope, exclusions, and terms.

Copyright (c) 2018-2026 OntoLedgy Ltd.

**Important — DWSIM scope.** This repository links to **DWSIM**, an upstream chemical process simulator licensed under **GPL-3.0** by its maintainers. DWSIM is **not** owned by OntoLedgy and **cannot** be relicensed by us. The commercial license offered here covers OntoLedgy's own code (the MCP server, the `DwsimWorker` glue source, and OntoLedgy-authored build/test/doc material) but does **not** relicense DWSIM or any compiled artifact (e.g. `DwsimWorker.dll`) to the extent it embeds or links DWSIM .NET assemblies. See [COMMERCIAL.md](COMMERCIAL.md) for the three practical paths to a fully proprietary deployment.

**Why AGPL?** MCP servers are reachable by network clients (HTTP/SSE transports), and even stdio servers are often proxied over the network by their MCP hosts. AGPL §13 ensures that modifications to a network-reachable instance have their source made available to those users — closing the "SaaS loophole" in plain GPL. GPLv3 §13 explicitly permits combining GPLv3 code (DWSIM) with AGPLv3 code.

DWSIM is licensed under GPLv3. See the [DWSIM repository](https://github.com/DanWBR/dwsim) for more information.

---

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for development guidelines, code standards, and PR flow.

## Acknowledgments

- **DWSIM** — open-source chemical process simulation by Daniel Medeiros.
- **CAPE-OPEN** — industry interoperability standards for process simulators.
- **Model Context Protocol** — by Anthropic.
- **FastMCP** — by Jeremiah Lowin.
- **pythonnet** — Python ↔ .NET CLR bridge.
