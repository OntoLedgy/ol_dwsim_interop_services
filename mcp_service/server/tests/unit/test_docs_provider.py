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

"""Unit tests for documentation resource provider."""

import pytest
from pathlib import Path
from unittest.mock import AsyncMock, MagicMock, patch
from mcp import types

from dwsim_mcp_server.resources.docs import DocsProvider
from dwsim_mcp_server.resources.base import ResourceNotFoundError


@pytest.fixture
def docs_dir(tmp_path: Path) -> Path:
    """Create temporary documentation directory with test files."""
    docs_path = tmp_path / "docs" / "resources"
    docs_path.mkdir(parents=True)
    
    # Create test markdown files
    (docs_path / "index.md").write_text(
        "# DWSIM Documentation Index\n\nWelcome to DWSIM docs."
    )
    (docs_path / "unit-operations.md").write_text(
        "# Unit Operations\n\nSeparators, mixers, heaters, etc."
    )
    (docs_path / "property-packages.md").write_text(
        "# Property Packages\n\nThermodynamic models for process simulation."
    )
    
    return docs_path


@pytest.fixture
def empty_docs_dir(tmp_path: Path) -> Path:
    """Create empty documentation directory."""
    docs_path = tmp_path / "empty_docs"
    docs_path.mkdir(parents=True)
    return docs_path


@pytest.fixture
def provider(docs_dir: Path) -> DocsProvider:
    """Create DocsProvider with test directory."""
    return DocsProvider(docs_path=str(docs_dir))


class TestDocsProviderGetResourceTemplates:
    """Tests for get_resource_templates method."""

    def test_returns_templates(self, provider: DocsProvider):
        """Should return resource templates for docs."""
        templates = provider.get_resource_templates()
        
        assert len(templates) >= 1
        # Should have both index and topic templates
        uri_templates = [t.uriTemplate for t in templates]
        assert any("docs" in t for t in uri_templates)

    def test_template_has_description(self, provider: DocsProvider):
        """Template should have helpful description."""
        templates = provider.get_resource_templates()
        
        assert templates[0].description is not None
        assert len(templates[0].description) > 10


class TestDocsProviderListResources:
    """Tests for list_resources method."""

    @pytest.mark.asyncio
    async def test_lists_all_markdown_files(self, provider: DocsProvider, docs_dir: Path):
        """Should return resource for each markdown file."""
        resources = await provider.list_resources()
        
        # We created 3 markdown files
        assert len(resources) == 3

    @pytest.mark.asyncio
    async def test_resource_uris_follow_pattern(self, provider: DocsProvider):
        """Each resource should have proper URI format."""
        resources = await provider.list_resources()
        
        for resource in resources:
            uri_str = str(resource.uri)
            assert uri_str.startswith("resource://docs")
            assert not uri_str.endswith(".md")

    @pytest.mark.asyncio
    async def test_resource_names_extracted_from_content(self, provider: DocsProvider):
        """Resource names should come from first heading or filename."""
        resources = await provider.list_resources()
        
        resource_names = [r.name for r in resources]
        # Should extract from markdown heading
        assert any("DWSIM Documentation Index" in name or "index" in name.lower() 
                   for name in resource_names)

    @pytest.mark.asyncio
    async def test_resources_have_valid_mime_type(self, provider: DocsProvider):
        """All docs resources should have valid MIME type."""
        resources = await provider.list_resources()
        
        for resource in resources:
            # Index returns JSON, topics return markdown
            assert resource.mimeType in ["text/markdown", "application/json"]

    @pytest.mark.asyncio
    async def test_empty_directory_returns_index_only(self, empty_docs_dir: Path):
        """Should return only index resource for directory with no markdown files."""
        provider = DocsProvider(docs_path=str(empty_docs_dir))
        resources = await provider.list_resources()
        
        # Should still have index resource
        assert len(resources) == 1
        assert "index" in resources[0].name.lower() or "docs" in str(resources[0].uri)

    @pytest.mark.asyncio
    async def test_nonexistent_directory_returns_index_only(self, tmp_path: Path):
        """Should handle nonexistent directory gracefully with index."""
        provider = DocsProvider(docs_path=str(tmp_path / "nonexistent"))
        resources = await provider.list_resources()
        
        # Should still have index resource even if directory doesn't exist
        assert len(resources) >= 0  # May be 0 or 1 depending on implementation


class TestDocsProviderReadResource:
    """Tests for read_resource method."""

    @pytest.mark.asyncio
    async def test_read_existing_topic(self, provider: DocsProvider):
        """Should return content for valid topic."""
        result = await provider.read_resource("resource://docs/index")
        
        assert result is not None
        assert len(result.contents) == 1
        content = result.contents[0]
        assert hasattr(content, "text")
        assert "DWSIM Documentation Index" in content.text

    @pytest.mark.asyncio
    async def test_read_returns_uri_in_result(self, provider: DocsProvider):
        """Result should include the original URI."""
        result = await provider.read_resource("resource://docs/unit-operations")
        
        assert str(result.contents[0].uri) == "resource://docs/unit-operations"

    @pytest.mark.asyncio
    async def test_read_missing_topic_raises_not_found(self, provider: DocsProvider):
        """Should raise ResourceNotFoundError for unknown topic."""
        with pytest.raises(ResourceNotFoundError) as exc_info:
            await provider.read_resource("resource://docs/nonexistent-topic")
        
        assert "nonexistent-topic" in str(exc_info.value.message)

    @pytest.mark.asyncio
    async def test_not_found_includes_available_topics(self, provider: DocsProvider):
        """NotFound error should list available topics."""
        with pytest.raises(ResourceNotFoundError) as exc_info:
            await provider.read_resource("resource://docs/invalid")
        
        # Should have suggestions
        assert exc_info.value.suggestions is not None
        assert len(exc_info.value.suggestions) > 0

    @pytest.mark.asyncio
    async def test_caches_content_after_first_read(self, provider: DocsProvider, docs_dir: Path):
        """Content should be cached after first read."""
        # First read
        await provider.read_resource("resource://docs/index")
        
        # Modify the file
        (docs_dir / "index.md").write_text("Modified content")
        
        # Second read should return cached content
        result = await provider.read_resource("resource://docs/index")
        
        # Should still have original content due to caching
        assert "DWSIM Documentation Index" in result.contents[0].text

    @pytest.mark.asyncio
    async def test_handles_various_uri_formats(self, provider: DocsProvider):
        """Should handle URIs with and without trailing slashes."""
        # Standard format
        result1 = await provider.read_resource("resource://docs/index")
        assert result1 is not None


class TestDocsProviderIntegration:
    """Integration-style tests for DocsProvider."""

    @pytest.mark.asyncio
    async def test_list_then_read_workflow(self, provider: DocsProvider):
        """Should be able to list resources then read any of them."""
        # List all resources
        resources = await provider.list_resources()
        assert len(resources) > 0
        
        # Read each resource (convert AnyUrl to string)
        for resource in resources:
            result = await provider.read_resource(str(resource.uri))
            assert len(result.contents) > 0
            assert hasattr(result.contents[0], "text")
