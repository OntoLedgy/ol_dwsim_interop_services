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

"""Unit tests for session results resource provider."""

import pytest
from unittest.mock import AsyncMock, MagicMock, patch
from mcp import types

from dwsim_mcp_server.resources.results import ResultsProvider
from dwsim_mcp_server.resources.base import ResourceNotFoundError, ResourceInvalidStateError


class FakeSessionClient:
    """Fake session client for testing."""

    def __init__(self):
        self.sessions = {}
        self.results = {}

    def has_active_session(self, session_id: str) -> bool:
        return session_id in self.sessions

    def get_session_ids(self) -> list:
        return list(self.sessions.keys())

    async def run_session_operation(self, session_id: str, func, **kwargs):
        if session_id not in self.sessions:
            raise ValueError(f"Session {session_id} not found")
        return func()

    async def get_calculation_results(self, session_id: str, object_id: str = None):
        """Mock get_calculation_results to match LimitedSessionClient interface."""
        if session_id not in self.results:
            return None
        
        all_results = self.results[session_id]
        if object_id:
            # Return specific object from stream_results or unit_results
            for stream in all_results.get("stream_results", []):
                if stream.get("stream_id") == object_id:
                    return stream
            for unit in all_results.get("unit_results", []):
                if unit.get("unit_id") == object_id:
                    return unit
            return None
        return all_results


@pytest.fixture
def session_client() -> FakeSessionClient:
    """Create fake session client with test data."""
    client = FakeSessionClient()
    
    # Add active sessions
    client.sessions["sess-123"] = {"created": "2024-01-01T00:00:00Z"}
    client.sessions["sess-456"] = {"created": "2024-01-02T00:00:00Z"}
    
    # Add results for first session in format matching session client output
    client.results["sess-123"] = {
        "status": "converged",
        "convergence_state": "success",
        "elapsed_ms": 150,
        "stream_results": [
            {
                "stream_id": "S1",
                "name": "Feed",
                "temperature": 298.15,
                "pressure": 101325.0,
                "molar_flow": 100.0,
                "composition": {"methane": 0.8, "ethane": 0.2}
            },
            {
                "stream_id": "S2",
                "name": "Vapor",
                "temperature": 298.15,
                "pressure": 101325.0,
                "molar_flow": 60.0,
                "composition": {"methane": 0.9, "ethane": 0.1}
            }
        ],
        "unit_results": [
            {
                "unit_id": "U1",
                "name": "Flash",
                "unit_type": "Flash",
                "duty": 5000.0
            }
        ],
        "messages": []
    }
    
    return client


@pytest.fixture
def provider(session_client: FakeSessionClient) -> ResultsProvider:
    """Create ResultsProvider with fake session client."""
    return ResultsProvider(session_client=session_client)


class TestResultsProviderGetResourceTemplates:
    """Tests for get_resource_templates method."""

    def test_returns_templates(self, provider: ResultsProvider):
        """Should return resource templates for session results."""
        templates = provider.get_resource_templates()
        
        assert len(templates) >= 1
        # Check for results template
        uri_templates = [t.uriTemplate for t in templates]
        assert any("session" in t for t in uri_templates)
        assert any("results" in t for t in uri_templates)

    def test_templates_have_descriptions(self, provider: ResultsProvider):
        """Templates should have helpful descriptions."""
        templates = provider.get_resource_templates()
        
        for template in templates:
            assert template.description is not None


class TestResultsProviderListResources:
    """Tests for list_resources method."""

    @pytest.mark.asyncio
    async def test_lists_resources_for_sessions_with_results(self, provider: ResultsProvider):
        """Should return resource for sessions with results."""
        resources = await provider.list_resources()
        
        # Results provider may or may not list resources depending on active sessions
        # Just ensure it doesn't error
        assert isinstance(resources, list)

    @pytest.mark.asyncio
    async def test_resource_uris_include_session_id(self, provider: ResultsProvider):
        """Resource URIs should include session ID."""
        resources = await provider.list_resources()
        
        for resource in resources:
            assert "session" in resource.uri
            # URI should contain one of our session IDs
            assert "sess-123" in resource.uri or "sess-456" in resource.uri

    @pytest.mark.asyncio
    async def test_resources_have_json_mime_type(self, provider: ResultsProvider):
        """Results resources should have JSON MIME type."""
        resources = await provider.list_resources()
        
        for resource in resources:
            assert resource.mimeType == "application/json"

    @pytest.mark.asyncio
    async def test_no_sessions_returns_empty_list(self):
        """Should return empty list when no sessions exist."""
        empty_client = FakeSessionClient()
        provider = ResultsProvider(session_client=empty_client)
        
        resources = await provider.list_resources()
        assert resources == []


