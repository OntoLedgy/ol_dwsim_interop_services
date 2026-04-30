# SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
#
# This file is part of the OntoLedgy Thermodynamics Architecture and is
# dual-licensed:
#
#   1. Open source under the GNU Affero General Public License v3.0 or
#      later (AGPL-3.0-or-later). See the LICENSE file in the repository
#      root for the full licence text and NOTICE for attribution.
#   2. Commercial under a separate proprietary licence offered by
#      OntoLedgy Ltd. See COMMERCIAL.md for terms and contact details.
#
# SPDX-License-Identifier: AGPL-3.0-or-later

"""Unit tests for UI resource provider."""

import json
from pathlib import Path

import pytest

from dwsim_mcp_server.resources.base import ResourceInvalidStateError, ResourceNotFoundError
from dwsim_mcp_server.resources.ui_resource_provider import UiResourceProvider


@pytest.fixture
def apps_dir(tmp_path: Path) -> Path:
    apps_path = tmp_path / "apps" / "templates"
    apps_path.mkdir(parents=True)

    # App with app.json and CSP config
    sim_dir = apps_path / "simulation-results"
    sim_dir.mkdir()
    (sim_dir / "index.html").write_text("<html><body>Simulation Results</body></html>")
    (sim_dir / "app.json").write_text(
        json.dumps(
            {
                "name": "simulation-results",
                "title": "Simulation Results",
                "description": "Visualizes simulation output",
                "version": "1.0.0",
                "entryPoint": "index.html",
                "csp": {
                    "connectDomains": ["https://api.example.com"],
                    "resourceDomains": ["https://cdn.example.com"],
                },
            }
        )
    )

    # App without app.json (fallback metadata)
    diag_dir = apps_path / "diagnostics"
    diag_dir.mkdir()
    (diag_dir / "index.html").write_text("<html><body>Diagnostics</body></html>")

    return apps_path


@pytest.fixture
def provider(apps_dir: Path) -> UiResourceProvider:
    return UiResourceProvider(apps_path=str(apps_dir))


class TestUiResourceProviderTemplates:
    def test_returns_templates(self, provider: UiResourceProvider):
        templates = provider.get_resource_templates()
        assert len(templates) >= 2
        uri_templates = [t.uriTemplate for t in templates]
        assert any(t.startswith("ui://dwsim") for t in uri_templates)


class TestUiResourceProviderListResources:
    @pytest.mark.asyncio
    async def test_lists_available_apps(self, provider: UiResourceProvider):
        resources = await provider.list_resources()
        uris = [str(r.uri) for r in resources]
        assert "ui://dwsim/simulation-results" in uris
        assert "ui://dwsim/diagnostics" in uris

    @pytest.mark.asyncio
    async def test_resource_metadata_from_app_json(self, provider: UiResourceProvider):
        resources = await provider.list_resources()
        sim_resource = next(r for r in resources if str(r.uri).endswith("simulation-results"))
        assert sim_resource.name == "Simulation Results"
        assert "Visualizes simulation output" in (sim_resource.description or "")


class TestUiResourceProviderReadResource:
    @pytest.mark.asyncio
    async def test_read_resource_returns_html(self, provider: UiResourceProvider):
        result = await provider.read_resource("ui://dwsim/simulation-results")
        assert result is not None
        assert len(result.contents) == 1
        content = result.contents[0]
        assert "Simulation Results" in content.text
        assert content.mimeType == UiResourceProvider.MIME_TYPE

    @pytest.mark.asyncio
    async def test_read_resource_includes_csp_metadata(self, provider: UiResourceProvider):
        result = await provider.read_resource("ui://dwsim/simulation-results")
        content = result.contents[0]
        assert content.meta is not None
        ui_meta = content.meta.get("ui")
        assert ui_meta is not None
        assert "csp" in ui_meta
        assert "connectDomains" in ui_meta["csp"]

    @pytest.mark.asyncio
    async def test_parameterized_uri_reads_app(self, provider: UiResourceProvider):
        result = await provider.read_resource("ui://dwsim/diagnostics/session-123")
        content = result.contents[0]
        assert "Diagnostics" in content.text

    @pytest.mark.asyncio
    async def test_invalid_scheme_raises_error(self, provider: UiResourceProvider):
        with pytest.raises(ResourceNotFoundError):
            await provider.read_resource("resource://dwsim/diagnostics")

    @pytest.mark.asyncio
    async def test_unknown_app_raises_not_found(self, provider: UiResourceProvider):
        with pytest.raises(ResourceNotFoundError) as exc_info:
            await provider.read_resource("ui://dwsim/unknown-app")
        assert "unknown-app" in exc_info.value.message

    @pytest.mark.asyncio
    async def test_missing_entry_point_raises_invalid_state(self, apps_dir: Path):
        broken_dir = apps_dir / "broken"
        broken_dir.mkdir()
        (broken_dir / "app.json").write_text(
            json.dumps(
                {
                    "name": "broken",
                    "title": "Broken",
                    "description": "Missing entry point",
                    "entryPoint": "missing.html",
                }
            )
        )
        provider = UiResourceProvider(apps_path=str(apps_dir))
        with pytest.raises(ResourceInvalidStateError):
            await provider.read_resource("ui://dwsim/broken")
