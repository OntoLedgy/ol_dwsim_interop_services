<!--
SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.

SPDX-License-Identifier: AGPL-3.0-or-later
-->

## Live DWSIM integration tests
Enable with:
  uv run pytest tests/integration -m live_dwsim -v
Requires DwsimWorker.dll built (see mcp_service/dwsim_worker/SETUP.md)
and dwsim_binaries/ populated. Tests are skipped by default.