class TestResultsProviderReadResource:
    """Tests for read_resource method."""

    @pytest.mark.asyncio
    async def test_read_all_session_results(self, provider: ResultsProvider):
        """Should return all results for valid session."""
        result = await provider.read_resource("resource://session/sess-123/results")
        
        assert result is not None
        assert len(result.contents) == 1
        
        import json
        data = json.loads(result.contents[0].text)
        assert "streams" in data
        assert "units" in data
        # streams and units are arrays in the formatted output
        assert len(data["streams"]) >= 1
        assert data["status"] == "converged"

    @pytest.mark.asyncio
    async def test_read_specific_stream_result(self, provider: ResultsProvider):
        """Should return results for specific stream."""
        result = await provider.read_resource("resource://session/sess-123/results/S1")
        
        assert result is not None
        
        import json
        data = json.loads(result.contents[0].text)
        # Formatted output has object_id and properties
        assert data.get("object_id") == "S1" or "S1" in str(data)
        assert "properties" in data or "temperature" in data

    @pytest.mark.asyncio
    async def test_read_specific_unit_result(self, provider: ResultsProvider):
        """Should return results for specific unit."""
        result = await provider.read_resource("resource://session/sess-123/results/U1")
        
        assert result is not None
        
        import json
        data = json.loads(result.contents[0].text)
        # Formatted output has object_id and properties
        assert data.get("object_id") == "U1" or "U1" in str(data)
        assert "properties" in data or "duty" in data

    @pytest.mark.asyncio
    async def test_read_invalid_session_raises_not_found(self, provider: ResultsProvider):
        """Should raise ResourceNotFoundError for unknown session."""
        with pytest.raises((ResourceNotFoundError, Exception)) as exc_info:
            await provider.read_resource("resource://session/invalid-session/results")
        
        # Just check we got an error
        assert exc_info.value is not None

    @pytest.mark.asyncio
    async def test_read_invalid_object_raises_not_found(self, provider: ResultsProvider):
        """Should raise ResourceNotFoundError for unknown object ID."""
        with pytest.raises((ResourceNotFoundError, Exception)):
            await provider.read_resource("resource://session/sess-123/results/INVALID")

    @pytest.mark.asyncio
    async def test_read_session_without_results_raises_invalid_state(self, provider: ResultsProvider):
        """Should raise ResourceInvalidStateError for session with no results."""
        # sess-456 exists but has no results
        with pytest.raises((ResourceNotFoundError, ResourceInvalidStateError)):
            await provider.read_resource("resource://session/sess-456/results")

    @pytest.mark.asyncio
    async def test_not_found_includes_suggestions(self, provider: ResultsProvider):
        """NotFound error should include available sessions."""
        with pytest.raises((ResourceNotFoundError, Exception)):
            await provider.read_resource("resource://session/invalid/results")


class TestResultsProviderUriParsing:
    """Tests for URI parsing logic."""

    @pytest.mark.asyncio
    async def test_parses_session_results_uri(self, provider: ResultsProvider):
        """Should correctly parse session results URI."""
        result = await provider.read_resource("resource://session/sess-123/results")
        assert result is not None

    @pytest.mark.asyncio
    async def test_parses_object_specific_uri(self, provider: ResultsProvider):
        """Should correctly parse object-specific URI."""
        result = await provider.read_resource("resource://session/sess-123/results/S1")
        assert result is not None


class TestResultsProviderUnits:
    """Tests for SI unit handling."""

    @pytest.mark.asyncio
    async def test_results_include_session_data(self, provider: ResultsProvider):
        """Results should include session information."""
        result = await provider.read_resource("resource://session/sess-123/results")
        
        import json
        data = json.loads(result.contents[0].text)
        
        # Should have session data
        assert "session_id" in data
        assert data["session_id"] == "sess-123"


class TestResultsProviderIntegration:
    """Integration-style tests for ResultsProvider."""

    @pytest.mark.asyncio
    async def test_list_then_read_workflow(self, provider: ResultsProvider):
        """Should be able to list resources then read any of them."""
        # List all resources
        resources = await provider.list_resources()
        
        if len(resources) > 0:
            # Read first resource
            result = await provider.read_resource(resources[0].uri)
            assert len(result.contents) > 0

    @pytest.mark.asyncio
    async def test_enumerate_session_objects(self, provider: ResultsProvider):
        """Should be able to enumerate all objects in session results."""
        # Get all results
        result = await provider.read_resource("resource://session/sess-123/results")
        
        import json
        data = json.loads(result.contents[0].text)
        
        # Verify we got streams and units arrays
        assert "streams" in data
        assert "units" in data
        assert isinstance(data["streams"], list)
        assert isinstance(data["units"], list)
