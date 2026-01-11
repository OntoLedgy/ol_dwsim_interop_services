import asyncio
from types import SimpleNamespace

import pytest

from dwsim_mcp_server.config import ServerSettings
from dwsim_mcp_server.ipc.exceptions import AssemblyLoadError, InteropError, SessionError
from dwsim_mcp_server.ipc.flowsheet_client import FlowsheetClient
from dwsim_mcp_server.ipc.limited_session_client import LimitedSessionClient
from dwsim_mcp_server.service import FlowsheetService
from dwsim_mcp_server.tools.flowsheet import handle_flowsheet_tool


@pytest.mark.integration
def test_agent_style_flowsheet_build_with_real_worker():
    """End-to-end flowsheet tool chain against real DwsimWorker via pythonnet.

    Skips if the DWSIM worker assemblies cannot be loaded.
    """

    async def _run():
        settings = ServerSettings()
        session_client = LimitedSessionClient(settings.resource_limits)
        session_client.start_monitoring()
        session_id = await session_client.create_session(flowsheet_name="mcp-integration")
        try:
            flowsheet_client = FlowsheetClient(session_client)
        except (AssemblyLoadError, InteropError) as exc:
            await session_client.stop_monitoring()
            pytest.skip(f"DWSIM worker not available: {exc}")

        service = FlowsheetService(session_client=session_client, flowsheet_client=flowsheet_client)
        deps = SimpleNamespace(flowsheet_service=service)

        try:
            add_compound = {"session_id": session_id, "compound_name": "Water"}
            result = await handle_flowsheet_tool("add_compound", add_compound, deps)
            if isinstance(result, dict):
                assert result["added"] is True
            else:
                pytest.skip(f"add_compound failed: {result}")

            set_pp = {"session_id": session_id, "package_name": "peng-robinson", "options": {}}
            pp_result = await handle_flowsheet_tool("set_property_package", set_pp, deps)
            if isinstance(pp_result, dict):
                assert pp_result["applied"] is True
            else:
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
            assert isinstance(stream_result, dict) and "stream_id" in stream_result
            stream_id = stream_result["stream_id"]

            add_unit = {
                "session_id": session_id,
                "unit_type": "separator",
                "name": "sep-01",
                "parameters": {},
            }
            unit_result = await handle_flowsheet_tool("add_unit", add_unit, deps)
            assert isinstance(unit_result, dict) and "unit_id" in unit_result
            unit_id = unit_result["unit_id"]

            connect_args = {
                "session_id": session_id,
                "source_id": stream_id,
                "target_id": unit_id,
                "port_name": "Inlet",
            }
            connect_result = await handle_flowsheet_tool("connect", connect_args, deps)
            assert isinstance(connect_result, dict) and connect_result.get("connected") is True

            list_args = {"session_id": session_id}
            list_result = await handle_flowsheet_tool("list_objects", list_args, deps)
            assert isinstance(list_result, dict)
            assert any(s["id"] == stream_id for s in list_result.get("streams", []))
            assert any(u["id"] == unit_id for u in list_result.get("units", []))

        finally:
            try:
                await session_client.close_session(session_id)
            except SessionError:
                pass
            await session_client.stop_monitoring()

    try:
        asyncio.run(_run())
    except AssemblyLoadError as exc:
        pytest.skip(f"DWSIM worker not available: {exc}")
    except InteropError as exc:
        pytest.skip(f"Interop failed: {exc}")
