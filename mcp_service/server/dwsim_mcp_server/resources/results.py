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

"""Session results resource provider for detailed simulation data."""

from __future__ import annotations

from typing import Any, Dict, List, Optional, TYPE_CHECKING

from mcp import types

from dwsim_mcp_server.models.resources.session_result_resource import SessionResultResource
from dwsim_mcp_server.resources.base import (
    BaseResourceProvider,
    ResourceInvalidStateError,
    ResourceNotFoundError,
)

if TYPE_CHECKING:
    from dwsim_mcp_server.ipc.limited_session_client import LimitedSessionClient


class ResultsProvider(BaseResourceProvider):
    """Resource provider for detailed session simulation results.

    Serves simulation results that are too large for tool responses.
    Results are formatted using CAPE-OPEN standard property names and SI units.

    Resource URIs:
        - resource://session/{sessionId}/results - All objects in session
        - resource://session/{sessionId}/results/{objectId} - Specific object details
    """

    SCHEME = "session"

    # Standard SI units for common properties
    PROPERTY_UNITS: Dict[str, str] = {
        "temperature": "K",
        "pressure": "Pa",
        "molarFlow": "mol/s",
        "massFlow": "kg/s",
        "volumetricFlow": "m³/s",
        "molarEnthalpy": "J/mol",
        "massEnthalpy": "J/kg",
        "molarEntropy": "J/(mol·K)",
        "massEntropy": "J/(kg·K)",
        "molarDensity": "mol/m³",
        "massDensity": "kg/m³",
        "molecularWeight": "kg/mol",
        "vaporFraction": "-",
        "liquidFraction": "-",
        "compressibilityFactor": "-",
        "viscosity": "Pa·s",
        "thermalConductivity": "W/(m·K)",
        "surfaceTension": "N/m",
        "heatCapacityCp": "J/(mol·K)",
        "heatCapacityCv": "J/(mol·K)",
    }

    def __init__(self, session_client: "LimitedSessionClient") -> None:
        """Initialize ResultsProvider.

        Args:
            session_client: Client for accessing session data.
        """
        super().__init__()
        self._session_client = session_client

    def get_resource_templates(self) -> List[types.ResourceTemplate]:
        """Return resource templates for session result URIs."""
        return [
            types.ResourceTemplate(
                uriTemplate="resource://session/{sessionId}/results",
                name="Session Results",
                description="Get all simulation results for a session (streams, units, overall status)",
                mimeType="application/json",
            ),
            types.ResourceTemplate(
                uriTemplate="resource://session/{sessionId}/results/{objectId}",
                name="Object Results",
                description="Get detailed results for a specific stream or unit operation",
                mimeType="application/json",
            ),
        ]

    async def list_resources(self) -> List[types.Resource]:
        """Return all available session result resources.

        Lists resources for all active sessions tracked by the session client.
        """
        resources: List[types.Resource] = []

        # Get active session IDs from tracker
        active_sessions = self._get_active_sessions()

        for session_id in active_sessions:
            resources.append(
                self.create_resource(
                    uri=f"resource://session/{session_id}/results",
                    name=f"Session {session_id[:8]}... Results",
                    description=f"Simulation results for session {session_id}",
                    mime_type="application/json",
                )
            )

        return resources

    async def read_resource(self, uri: str) -> types.ReadResourceResult:
        """Read session result content by URI."""
        scheme, segments = self.parse_uri(uri)

        if scheme != self.SCHEME:
            raise ResourceNotFoundError(
                f"Invalid scheme '{scheme}' for session provider",
                suggestions=["resource://session/{sessionId}/results"],
            )

        if len(segments) < 2 or segments[1] != "results":
            raise ResourceNotFoundError(
                "Invalid session resource path. Expected: resource://session/{sessionId}/results",
                suggestions=["resource://session/{sessionId}/results"],
            )

        session_id = segments[0]
        object_id = segments[2] if len(segments) > 2 else None

        # Validate session exists
        if not self._session_exists(session_id):
            raise ResourceNotFoundError(
                f"Session '{session_id}' not found or expired",
                suggestions=["Create a new session with create_session tool"],
            )

        # Get results
        try:
            results = await self._get_session_results(session_id, object_id)
        except Exception as e:
            self._logger.error(
                "results_fetch_error",
                session_id=session_id,
                object_id=object_id,
                error=str(e),
            )
            raise ResourceInvalidStateError(
                f"Failed to fetch results: {str(e)}. Ensure simulation has been run."
            )

        return self.create_json_result(uri, results)

    def _get_active_sessions(self) -> List[str]:
        """Get list of active session IDs.

        Returns:
            List of session ID strings.
        """
        try:
            # Access session tracker if available
            tracker = getattr(self._session_client, "_session_tracker", None)
            if tracker is not None:
                return list(tracker.get_active_sessions())
            return []
        except Exception as e:
            self._logger.warning("failed_to_get_sessions", error=str(e))
            return []

    def _session_exists(self, session_id: str) -> bool:
        """Check if a session exists.

        Args:
            session_id: Session identifier to check.

        Returns:
            True if session exists and is active.
        """
        try:
            tracker = getattr(self._session_client, "_session_tracker", None)
            if tracker is not None:
                return tracker.is_active(session_id)
            # If no tracker, assume session might exist
            return True
        except Exception:
            return False

    async def _get_session_results(
        self, session_id: str, object_id: Optional[str] = None
    ) -> Dict[str, Any]:
        """Get results for a session.

        Args:
            session_id: Session identifier
            object_id: Optional specific object ID to filter results

        Returns:
            Dict containing result data structured for JSON response.
        """
        # Use session client to get calculation results
        raw_results = await self._session_client.get_calculation_results(
            session_id, object_id=object_id
        )

        # Transform to resource format
        if object_id:
            # Single object result
            return self._format_object_result(session_id, object_id, raw_results)
        else:
            # All objects
            return self._format_all_results(session_id, raw_results)

    def _format_object_result(
        self, session_id: str, object_id: str, raw_result: Dict[str, Any]
    ) -> Dict[str, Any]:
        """Format a single object's results.

        Args:
            session_id: Session identifier
            object_id: Object identifier
            raw_result: Raw result data from session client

        Returns:
            Formatted result dict.
        """
        # Determine object type from result data
        object_type = "stream"  # Default
        if "unit_type" in raw_result or "operation_type" in raw_result:
            object_type = "unit"

        # Extract properties and add units
        properties = {}
        units = {}

        for key, value in raw_result.items():
            if key in ("session_id", "object_id", "object_type", "object_name"):
                continue
            properties[key] = value
            if key in self.PROPERTY_UNITS:
                units[key] = self.PROPERTY_UNITS[key]

        result = SessionResultResource(
            session_id=session_id,
            object_id=object_id,
            object_type=object_type,
            object_name=raw_result.get("name", object_id),
            properties=properties,
            units=units,
            phase_properties=raw_result.get("phase_properties"),
        )

        return result.model_dump()

    def _format_all_results(
        self, session_id: str, raw_results: Dict[str, Any]
    ) -> Dict[str, Any]:
        """Format all session results.

        Args:
            session_id: Session identifier
            raw_results: Raw results from session client

        Returns:
            Formatted results dict with streams and units.
        """
        formatted: Dict[str, Any] = {
            "session_id": session_id,
            "status": raw_results.get("status", "unknown"),
            "convergence_state": raw_results.get("convergence_state", "unknown"),
            "elapsed_ms": raw_results.get("elapsed_ms", 0),
            "streams": [],
            "units": [],
            "messages": raw_results.get("messages", []),
        }

        # Format stream results
        stream_results = raw_results.get("stream_results", [])
        for stream in stream_results:
            stream_data = self._format_stream_result(stream)
            formatted["streams"].append(stream_data)

        # Format unit results if available
        unit_results = raw_results.get("unit_results", [])
        for unit in unit_results:
            unit_data = self._format_unit_result(unit)
            formatted["units"].append(unit_data)

        return formatted

    def _format_stream_result(self, stream: Dict[str, Any]) -> Dict[str, Any]:
        """Format a stream result with units.

        Args:
            stream: Raw stream data

        Returns:
            Formatted stream dict with units.
        """
        result = {
            "object_id": stream.get("stream_id", stream.get("name", "unknown")),
            "name": stream.get("name", "unknown"),
            "object_type": "stream",
            "properties": {},
            "units": {},
            "composition": stream.get("composition", {}),
        }

        # Map common properties
        property_mapping = {
            "temperature": "temperature",
            "pressure": "pressure",
            "molar_flow": "molarFlow",
            "mass_flow": "massFlow",
            "vapor_fraction": "vaporFraction",
            "molar_enthalpy": "molarEnthalpy",
            "molar_entropy": "molarEntropy",
        }

        for raw_key, standard_key in property_mapping.items():
            if raw_key in stream:
                result["properties"][standard_key] = stream[raw_key]
                if standard_key in self.PROPERTY_UNITS:
                    result["units"][standard_key] = self.PROPERTY_UNITS[standard_key]

        # Include phase properties if available
        if "phases" in stream:
            result["phase_properties"] = stream["phases"]

        return result

    def _format_unit_result(self, unit: Dict[str, Any]) -> Dict[str, Any]:
        """Format a unit operation result.

        Args:
            unit: Raw unit data

        Returns:
            Formatted unit dict.
        """
        return {
            "object_id": unit.get("unit_id", unit.get("name", "unknown")),
            "name": unit.get("name", "unknown"),
            "object_type": "unit",
            "unit_type": unit.get("type", "unknown"),
            "properties": unit.get("properties", {}),
            "inlet_streams": unit.get("inlet_streams", []),
            "outlet_streams": unit.get("outlet_streams", []),
        }
