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

"""Sample cases resource provider for DWSIM example flowsheets."""

from __future__ import annotations

import asyncio
import json
from pathlib import Path
from typing import Any, Dict, List, Optional

import aiofiles
from mcp import types

from dwsim_mcp_server.models.resources.sample_case_info import SampleCaseInfo
from dwsim_mcp_server.resources.base import (
    BaseResourceProvider,
    ResourceNotFoundError,
)


class SamplesProvider(BaseResourceProvider):
    """Resource provider for sample DWSIM simulation cases.

    Serves sample case metadata and flowsheet structures from a configured directory.
    Cases can be loaded via the load_case MCP tool.

    Resource URIs:
        - resource://cases - List all available sample cases
        - resource://cases/{name} - Get case metadata
        - resource://cases/{name}/flowsheet - Get flowsheet topology
    """

    SCHEME = "cases"

    def __init__(self, samples_path: str, case_storage_roots: Optional[List[str]] = None) -> None:
        """Initialize SamplesProvider.

        Args:
            samples_path: Path to directory containing sample case files.
            case_storage_roots: Allowed paths for case files (for path validation).
        """
        super().__init__()
        self._samples_path = Path(samples_path)
        self._case_storage_roots = case_storage_roots or []
        self._cases_cache: Optional[Dict[str, SampleCaseInfo]] = None

    def get_resource_templates(self) -> List[types.ResourceTemplate]:
        """Return resource templates for sample case URIs."""
        return [
            types.ResourceTemplate(
                uriTemplate="resource://cases",
                name="Sample Cases Index",
                description="List all available DWSIM sample simulation cases",
                mimeType="application/json",
            ),
            types.ResourceTemplate(
                uriTemplate="resource://cases/{name}",
                name="Sample Case Metadata",
                description="Get metadata for a specific sample case (compounds, units, complexity)",
                mimeType="application/json",
            ),
            types.ResourceTemplate(
                uriTemplate="resource://cases/{name}/flowsheet",
                name="Sample Case Flowsheet",
                description="Get flowsheet topology (streams, units, connections) for a sample case",
                mimeType="application/json",
            ),
        ]

    async def list_resources(self) -> List[types.Resource]:
        """Return all available sample case resources."""
        resources = [
            self.create_resource(
                uri="resource://cases",
                name="Sample Cases Index",
                description="List all available DWSIM sample simulation cases",
                mime_type="application/json",
            )
        ]

        cases = await self._get_available_cases()
        for name, info in cases.items():
            resources.append(
                self.create_resource(
                    uri=f"resource://cases/{name}",
                    name=info.name,
                    description=info.description,
                    mime_type="application/json",
                )
            )
            resources.append(
                self.create_resource(
                    uri=f"resource://cases/{name}/flowsheet",
                    name=f"{info.name} Flowsheet",
                    description=f"Flowsheet topology for {info.name}",
                    mime_type="application/json",
                )
            )

        return resources

    async def read_resource(self, uri: str) -> types.ReadResourceResult:
        """Read sample case content by URI."""
        scheme, segments = self.parse_uri(uri)

        if scheme != self.SCHEME:
            raise ResourceNotFoundError(
                f"Invalid scheme '{scheme}' for cases provider",
                suggestions=["resource://cases", "resource://cases/three-phase-separator"],
            )

        cases = await self._get_available_cases()

        # resource://cases - return index
        if not segments:
            cases_list = [
                {
                    "name": info.name,
                    "description": info.description,
                    "compounds": info.compounds,
                    "unit_operations": info.unit_operations,
                    "complexity": info.complexity,
                }
                for info in cases.values()
            ]
            return self.create_json_result(uri, {"cases": cases_list})

        # resource://cases/{name} or resource://cases/{name}/flowsheet
        case_name = segments[0]

        if case_name not in cases:
            available_names = list(cases.keys())
            raise ResourceNotFoundError(
                f"Sample case '{case_name}' not found",
                suggestions=available_names[:5],
            )

        case_info = cases[case_name]

        # resource://cases/{name} - return metadata
        if len(segments) == 1:
            return self.create_json_result(uri, case_info.model_dump())

        # resource://cases/{name}/flowsheet - return flowsheet topology
        if len(segments) == 2 and segments[1] == "flowsheet":
            flowsheet = await self._get_case_flowsheet(case_info)
            return self.create_json_result(uri, flowsheet)

        raise ResourceNotFoundError(
            f"Unknown resource path: {'/'.join(segments)}",
            suggestions=[f"resource://cases/{case_name}", f"resource://cases/{case_name}/flowsheet"],
        )

    async def _get_available_cases(self) -> Dict[str, SampleCaseInfo]:
        """Get dictionary of available sample cases.

        Returns cached dict or scans samples directory.
        """
        if self._cases_cache is not None:
            return self._cases_cache

        cases: Dict[str, SampleCaseInfo] = {}

        if not self._samples_path.exists():
            self._logger.warning("samples_path_missing", path=str(self._samples_path))
            return cases

        # Scan for case files and metadata
        loop = asyncio.get_event_loop()
        
        # Look for .json metadata files first
        json_files = await loop.run_in_executor(
            None, lambda: list(self._samples_path.glob("*.json"))
        )

        for json_file in json_files:
            try:
                case_info = await self._load_case_metadata(json_file)
                if case_info:
                    cases[case_info.name] = case_info
            except Exception as e:
                self._logger.warning(
                    "case_metadata_load_failed", file=str(json_file), error=str(e)
                )

        # Also scan for .dwxmz files without metadata
        dwxmz_files = await loop.run_in_executor(
            None, lambda: list(self._samples_path.glob("*.dwxmz"))
        )

        for dwxmz_file in dwxmz_files:
            case_name = dwxmz_file.stem
            if case_name not in cases:
                # Create basic metadata from filename
                cases[case_name] = SampleCaseInfo(
                    name=case_name,
                    description=f"DWSIM case file: {dwxmz_file.name}",
                    compounds=[],
                    unit_operations=[],
                    complexity="simple",
                    file_path=str(dwxmz_file),
                )

        self._cases_cache = cases
        self._logger.info("sample_cases_loaded", count=len(cases))
        return cases

    async def _load_case_metadata(self, json_path: Path) -> Optional[SampleCaseInfo]:
        """Load case metadata from JSON file.

        Args:
            json_path: Path to metadata JSON file

        Returns:
            SampleCaseInfo or None if invalid
        """
        try:
            async with aiofiles.open(json_path, "r", encoding="utf-8") as f:
                content = await f.read()
            
            data = json.loads(content)
            
            # Find corresponding .dwxmz file
            case_name = json_path.stem
            dwxmz_path = json_path.with_suffix(".dwxmz")
            
            if not dwxmz_path.exists():
                # Try to use file_path from metadata
                file_path = data.get("file_path", str(dwxmz_path))
            else:
                file_path = str(dwxmz_path)

            return SampleCaseInfo(
                name=data.get("name", case_name),
                description=data.get("description", f"Sample case: {case_name}"),
                compounds=data.get("compounds", []),
                unit_operations=data.get("unit_operations", []),
                complexity=data.get("complexity", "simple"),
                file_path=file_path,
            )

        except json.JSONDecodeError as e:
            self._logger.warning("invalid_json_metadata", file=str(json_path), error=str(e))
            return None
        except Exception as e:
            self._logger.warning("metadata_load_error", file=str(json_path), error=str(e))
            return None

    async def _get_case_flowsheet(self, case_info: SampleCaseInfo) -> Dict[str, Any]:
        """Get flowsheet topology for a case.

        Args:
            case_info: Case metadata

        Returns:
            Dict with streams, units, and connections.

        Note:
            Full flowsheet parsing from .dwxmz files is complex.
            For now, return metadata-based summary. Full implementation
            would require DWSIM library integration.
        """
        # Basic flowsheet representation from metadata
        flowsheet: Dict[str, Any] = {
            "case_name": case_info.name,
            "compounds": case_info.compounds,
            "unit_operations": [
                {"type": op, "name": f"{op}-1"} for op in case_info.unit_operations
            ],
            "streams": [],
            "connections": [],
            "file_path": case_info.file_path,
            "load_instruction": f"Use load_case tool with file_path='{case_info.file_path}' to load this case into a session.",
        }

        # Try to load additional flowsheet data if available
        flowsheet_json = Path(case_info.file_path).with_suffix(".flowsheet.json")
        if flowsheet_json.exists():
            try:
                async with aiofiles.open(flowsheet_json, "r", encoding="utf-8") as f:
                    content = await f.read()
                flowsheet_data = json.loads(content)
                flowsheet.update(flowsheet_data)
            except Exception as e:
                self._logger.debug("flowsheet_json_not_available", error=str(e))

        return flowsheet

    def clear_cache(self) -> None:
        """Clear the cases cache."""
        self._cases_cache = None
        self._logger.debug("samples_cache_cleared")
