<!--
SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
SPDX-License-Identifier: AGPL-3.0-or-later
-->

# CI workflows

This directory contains the GitHub Actions workflows that gate and ship
the DWSIM MCP server. The current set is intentionally small: each
workflow has a single trigger and a single audience.

## Workflow inventory

| Workflow | Trigger | Audience | Purpose |
| --- | --- | --- | --- |
| [`ci.yml`](ci.yml) | PR to `develop`/`main`; push to `develop` | Contributors | Lint + Python tests (3.11, 3.12) + C# build + Windows integration tests. Single status check for branch protection. |
| [`reuse.yml`](reuse.yml) | PR or push to `develop`/`main` | Maintainers | REUSE/SPDX licence header compliance. Runs in parallel to `ci.yml`. |
| [`build-dwsim-worker.yml`](build-dwsim-worker.yml) | Tag push `v*-beta` | Route B beta testers | Builds `DwsimWorker.dll`, packages it as `DwsimWorker.zip`, and publishes a prerelease GitHub release asset (DIS-28). Not a redundant copy of `ci.yml::build-csharp` — different trigger, different output. |
| [`release.yml`](release.yml) | Tag push `v*.*.*` | End users | Builds the C# worker, packages prebuilt binaries via `scripts/package_prebuilt.py`, builds the Python wheel, publishes to PyPI via OIDC trusted publishing, and creates a GitHub Release with wheel + worker zip + source archive. |

## Composite action

[`./../actions/setup-dwsim`](../actions/setup-dwsim/action.yml) installs
.NET, MSBuild, the NuGet cache, the cached DWSIM binaries, and writes
`mcp_service/dwsim_worker/dwsim.config.json`. Every Windows job that
runs `build.bat` consumes it. Update the DWSIM binaries URL or cache
key once in `action.yml` and it propagates to all four workflows.

## Required local state for `uv sync --dev`

`mcp_service/server/pyproject.toml` declares a hatch
`force-include` rule that copies `prebuilt/DwsimWorker/` into the
wheel. Hatch resolves that path even for editable installs, so the
directory must exist before any `uv sync --dev` call.

The directory is otherwise produced by `scripts/package_prebuilt.py`
during `release.yml` only. To keep `ci.yml` working on a fresh clone
we ship a tracked placeholder at
`mcp_service/server/prebuilt/DwsimWorker/.gitkeep` and ignore everything
else under that directory (see [`.gitignore`](../../.gitignore)). The
release pipeline overwrites the placeholder directory with real
binaries before the wheel build runs, so end users still get the bundled
`DwsimWorker.dll`. Context: DIS-137.

## Branch protection (recommended)

To require green CI before merge, configure the following required
status checks on `develop` and `main` in GitHub repository settings:

- `Lint (Python)`
- `Test (Python 3.11)`
- `Test (Python 3.12)`
- `Build (C#)`
- `Integration (Full Stack)`
- `reuse lint`

The first five come from `ci.yml`; the last from `reuse.yml`.
