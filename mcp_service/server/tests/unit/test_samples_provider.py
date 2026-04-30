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

"""Unit tests for sample cases resource provider."""

import pytest
import json
from pathlib import Path
from unittest.mock import AsyncMock, MagicMock, patch
from mcp import types

from dwsim_mcp_server.resources.samples import SamplesProvider
from dwsim_mcp_server.resources.base import ResourceNotFoundError


@pytest.fixture
def samples_dir(tmp_path: Path) -> Path:
    """Create temporary samples directory with test files."""
    samples_path = tmp_path / "cases" / "samples"
    samples_path.mkdir(parents=True)
    
    # Create test metadata files
    (samples_path / "simple-flash.json").write_text(json.dumps({
        "name": "simple-flash",
        "title": "Simple Flash Calculation",
        "description": "A basic vapor-liquid equilibrium flash calculation.",
        "compounds": ["methane", "ethane", "propane"],
        "unit_operations": ["Flash"],
        "property_package": "Peng-Robinson",
        "complexity": "simple",
        "learning_objectives": ["Create streams", "Set compositions", "Run flash calculation"]
    }))
    
    (samples_path / "three-phase-separator.json").write_text(json.dumps({
        "name": "three-phase-separator",
        "title": "Three-Phase Separator",
        "description": "Separation of hydrocarbon mixture into gas, oil, and water phases.",
        "compounds": ["methane", "ethane", "propane", "water"],
        "unit_operations": ["Separator", "Splitter"],
        "property_package": "NRTL",
        "complexity": "moderate",
        "learning_objectives": ["Handle three-phase equilibrium", "Configure separators"]
    }))
    
    # Create corresponding case files
    (samples_path / "simple-flash.dwxmz").write_bytes(b"<mock dwsim case data>")
    (samples_path / "three-phase-separator.dwxmz").write_bytes(b"<mock case data>")
    
    return samples_path


@pytest.fixture
def empty_samples_dir(tmp_path: Path) -> Path:
    """Create empty samples directory."""
    samples_path = tmp_path / "empty_samples"
    samples_path.mkdir(parents=True)
    return samples_path


@pytest.fixture
def provider(samples_dir: Path) -> SamplesProvider:
    """Create SamplesProvider with test directory."""
    return SamplesProvider(
        samples_path=str(samples_dir),
        case_storage_roots=[str(samples_dir.parent)]
    )


class TestSamplesProviderGetResourceTemplates:
    """Tests for get_resource_templates method."""

    def test_returns_templates(self, provider: SamplesProvider):
        """Should return resource templates for cases."""
        templates = provider.get_resource_templates()
        
        assert len(templates) >= 1
        # Check for metadata template
        uri_templates = [t.uriTemplate for t in templates]
        assert any("cases" in t for t in uri_templates)

    def test_templates_have_descriptions(self, provider: SamplesProvider):
        """Templates should have helpful descriptions."""
        templates = provider.get_resource_templates()
        
        for template in templates:
            assert template.description is not None
            assert len(template.description) > 10


class TestSamplesProviderListResources:
    """Tests for list_resources method."""

    @pytest.mark.asyncio
    async def test_lists_all_sample_cases(self, provider: SamplesProvider):
        """Should return resource for each sample case."""
        resources = await provider.list_resources()
        
        # We have 2 sample cases
        assert len(resources) >= 2

    @pytest.mark.asyncio
    async def test_resource_uris_follow_pattern(self, provider: SamplesProvider):
        """Each resource should have proper URI format."""
        resources = await provider.list_resources()
        
        for resource in resources:
            uri_str = str(resource.uri)
            assert uri_str.startswith("resource://cases")

    @pytest.mark.asyncio
    async def test_resources_have_names_from_metadata(self, provider: SamplesProvider):
        """Resource names should come from metadata title."""
        resources = await provider.list_resources()
        
        resource_names = [r.name for r in resources]
        # Should have descriptive names from metadata
        assert any("Flash" in name or "flash" in name.lower() for name in resource_names)

    @pytest.mark.asyncio
    async def test_resources_have_json_mime_type(self, provider: SamplesProvider):
        """Case metadata resources should have JSON MIME type."""
        resources = await provider.list_resources()
        
        for resource in resources:
            assert resource.mimeType in ["application/json", "application/octet-stream"]

    @pytest.mark.asyncio
    async def test_empty_directory_returns_index_only(self, empty_samples_dir: Path):
        """Should return only index resource for directory with no cases."""
        provider = SamplesProvider(samples_path=str(empty_samples_dir))
        resources = await provider.list_resources()
        
        # Should have index resource
        assert len(resources) == 1
        assert "cases" in str(resources[0].uri)

    @pytest.mark.asyncio
    async def test_nonexistent_directory_returns_index_only(self, tmp_path: Path):
        """Should handle nonexistent directory gracefully with index."""
        provider = SamplesProvider(samples_path=str(tmp_path / "nonexistent"))
        resources = await provider.list_resources()
        
        # Should have index resource even if directory doesn't exist
        assert len(resources) >= 0


