# Changelog

All notable changes to the DWSIM Interop Services project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

## [0.1.4] - 2026-05-06

### Fixed

- **Property-package inventory loader (DIS-37):** Resolve `property_packages.toml` via `importlib.resources` from the installed package's `_prebuilt/` directory, with a development fallback to the repo `shared/` folder. Fixes `DIS-37 property-package inventory missing at <tool-root>\shared\property_packages.toml` on fresh `uv tool install` deployments.

## [0.1.1] - 2026-05-14

### Fixed

- **`dwsim-mcp version` reports `Commit: unknown` (cosmetic):** `_commit_sha.py` was missing from the
  published wheel because (a) the PowerShell heredoc in the "Bake release commit metadata" CI step is
  fragile under YAML block-scalar indentation rules, and (b) `mcp_service/server/.gitignore` lists
  `dwsim_mcp_server/_commit_sha.py`, causing hatchling's default VCS-based file picker to silently
  exclude it. Fixed by replacing the heredoc with an explicit `Set-Content -Value` call and declaring
  the file as a hatchling `artifact` (`[tool.hatch.build.targets.wheel] artifacts = [...]`) so it is
  always bundled regardless of VCS tracking. A new "Verify `_commit_sha.py` bundled in wheel" CI step
  now fails the build immediately if the file is absent, preventing silent regression.

## [0.1.0] - 2026-04-30

First public beta release. `pip install ol-dwsim-mcp-server` and `pipx install ol-dwsim-mcp-server` are now supported on Windows.

### Added

- **DwsimAdapter** implementing the SimulatorAdapter protocol for clean three-layer architecture integration
- **OAuth authorization** with Clerk (authorization code flow with PKCE, client credentials, token proxy)
- **Test harness** with live MCP server testing, scenario setup app sourcing from Confluence, and OAuth support
- **MCP Apps backend** for interactive UI visualisations with real SensitivityStudyResult schema
- **Flash calculation tools** with algorithm settings exposure via MCP
- **Sensitivity analysis tools** with async/sync bridging and optimisation support
- **NRTL binary interaction parameter** tools with input transformation layer
- **Flowsheet export** functionality and auto-composition for outlet streams
- **MCP resource providers** for docs, samples, and results
- **HTTP transport mode** for network deployments with reverse proxy support
- **Observability stack**: OpenTelemetry distributed tracing, metrics collection, diagnostics infrastructure, log export, correlation context enrichment
- **Deployment automation**: Windows Server setup script, PowerShell install scripts (admin + user), `build.bat` with MSBuild auto-detection
- **Compound usability features** including ChemSep database support and case-insensitive lookup
- **Session management** with save/load case support
- **CAPE-OPEN DTOs**, converters, and unit normalisation
- **Resource limits** enforcement for Python runtime
- **Pythonnet bridge** and worker resolver for .NET/Python interop

### Changed

- Migrated server bootstrap and tools to FastMCP (v2.x compatibility)
- Switched from pip/poetry to uv for faster dependency management
- Reframed project as DWSIM adapter in three-layer architecture
- Consolidated DWSIM path configuration to single source
- Moved deployment scripts to `scripts/` folder
- Replaced Docker deployment approach with Windows-native deployment guide
- Redesigned physical property model with type-safe architecture
- Switched build backend from poetry to hatchling

### Fixed

- Flash calculation: configured GlobalSettings for headless mode, fixed success detection and data extraction
- Compound handling: correct DWSIM names for isobutane, isopentane, H2S; case-insensitive composition key lookup
- CORS configuration: exposed `Mcp-Session-Id` header, added CORS middleware for browser test harness
- Server stability: prevent console freezing, correct field names in `add_stream`/`add_unit`
- OAuth: discovery endpoint, required_scopes property, syntax error fix, scope requirement removal
- Build: handle paths with parentheses, improve MSBuild detection, correct prebuilt path
- Worker: .NET Framework 4.8 compatibility (use `IndexOf` instead of `Contains`), numeric indices for Vessel ports
- Deployment: reliable session ID extraction, stale process cleanup, env var transport config
- Resource providers: package-relative paths, context parameter compatibility
- Calculation engine: solver initialisation, graphic object naming, UpdateInterface crash resolution, compound database loading from multiple sources
- STA threading for DWSIM COM interop; serialised interop calls on single thread

[Unreleased]: https://github.com/OntoLedgy/ol_dwsim_interop_services/compare/v0.1.0...HEAD
[0.1.1]: https://github.com/OntoLedgy/ol_dwsim_interop_services/compare/v0.1.0...v0.1.1
[0.1.0]: https://github.com/OntoLedgy/ol_dwsim_interop_services/releases/tag/v0.1.0
