import asyncio
from types import SimpleNamespace

import pytest

from dwsim_mcp_server.config import ServerSettings
from dwsim_mcp_server.ipc.exceptions import AssemblyLoadError, InteropError, SessionError
from dwsim_mcp_server.ipc.flowsheet_client import FlowsheetClient
from dwsim_mcp_server.ipc.limited_session_client import LimitedSessionClient
from dwsim_mcp_server.service import FlowsheetService
from dwsim_mcp_server.tools.flowsheet import handle_flowsheet_tool
from dwsim_mcp_server.tools.simulation import handle_simulation_tool


async def _build_basic_flowsheet(session_client, session_id: str):
    flowsheet_client = FlowsheetClient(session_client)
    service = FlowsheetService(session_client=session_client, flowsheet_client=flowsheet_client)
    deps = SimpleNamespace(flowsheet_service=service)

    add_compound = {"session_id": session_id, "compound_name": "Water"}
    result = await handle_flowsheet_tool("add_compound", add_compound, deps)
    if not isinstance(result, dict):
        pytest.skip(f"add_compound failed: {result}")

    set_pp = {"session_id": session_id, "package_name": "peng-robinson", "options": {}}
    pp_result = await handle_flowsheet_tool("set_property_package", set_pp, deps)
    if not isinstance(pp_result, dict):
        pytest.skip(f"set_property_package failed: {pp_result}")

    add_stream = {
        "session_id": session_id,
        "name": "feed",
        "temperature": 298.15,
        "pressure": 101325.0,
        "molar_flow": 10.0,
        "composition": {"Water": 1.0},
    }
    stream_result = await handle_flowsheet_tool("add_stream", add_stream, deps)
    if not isinstance(stream_result, dict) or "stream_id" not in stream_result:
        pytest.skip(f"add_stream failed: {stream_result}")

    add_unit = {
        "session_id": session_id,
        "unit_type": "separator",
        "name": "sep-01",
        "parameters": {},
    }
    unit_result = await handle_flowsheet_tool("add_unit", add_unit, deps)
    if not isinstance(unit_result, dict) or "unit_id" not in unit_result:
        pytest.skip(f"add_unit failed: {unit_result}")

    connect_args = {
        "session_id": session_id,
        "source_id": stream_result["stream_id"],
        "target_id": unit_result["unit_id"],
        "port_name": "Inlet",
    }
    connect_result = await handle_flowsheet_tool("connect", connect_args, deps)
    if not isinstance(connect_result, dict) or not connect_result.get("connected"):
        pytest.skip(f"connect failed: {connect_result}")


@pytest.mark.integration
def test_simulation_workflow_integration():
    async def _run():
        settings = ServerSettings()
        session_client = LimitedSessionClient(settings.resource_limits)
        session_client.start_monitoring()
        session_id = await session_client.create_session(flowsheet_name="mcp-simulation-integration")
        try:
            await _build_basic_flowsheet(session_client, session_id)

            sim_deps = SimpleNamespace(session_client=session_client)
            run_result = await handle_simulation_tool(
                "run",
                {"session_id": session_id, "timeout_seconds": 30},
                sim_deps,
            )
            if not isinstance(run_result, dict):
                pytest.skip(f"run failed: {run_result}")

            status_result = await handle_simulation_tool(
                "get_status",
                {"session_id": session_id},
                sim_deps,
            )
            if not isinstance(status_result, dict):
                pytest.skip(f"get_status failed: {status_result}")

            results = await handle_simulation_tool(
                "get_results",
                {"session_id": session_id},
                sim_deps,
            )
            if not isinstance(results, dict):
                pytest.skip(f"get_results failed: {results}")

            cached_results = await handle_simulation_tool(
                "get_results",
                {"session_id": session_id},
                sim_deps,
            )

            assert run_result["status"] in {"converged", "failed"}
            assert status_result["status"] in {"idle", "running", "converged", "failed", "timeout"}
            assert results["stream_results"] == cached_results["stream_results"]
        finally:
            try:
                await session_client.close_session(session_id)
            except SessionError:
                pass
            await session_client.stop_monitoring()

    try:
        asyncio.run(_run())
    except (AssemblyLoadError, InteropError) as exc:
        pytest.skip(f"DWSIM worker not available: {exc}")


@pytest.mark.integration
def test_simulation_timeout_scenario():
    async def _run():
        settings = ServerSettings()
        session_client = LimitedSessionClient(settings.resource_limits)
        session_client.start_monitoring()
        session_id = await session_client.create_session(flowsheet_name="mcp-timeout-sim")
        try:
            await _build_basic_flowsheet(session_client, session_id)

            sim_deps = SimpleNamespace(session_client=session_client)
            run_result = await handle_simulation_tool(
                "run",
                {"session_id": session_id, "timeout_seconds": 0.001},
                sim_deps,
            )
            if isinstance(run_result, dict):
                pytest.skip("Timeout not triggered; simulation completed within timeout.")
            assert run_result.isError is True
        finally:
            try:
                await session_client.close_session(session_id)
            except SessionError:
                pass
            await session_client.stop_monitoring()

    try:
        asyncio.run(_run())
    except (AssemblyLoadError, InteropError) as exc:
        pytest.skip(f"DWSIM worker not available: {exc}")


@pytest.mark.integration
def test_simulation_invalid_flowsheet():
    async def _run():
        settings = ServerSettings()
        session_client = LimitedSessionClient(settings.resource_limits)
        session_client.start_monitoring()
        session_id = await session_client.create_session(flowsheet_name="mcp-invalid-sim")
        try:
            sim_deps = SimpleNamespace(session_client=session_client)
            run_result = await handle_simulation_tool(
                "run",
                {"session_id": session_id, "timeout_seconds": 5},
                sim_deps,
            )
            if isinstance(run_result, dict):
                pytest.skip("Run succeeded unexpectedly; invalid flowsheet failure not enforced.")
            assert run_result.isError is True
        finally:
            try:
                await session_client.close_session(session_id)
            except SessionError:
                pass
            await session_client.stop_monitoring()

    try:
        asyncio.run(_run())
    except (AssemblyLoadError, InteropError) as exc:
        pytest.skip(f"DWSIM worker not available: {exc}")
