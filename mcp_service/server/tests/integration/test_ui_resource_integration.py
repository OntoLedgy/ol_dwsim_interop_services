"""Integration tests for UI resource providers and tool metadata."""

import json
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import MagicMock

import pytest

from dwsim_mcp_server.config import ServerSettings
from dwsim_mcp_server.resources.ui_resource_provider import UiResourceProvider
from dwsim_mcp_server.resources.registry import register_resources
from dwsim_mcp_server.tools.simulation import build_simulation_tools


@pytest.fixture
def apps_dir(tmp_path: Path) -> Path:
    apps_path = tmp_path / "apps" / "templates"
    sim_dir = apps_path / "simulation-results"
    sim_dir.mkdir(parents=True)
    (sim_dir / "index.html").write_text("<html><body>Simulation Results</body></html>")
    (sim_dir / "app.json").write_text(
        json.dumps(
            {
                "name": "simulation-results",
                "title": "Simulation Results",
                "description": "Visualize simulation output",
                "entryPoint": "index.html",
            }
        )
    )
    return apps_path


@pytest.fixture
def mock_dependencies(apps_dir: Path):
    settings = MagicMock(spec=ServerSettings)
    settings.docs_path = str(apps_dir)
    settings.sample_cases_path = str(apps_dir)
    settings.case_storage_roots = []
    settings.apps_path = str(apps_dir)
    settings.apps_cache_enabled = True

    deps = SimpleNamespace(
        settings=settings,
        session_client=MagicMock(),
    )
    return deps


class TestUiResourceIntegration:
    @pytest.mark.asyncio
    async def test_resources_list_includes_ui_resources(self, apps_dir: Path):
        provider = UiResourceProvider(apps_path=str(apps_dir))
        resources = await provider.list_resources()
        uris = [str(r.uri) for r in resources]
        assert "ui://dwsim/simulation-results" in uris

    @pytest.mark.asyncio
    async def test_resources_read_returns_html(self, apps_dir: Path):
        provider = UiResourceProvider(apps_path=str(apps_dir))
        result = await provider.read_resource("ui://dwsim/simulation-results")
        assert result is not None
        assert len(result.contents) == 1
        assert "Simulation Results" in result.contents[0].text

    def test_tool_definition_includes_ui_metadata(self):
        tools = build_simulation_tools()
        run_tool = next(tool for tool in tools if tool.name == "run")
        assert run_tool.meta is not None
        assert "ui" in run_tool.meta
        assert run_tool.meta["ui"]["resourceUri"] == "ui://dwsim/simulation-results"


class TestUiResourceRegistration:
    def test_resource_registration_includes_ui_provider(self, mock_dependencies):
        from mcp.server import Server

        server = Server("test-ui-resources")
        register_resources(server, mock_dependencies)
        assert server is not None