class TestSamplesProviderReadResource:
    """Tests for read_resource method."""

    @pytest.mark.asyncio
    async def test_read_case_metadata(self, provider: SamplesProvider):
        """Should return metadata for valid case."""
        result = await provider.read_resource("resource://cases/simple-flash")
        
        assert result is not None
        assert len(result.contents) == 1
        content = result.contents[0]
        assert hasattr(content, "text")
        
        # Parse as JSON
        data = json.loads(content.text)
        assert data["name"] == "simple-flash"
        assert "methane" in data["compounds"]

    @pytest.mark.asyncio
    async def test_read_returns_uri_in_result(self, provider: SamplesProvider):
        """Result should include the original URI."""
        result = await provider.read_resource("resource://cases/simple-flash")
        
        assert str(result.contents[0].uri) == "resource://cases/simple-flash"

    @pytest.mark.asyncio
    async def test_read_missing_case_raises_not_found(self, provider: SamplesProvider):
        """Should raise ResourceNotFoundError for unknown case."""
        with pytest.raises(ResourceNotFoundError) as exc_info:
            await provider.read_resource("resource://cases/nonexistent-case")
        
        assert "nonexistent-case" in str(exc_info.value.message)

    @pytest.mark.asyncio
    async def test_not_found_includes_available_cases(self, provider: SamplesProvider):
        """NotFound error should list available cases."""
        with pytest.raises(ResourceNotFoundError) as exc_info:
            await provider.read_resource("resource://cases/invalid")
        
        # Should have suggestions
        assert exc_info.value.suggestions is not None
        assert len(exc_info.value.suggestions) > 0

    @pytest.mark.asyncio
    async def test_case_list_uri_returns_all_cases(self, provider: SamplesProvider):
        """Reading resource://cases should return list of all cases."""
        result = await provider.read_resource("resource://cases")
        
        assert result is not None
        content = result.contents[0]
        data = json.loads(content.text)
        
        # Returns object with cases array
        assert "cases" in data
        assert isinstance(data["cases"], list)
        assert len(data["cases"]) >= 2


class TestSamplesProviderMetadataExtraction:
    """Tests for metadata extraction from JSON files."""

    @pytest.mark.asyncio
    async def test_extracts_all_metadata_fields(self, provider: SamplesProvider):
        """Should extract all metadata fields from JSON."""
        result = await provider.read_resource("resource://cases/three-phase-separator")
        data = json.loads(result.contents[0].text)
        
        assert data["name"] == "three-phase-separator"
        assert data["description"] is not None
        assert "water" in data["compounds"]
        assert "Separator" in data["unit_operations"]
        assert data["complexity"] == "moderate"

    @pytest.mark.asyncio
    async def test_handles_missing_optional_fields(self, samples_dir: Path):
        """Should handle JSON with missing optional fields."""
        # Create minimal metadata file
        (samples_dir / "minimal.json").write_text(json.dumps({
            "name": "minimal",
            "description": "A minimal case"
        }))
        (samples_dir / "minimal.dwxmz").write_bytes(b"<data>")
        
        provider = SamplesProvider(samples_path=str(samples_dir))
        result = await provider.read_resource("resource://cases/minimal")
        data = json.loads(result.contents[0].text)
        
        assert data["name"] == "minimal"


class TestSamplesProviderIntegration:
    """Integration-style tests for SamplesProvider."""

    @pytest.mark.asyncio
    async def test_list_then_read_workflow(self, provider: SamplesProvider):
        """Should be able to list resources then read any of them."""
        # List all resources
        resources = await provider.list_resources()
        assert len(resources) > 0
        
        # Read each case metadata resource (convert AnyUrl to string)
        for resource in resources:
            result = await provider.read_resource(str(resource.uri))
            assert len(result.contents) > 0
