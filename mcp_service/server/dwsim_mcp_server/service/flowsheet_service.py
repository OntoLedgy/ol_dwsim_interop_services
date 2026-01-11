"""FlowsheetService bridges MCP inputs to pythonnet worker adapters."""

from __future__ import annotations

from typing import Any, Dict, Optional, Protocol, Tuple

from models.mcp_inputs import (
    AddCompoundInput,
    AddCompoundOutput,
    AddStreamInput,
    AddStreamOutput,
    AddUnitInput,
    AddUnitOutput,
    ConnectInput,
    ConnectOutput,
    DeleteObjectInput,
    DeleteObjectOutput,
    ListObjectsInput,
    ListObjectsOutput,
    SetObjectParameterInput,
    SetObjectParameterOutput,
    SetPropertyPackageInput,
    SetPropertyPackageOutput,
)

from dwsim_mcp_server.ipc.limited_session_client import LimitedSessionClient
from dwsim_mcp_server.observability import get_logger


class FlowsheetClientProtocol(Protocol):
    """Protocol describing required flowsheet operations."""

    def add_compound(self, session_id: str, compound_name: str) -> bool: ...

    def set_property_package(self, session_id: str, package_name: str, options: Dict[str, str]) -> bool: ...

    def add_stream(
        self,
        session_id: str,
        *,
        name: str,
        temperature: Optional[float],
        pressure: Optional[float],
        molar_flow: Optional[float],
        mass_flow: Optional[float],
        composition: Dict[str, float],
        phase_hint: Optional[str],
    ) -> str: ...

    def add_unit(
        self,
        session_id: str,
        *,
        unit_type: str,
        name: str,
        parameters: Dict[str, Any],
    ) -> Tuple[str, str]: ...

    def connect(self, session_id: str, *, source_id: str, target_id: str, port_name: str) -> bool: ...

    def list_objects(self, session_id: str) -> Dict[str, Any]: ...

    def set_object_parameter(
        self,
        session_id: str,
        *,
        object_id: str,
        parameter_name: str,
        value: Any,
    ) -> Any: ...

    def delete_object(self, session_id: str, *, object_id: str) -> Dict[str, Any]: ...


