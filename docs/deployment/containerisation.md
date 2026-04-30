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

# Containerisation Stance and Deployment Model

## Why No Docker Image Is Provided

The DWSIM MCP Server cannot be containerised using standard Linux-based Docker images due to hard dependencies on the Windows desktop stack:

1. **Windows COM / .NET Framework interop** -- The core DWSIM engine is a .NET Framework 4.8 application accessed via pythonnet. It relies on COM interop and Windows-specific assemblies that have no Linux equivalent.

2. **DWSIM GUI runtime requirements** -- DWSIM uses **Eto.Forms** with the **WinForms** platform backend. Even in headless operation the server must initialise the WinForms platform to construct flowsheet objects. This requires Windows with Desktop Experience; Windows Server Core is insufficient.

3. **ChemSep / compound databases** -- DWSIM loads thermodynamic data from Windows-path-resolved databases (ChemSep XML, DWSIM compound databases). These file-resolution mechanisms assume a Windows filesystem layout.

An early Docker-based deployment was attempted and subsequently removed (see commit `fb2d70a`) after confirming that the .NET Framework and GUI dependencies cannot run inside a Linux container.

## Supported Deployment Model

The server runs as a native Windows process on bare-metal or virtual-machine infrastructure.

### Platform Requirements

| Requirement | Detail |
|---|---|
| **OS** | Windows Server 2022 with Desktop Experience, or Windows 10/11 |
| **Runtime** | .NET Framework 4.8, Python 3.12+ |
| **Build tools** | Visual Studio Build Tools 2022 (MSBuild) |
| **Package manager** | uv (replaces pip/poetry for faster installs) |

> **Windows Server Core will NOT work.** The WinForms platform backend requires a full desktop shell.

### Build and Start Process

```
# 1. Build the .NET worker and set up the Python environment
scripts\build.bat

# 2. Start the MCP server (HTTP transport for network access)
scripts\start-http.bat          # development
scripts\start-http-prod.bat     # production

# 3. Start in stdio mode (for local MCP client integration)
scripts\start-stdio.bat
```

`build.bat` performs the following steps automatically:

1. Locates MSBuild via VS Build Tools or common install paths
2. Copies DWSIM binaries from your local DWSIM build into `dwsim_binaries/`
3. Verifies all critical .NET assemblies are present
4. Compiles the DwsimWorker .NET solution
5. Creates the Python virtual environment via uv and installs dependencies

The server entry point is `python -m dwsim_mcp_server` (or the `dwsim-mcp` CLI wrapper).

### Automated Server Provisioning

For fresh Windows Server instances, an unattended setup script is provided:

```powershell
# Run as Administrator
scripts\setup-windows-server.ps1 -RepoUrl "<repository-url>" -InstallPath "C:\DwsimMcp"
```

This installs Git, Python, VS Build Tools via Chocolatey, clones the repository, and runs the full build. See `docs/deployment/installation-guide.md` for step-by-step details.

## Network Architecture

```
┌──────────────────────────────────────┐
│         MCP Client (LLM agent)       │
└──────────────┬───────────────────────┘
               │ HTTP (Streamable HTTP transport)
               ▼
┌──────────────────────────────────────┐
│    Reverse Proxy (nginx / IIS ARR)   │  ← optional
└──────────────┬───────────────────────┘
               │
               ▼
┌──────────────────────────────────────┐
│       DWSIM MCP Server (Python)      │
│  ┌────────────────────────────────┐  │
│  │  FastMCP + Clerk OAuth         │  │
│  └────────────┬───────────────────┘  │
│               │ pythonnet / COM      │
│  ┌────────────▼───────────────────┐  │
│  │  DwsimWorker (.NET Fwk 4.8)   │  │
│  └────────────────────────────────┘  │
│         Windows Server 2022          │
└──────────────────────────────────────┘
```

The server supports `DWSIM_PUBLIC_BASE_URL` for reverse-proxy deployments and exposes an OAuth discovery endpoint for MCP client auto-configuration.

## Future Containerisation Path

Windows Server containers with .NET desktop runtime support offer a potential path forward:

1. **Windows Server containers** -- Docker on Windows can run Windows-based containers (`mcr.microsoft.com/windows/servercore` or `mcr.microsoft.com/windows` with Desktop Experience). These containers include the full Win32 API surface needed by DWSIM.

2. **Prerequisites for container support**:
   - A Windows container host (Windows Server 2022 or Windows 11 with Hyper-V isolation)
   - A base image that includes .NET Framework 4.8 and Desktop Experience components
   - DWSIM binaries baked into the image or mounted as a volume
   - Validation that Eto.Forms/WinForms initialisation succeeds without a physical display (virtual framebuffer or headless desktop session)

3. **Expected benefits**:
   - Reproducible builds and deployments
   - Horizontal scaling behind a load balancer
   - Integration with container orchestrators (Kubernetes with Windows node pools)

4. **Open questions**:
   - Whether WinForms initialisation works inside a Windows container without an interactive desktop session
   - Image size (Windows base images are typically 5--10 GB)
   - Cold-start time for DWSIM engine initialisation inside a container

Until these questions are resolved through experimentation, the bare-metal / VM deployment model remains the supported approach.
