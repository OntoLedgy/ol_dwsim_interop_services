## Live DWSIM integration tests
Enable with:
  uv run pytest tests/integration -m live_dwsim -v
Requires DwsimWorker.dll built (see mcp_service/dwsim_worker/SETUP.md)
and dwsim_binaries/ populated. Tests are skipped by default.