class FlowsheetService:
    """Service layer for flowsheet-building MCP tools."""

    def __init__(
        self,
        *,
        session_client: LimitedSessionClient,
        flowsheet_client: FlowsheetClientProtocol,
    ) -> None:
        self._session_client = session_client
        self._client = flowsheet_client
        self._logger = get_logger(__name__)

    async def add_compound(self, payload: AddCompoundInput) -> AddCompoundOutput:
        """Add a compound to the session (idempotent)."""

        def _op() -> bool:
            return bool(self._client.add_compound(payload.session_id, payload.compound_name))

        added = await self._session_client.run_session_operation(payload.session_id, _op)
        self._logger.info(
            "compound_added",
            session_id=payload.session_id,
            compound=payload.compound_name,
            added=bool(added),
        )
        return AddCompoundOutput(compound_name=payload.compound_name, added=bool(added))

    async def set_property_package(self, payload: SetPropertyPackageInput) -> SetPropertyPackageOutput:
        """Apply a property package to the session."""

        def _op() -> bool:
            return bool(
                self._client.set_property_package(
                    payload.session_id,
                    payload.package_name,
                    payload.options,
                )
            )

        applied = await self._session_client.run_session_operation(payload.session_id, _op)
        self._logger.info(
            "property_package_set",
            session_id=payload.session_id,
            package=payload.package_name,
            applied=bool(applied),
        )
        return SetPropertyPackageOutput(package_name=payload.package_name, applied=bool(applied))

    async def add_stream(self, payload: AddStreamInput) -> AddStreamOutput:
        """Create a stream with validated thermodynamic state."""

        def _op() -> str:
            return str(
                self._client.add_stream(
                    payload.session_id,
                    name=payload.name,
                    temperature=payload.temperature,
                    pressure=payload.pressure,
                    molar_flow=payload.molar_flow,
                    mass_flow=payload.mass_flow,
                    composition=payload.composition,
                    phase_hint=payload.phase_hint,
                )
            )

        stream_id = await self._session_client.run_session_operation(payload.session_id, _op)
        self._logger.info(
            "stream_created",
            session_id=payload.session_id,
            stream_id=stream_id,
            name=payload.name,
        )
        return AddStreamOutput(stream_id=stream_id, name=payload.name)

    async def add_unit(self, payload: AddUnitInput) -> AddUnitOutput:
        """Create a unit operation with validated parameters."""

        def _op() -> Tuple[str, str]:
            return self._client.add_unit(
                payload.session_id,
                unit_type=payload.unit_type,
                name=payload.name,
                parameters=payload.parameters,
            )

        unit_id, normalized_type = await self._session_client.run_session_operation(payload.session_id, _op)
        self._logger.info(
            "unit_created",
            session_id=payload.session_id,
            unit_id=unit_id,
            unit_type=normalized_type,
            name=payload.name,
        )
        return AddUnitOutput(unit_id=unit_id, name=payload.name, unit_type=normalized_type)

    async def connect(self, payload: ConnectInput) -> ConnectOutput:
        """Connect two flowsheet objects."""

        def _op() -> bool:
            return bool(
                self._client.connect(
                    payload.session_id,
                    source_id=payload.source_id,
                    target_id=payload.target_id,
                    port_name=payload.port_name,
                )
            )

        connected = await self._session_client.run_session_operation(payload.session_id, _op)
        self._logger.info(
            "objects_connected",
            session_id=payload.session_id,
            source_id=payload.source_id,
            target_id=payload.target_id,
            port_name=payload.port_name,
            connected=bool(connected),
        )
        return ConnectOutput(
            source_id=payload.source_id,
            target_id=payload.target_id,
            port_name=payload.port_name,
            connected=bool(connected),
        )

    async def list_objects(self, payload: ListObjectsInput) -> ListObjectsOutput:
        """List flowsheet objects in the session."""

        def _op() -> Dict[str, Any]:
            return self._client.list_objects(payload.session_id)

        raw_result = await self._session_client.run_session_operation(payload.session_id, _op)
        result = ListObjectsOutput.model_validate(raw_result)
        self._logger.info(
            "objects_listed",
            session_id=payload.session_id,
            stream_count=len(result.streams),
            unit_count=len(result.units),
            connection_count=len(result.connections),
        )
        return result

    async def set_object_parameter(self, payload: SetObjectParameterInput) -> SetObjectParameterOutput:
        """Update a parameter on a flowsheet object."""

        def _op() -> Any:
            return self._client.set_object_parameter(
                payload.session_id,
                object_id=payload.object_id,
                parameter_name=payload.parameter_name,
                value=payload.value,
            )

        result = await self._session_client.run_session_operation(payload.session_id, _op)
        value, previous_value = _normalize_parameter_result(result)
        self._logger.info(
            "object_parameter_set",
            session_id=payload.session_id,
            object_id=payload.object_id,
            parameter_name=payload.parameter_name,
        )
        return SetObjectParameterOutput(
            object_id=payload.object_id,
            parameter_name=payload.parameter_name,
            value=value,
            previous_value=previous_value,
        )

    async def delete_object(self, payload: DeleteObjectInput) -> DeleteObjectOutput:
        """Delete an object and orphaned connections."""

        def _op() -> Dict[str, Any]:
            return self._client.delete_object(payload.session_id, object_id=payload.object_id)

        raw_result = await self._session_client.run_session_operation(payload.session_id, _op)
        result = _normalize_delete_result(raw_result, payload.object_id)
        self._logger.info(
            "object_deleted",
            session_id=payload.session_id,
            object_id=payload.object_id,
            deleted=result.deleted,
            removed_connections=len(result.removed_connections),
        )
        return result


def _normalize_parameter_result(result: Any) -> tuple[Any, Optional[Any]]:
    """Normalize varied backend returns into (value, previous_value)."""
    if isinstance(result, dict):
        return result.get("value"), result.get("previous_value")
    if isinstance(result, tuple) and len(result) == 2:
        return result[0], result[1]
    return result, None


def _normalize_delete_result(raw_result: Dict[str, Any], object_id: str) -> DeleteObjectOutput:
    """Validate and normalize delete_object output."""
    payload = {
        "object_id": raw_result.get("object_id", object_id),
        "deleted": bool(raw_result.get("deleted", False)),
        "removed_connections": raw_result.get("removed_connections", []),
    }
    return DeleteObjectOutput.model_validate(payload)
