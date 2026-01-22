"""Integration tests for MCP resource protocol.

These tests validate the full resource flow through the MCP server
without requiring DWSIM to be installed. They test:
- Resource registration with MCP server
- list_resources returns all categories
- read_resource for docs, samples, and mocked session results
"""

import asyncio
import json
import pytest
from pathlib import Path
from unittest.mock import AsyncMock, MagicMock, patch
from types import SimpleNamespace

from mcp.server import Server
from mcp import types

from dwsim_mcp_server.config import ServerSettings
from dwsim_mcp_server.resources.registry import register_resources
from dwsim_mcp_server.resources.docs import DocsProvider
from dwsim_mcp_server.resources.samples import SamplesProvider
from dwsim_mcp_server.resources.results import ResultsProvider


class MockSessionClient:
    """Mock session client that simulates having active sessions with results."""

    def __init__(self):
        self.sessions = {
            "test-session-001": {"created": "2026-01-22T00:00:00Z"}
        }
        self.results = {
            "test-session-001": {
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
        }

    def has_active_session(self, session_id: str) -> bool:
        return session_id in self.sessions

    def get_session_ids(self) -> list:
        return list(self.sessions.keys())

    async def get_calculation_results(self, session_id: str, object_id: str = None):
        if session_id not in self.results:
            return None
        
        all_results = self.results[session_id]
        if object_id:
            for stream in all_results.get("stream_results", []):
                if stream.get("stream_id") == object_id:
                    return stream
            for unit in all_results.get("unit_results", []):
                if unit.get("unit_id") == object_id:
                    return unit
            return None
        return all_results


@pytest.fixture
def mock_dependencies(tmp_path: Path):
    """Create mock dependencies for resource registration."""
    # Create test docs directory
    docs_path = tmp_path / "docs" / "resources"
    docs_path.mkdir(parents=True)
    (docs_path / "index.md").write_text("# DWSIM Documentation\n\nWelcome to docs.")
    (docs_path / "unit-operations.md").write_text("# Unit Operations\n\nSeparators and more.")
    
    # Create test samples directory
    samples_path = tmp_path / "cases" / "samples"
    samples_path.mkdir(parents=True)
    (samples_path / "test-case.json").write_text(json.dumps({
        "name": "test-case",
        "title": "Test Case",
        "description": "A test simulation case",
        "compounds": ["methane"],
        "unit_operations": ["Flash"],
        "complexity": "simple"
    }))
    (samples_path / "test-case.dwxmz").write_bytes(b"<mock>")
    
    # Create mock settings
    settings = MagicMock(spec=ServerSettings)
    settings.docs_path = str(docs_path)
    settings.sample_cases_path = str(samples_path)
    settings.case_storage_roots = [str(samples_path.parent)]
    
    # Create mock session client
    session_client = MockSessionClient()
    
    # Create dependencies object
    deps = SimpleNamespace(
        settings=settings,
        session_client=session_client,
    )
    
    return deps


@pytest.fixture
def mcp_server_with_resources(mock_dependencies):
    """Create MCP server with resources registered."""
    server = Server("test-dwsim-mcp-server")
    register_resources(server, mock_dependencies)
    return server


class TestResourceRegistration:
    """Tests for resource registration with MCP server."""

    def test_register_resources_succeeds(self, mock_dependencies):
        """Should register resources without errors."""
        server = Server("test-server")
        
        # Should not raise
        register_resources(server, mock_dependencies)

    def test_server_has_list_resources_handler(self, mcp_server_with_resources):
        """Server should have list_resources handler after registration."""
        # The server should have handlers registered
        assert mcp_server_with_resources is not None


class TestListResources:
    """Tests for list_resources across all providers."""

    @pytest.mark.asyncio
    async def test_list_resources_returns_all_categories(self, mock_dependencies):
        """list_resources should return docs, cases, and session resources."""
        # Create providers directly to test
        docs_provider = DocsProvider(
            docs_path=mock_dependencies.settings.docs_path
        )
        samples_provider = SamplesProvider(
            samples_path=mock_dependencies.settings.sample_cases_path,
            case_storage_roots=mock_dependencies.settings.case_storage_roots,
        )
        results_provider = ResultsProvider(
            session_client=mock_dependencies.session_client
        )
        
        # List resources from each
        docs_resources = await docs_provider.list_resources()
        samples_resources = await samples_provider.list_resources()
        results_resources = await results_provider.list_resources()
        
        # Should have docs resources
        assert len(docs_resources) >= 1
        docs_uris = [str(r.uri) for r in docs_resources]
        assert any("docs" in uri for uri in docs_uris)
        
        # Should have samples resources
        assert len(samples_resources) >= 1
        samples_uris = [str(r.uri) for r in samples_resources]
        assert any("cases" in uri for uri in samples_uris)
        
        # Results provider list may be empty without active sessions in fake
        assert isinstance(results_resources, list)

    @pytest.mark.asyncio
    async def test_list_resources_contains_expected_docs(self, mock_dependencies):
        """Should list the documentation topics we created."""
        provider = DocsProvider(docs_path=mock_dependencies.settings.docs_path)
        resources = await provider.list_resources()
        
        # Should have index and unit-operations
        uris = [str(r.uri) for r in resources]
        assert any("index" in uri or "docs" == uri.split("/")[-1] for uri in uris)

    @pytest.mark.asyncio
    async def test_list_resources_contains_sample_cases(self, mock_dependencies):
        """Should list the sample cases we created."""
        provider = SamplesProvider(
            samples_path=mock_dependencies.settings.sample_cases_path,
            case_storage_roots=mock_dependencies.settings.case_storage_roots,
        )
        resources = await provider.list_resources()
        
        # Should have test-case
        uris = [str(r.uri) for r in resources]
        assert any("test-case" in uri or "cases" in uri for uri in uris)


class TestReadResourceDocs:
    """Tests for reading documentation resources."""

    @pytest.mark.asyncio
    async def test_read_docs_index(self, mock_dependencies):
        """Should read the documentation index."""
        provider = DocsProvider(docs_path=mock_dependencies.settings.docs_path)
        
        result = await provider.read_resource("resource://docs")
        
        assert result is not None
        assert len(result.contents) >= 1

    @pytest.mark.asyncio
    async def test_read_docs_specific_topic(self, mock_dependencies):
        """Should read a specific documentation topic."""
        provider = DocsProvider(docs_path=mock_dependencies.settings.docs_path)
        
        result = await provider.read_resource("resource://docs/index")
        
        assert result is not None
        content = result.contents[0]
        assert hasattr(content, "text")
        assert "DWSIM Documentation" in content.text

    @pytest.mark.asyncio
    async def test_read_docs_unit_operations(self, mock_dependencies):
        """Should read unit operations documentation."""
        provider = DocsProvider(docs_path=mock_dependencies.settings.docs_path)
        
        result = await provider.read_resource("resource://docs/unit-operations")
        
        assert result is not None
        content = result.contents[0]
        assert "Unit Operations" in content.text


class TestReadResourceSamples:
    """Tests for reading sample case resources."""

    @pytest.mark.asyncio
    async def test_read_cases_index(self, mock_dependencies):
        """Should read the cases index listing all samples."""
        provider = SamplesProvider(
            samples_path=mock_dependencies.settings.sample_cases_path,
            case_storage_roots=mock_dependencies.settings.case_storage_roots,
        )
        
        result = await provider.read_resource("resource://cases")
        
        assert result is not None
        content = result.contents[0]
        data = json.loads(content.text)
        assert "cases" in data
        assert len(data["cases"]) >= 1

    @pytest.mark.asyncio
    async def test_read_specific_case_metadata(self, mock_dependencies):
        """Should read metadata for a specific case."""
        provider = SamplesProvider(
            samples_path=mock_dependencies.settings.sample_cases_path,
            case_storage_roots=mock_dependencies.settings.case_storage_roots,
        )
        
        result = await provider.read_resource("resource://cases/test-case")
        
        assert result is not None
        content = result.contents[0]
        data = json.loads(content.text)
        assert data["name"] == "test-case"
        assert "methane" in data["compounds"]


class TestReadResourceResults:
    """Tests for reading session result resources."""

    @pytest.mark.asyncio
    async def test_read_session_results(self, mock_dependencies):
        """Should read results for a session with mock data."""
        provider = ResultsProvider(
            session_client=mock_dependencies.session_client
        )
        
        result = await provider.read_resource("resource://session/test-session-001/results")
        
        assert result is not None
        content = result.contents[0]
        data = json.loads(content.text)
        assert data["session_id"] == "test-session-001"
        assert data["status"] == "converged"
        assert len(data["streams"]) >= 1

    @pytest.mark.asyncio
    async def test_read_specific_stream_result(self, mock_dependencies):
        """Should read results for a specific stream."""
        provider = ResultsProvider(
            session_client=mock_dependencies.session_client
        )
        
        result = await provider.read_resource("resource://session/test-session-001/results/S1")
        
        assert result is not None
        content = result.contents[0]
        data = json.loads(content.text)
        # Should have object details
        assert "object_id" in data or "properties" in data

    @pytest.mark.asyncio
    async def test_read_specific_unit_result(self, mock_dependencies):
        """Should read results for a specific unit operation."""
        provider = ResultsProvider(
            session_client=mock_dependencies.session_client
        )
        
        result = await provider.read_resource("resource://session/test-session-001/results/U1")
        
        assert result is not None
        content = result.contents[0]
        data = json.loads(content.text)
        # Should have object details
        assert "object_id" in data or "properties" in data


class TestResourceErrorHandling:
    """Tests for error handling in resource operations."""

    @pytest.mark.asyncio
    async def test_read_nonexistent_doc_topic_error(self, mock_dependencies):
        """Should raise error for nonexistent documentation topic."""
        from dwsim_mcp_server.resources.base import ResourceNotFoundError
        
        provider = DocsProvider(docs_path=mock_dependencies.settings.docs_path)
        
        with pytest.raises(ResourceNotFoundError):
            await provider.read_resource("resource://docs/nonexistent-topic")

    @pytest.mark.asyncio
    async def test_read_nonexistent_case_error(self, mock_dependencies):
        """Should raise error for nonexistent sample case."""
        from dwsim_mcp_server.resources.base import ResourceNotFoundError
        
        provider = SamplesProvider(
            samples_path=mock_dependencies.settings.sample_cases_path,
            case_storage_roots=mock_dependencies.settings.case_storage_roots,
        )
        
        with pytest.raises(ResourceNotFoundError):
            await provider.read_resource("resource://cases/nonexistent-case")

    @pytest.mark.asyncio
    async def test_read_nonexistent_session_error(self, mock_dependencies):
        """Should raise error for nonexistent session."""
        from dwsim_mcp_server.resources.base import ResourceNotFoundError, ResourceError
        
        provider = ResultsProvider(
            session_client=mock_dependencies.session_client
        )
        
        # Should raise some kind of resource error for invalid session
        with pytest.raises((ResourceNotFoundError, ResourceError, Exception)):
            await provider.read_resource("resource://session/invalid-session/results")


class TestResourceTemplates:
    """Tests for resource template discovery."""

    @pytest.mark.asyncio
    async def test_docs_provider_templates(self, mock_dependencies):
        """DocsProvider should return valid templates."""
        provider = DocsProvider(docs_path=mock_dependencies.settings.docs_path)
        templates = provider.get_resource_templates()
        
        assert len(templates) >= 1
        for template in templates:
            assert template.uriTemplate is not None
            assert template.name is not None
            assert "docs" in template.uriTemplate

    @pytest.mark.asyncio
    async def test_samples_provider_templates(self, mock_dependencies):
        """SamplesProvider should return valid templates."""
        provider = SamplesProvider(
            samples_path=mock_dependencies.settings.sample_cases_path,
            case_storage_roots=mock_dependencies.settings.case_storage_roots,
        )
        templates = provider.get_resource_templates()
        
        assert len(templates) >= 1
        for template in templates:
            assert template.uriTemplate is not None
            assert "cases" in template.uriTemplate

    @pytest.mark.asyncio
    async def test_results_provider_templates(self, mock_dependencies):
        """ResultsProvider should return valid templates."""
        provider = ResultsProvider(
            session_client=mock_dependencies.session_client
        )
        templates = provider.get_resource_templates()
        
        assert len(templates) >= 1
        for template in templates:
            assert template.uriTemplate is not None
            assert "session" in template.uriTemplate


class TestEndToEndResourceFlow:
    """End-to-end tests simulating LLM agent resource usage patterns."""

    @pytest.mark.asyncio
    async def test_agent_discovers_and_reads_docs(self, mock_dependencies):
        """Simulate agent discovering and reading documentation."""
        provider = DocsProvider(docs_path=mock_dependencies.settings.docs_path)
        
        # Agent discovers available docs
        resources = await provider.list_resources()
        assert len(resources) >= 1
        
        # Agent reads each discovered resource
        for resource in resources:
            result = await provider.read_resource(str(resource.uri))
            assert result is not None
            assert len(result.contents) >= 1

    @pytest.mark.asyncio
    async def test_agent_discovers_and_reads_sample_cases(self, mock_dependencies):
        """Simulate agent discovering and reading sample cases."""
        provider = SamplesProvider(
            samples_path=mock_dependencies.settings.sample_cases_path,
            case_storage_roots=mock_dependencies.settings.case_storage_roots,
        )
        
        # Agent discovers available cases
        resources = await provider.list_resources()
        assert len(resources) >= 1
        
        # Agent reads the cases index
        index_result = await provider.read_resource("resource://cases")
        assert index_result is not None
        
        # Agent reads specific case metadata
        data = json.loads(index_result.contents[0].text)
        if data.get("cases"):
            case_name = data["cases"][0]["name"]
            case_result = await provider.read_resource(f"resource://cases/{case_name}")
            assert case_result is not None

    @pytest.mark.asyncio
    async def test_agent_reads_session_results_workflow(self, mock_dependencies):
        """Simulate agent reading session results after simulation."""
        provider = ResultsProvider(
            session_client=mock_dependencies.session_client
        )
        
        # Agent reads all results for session
        results = await provider.read_resource("resource://session/test-session-001/results")
        assert results is not None
        
        data = json.loads(results.contents[0].text)
        assert data["status"] == "converged"
        
        # Agent reads specific stream for details
        stream_result = await provider.read_resource(
            "resource://session/test-session-001/results/S1"
        )
        assert stream_result is not None

    @pytest.mark.asyncio
    async def test_full_resource_discovery_workflow(self, mock_dependencies):
        """Test complete resource discovery across all providers."""
        docs_provider = DocsProvider(docs_path=mock_dependencies.settings.docs_path)
        samples_provider = SamplesProvider(
            samples_path=mock_dependencies.settings.sample_cases_path,
            case_storage_roots=mock_dependencies.settings.case_storage_roots,
        )
        results_provider = ResultsProvider(
            session_client=mock_dependencies.session_client
        )
        
        # Get all templates
        all_templates = (
            docs_provider.get_resource_templates() +
            samples_provider.get_resource_templates() +
            results_provider.get_resource_templates()
        )
        
        # Should have templates for all three categories
        template_uris = [t.uriTemplate for t in all_templates]
        assert any("docs" in uri for uri in template_uris)
        assert any("cases" in uri for uri in template_uris)
        assert any("session" in uri for uri in template_uris)
        
        # Get all resources
        all_resources = (
            await docs_provider.list_resources() +
            await samples_provider.list_resources() +
            await results_provider.list_resources()
        )
        
        # Should have resources from docs and samples at minimum
        resource_uris = [str(r.uri) for r in all_resources]
        assert any("docs" in uri for uri in resource_uris)
        assert any("cases" in uri for uri in resource_uris)
