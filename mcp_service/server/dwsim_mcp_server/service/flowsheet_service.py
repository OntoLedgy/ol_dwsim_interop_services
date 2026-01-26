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
    ExportCsvInput,
    ExportCsvOutput,
    ExportJsonInput,
    ExportJsonOutput,
    FlashStreamInput,
    FlashStreamOutput,
    GenerateReportInput,
    GenerateReportOutput,
    ListCompoundsInput,
    ListCompoundsOutput,
    ListObjectsInput,
    ListObjectsOutput,
    SaveCaseInput,
    SaveCaseOutput,
    SetBinaryInteractionParameterInput,
    SetBinaryInteractionParameterOutput,
    SetObjectParameterInput,
    SetObjectParameterOutput,
    SetPropertyPackageInput,
    SetPropertyPackageOutput,
    ValidateCompoundsInput,
    ValidateCompoundsOutput,
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
        is_source: bool = False,
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

    def flash_stream(self, session_id: str, stream_id: str) -> bool: ...

    def set_binary_interaction_parameter(
        self,
        session_id: str,
        compound1: str,
        compound2: str,
        value: float,
    ) -> bool: ...

    def export_csv(
        self,
        session_id: str,
        *,
        file_path: str,
        object_ids: Optional[list[str]],
    ) -> Dict[str, Any]: ...

    def export_json(self, session_id: str, *, format: str) -> Dict[str, Any]: ...

    def generate_report(
        self,
        session_id: str,
        *,
        template: str,
        file_path: str,
    ) -> Dict[str, Any]: ...

    def save_case(self, session_id: str, *, file_path: str) -> Dict[str, Any]: ...

    def validate_compounds(self, session_id: str, *, compound_names: list[str]) -> Dict[str, Any]: ...

    def list_compounds(
        self,
        session_id: str,
        *,
        pattern: Optional[str],
        category: Optional[str],
        limit: int,
        offset: int,
    ) -> Dict[str, Any]: ...


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
                    is_source=payload.is_source,
                )
            )

        stream_id = await self._session_client.run_session_operation(payload.session_id, _op)
        self._logger.info(
            "stream_created",
            session_id=payload.session_id,
            stream_id=stream_id,
            name=payload.name,
            is_source=payload.is_source,
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

    async def export_csv(self, payload: ExportCsvInput) -> ExportCsvOutput:
        """Export flowsheet data to CSV."""

        def _op() -> Dict[str, Any]:
            return self._client.export_csv(
                payload.session_id,
                file_path=payload.file_path,
                object_ids=payload.object_ids,
            )

        raw_result = await self._session_client.run_session_operation(payload.session_id, _op)
        result = _normalize_export_csv_result(raw_result, payload.file_path)
        self._logger.info(
            "export_csv_completed",
            session_id=payload.session_id,
            file_path=payload.file_path,
            object_count=0 if payload.object_ids is None else len(payload.object_ids),
            row_count=result.row_count,
            success=bool(result.success),
        )
        return result

    async def export_json(self, payload: ExportJsonInput) -> ExportJsonOutput:
        """Export flowsheet data to JSON."""

        def _op() -> Dict[str, Any]:
            return self._client.export_json(payload.session_id, format=payload.format)

        raw_result = await self._session_client.run_session_operation(payload.session_id, _op)
        result = _normalize_export_json_result(raw_result)
        self._logger.info(
            "export_json_completed",
            session_id=payload.session_id,
            format=payload.format,
            key_count=len(result.data),
        )
        return result

    async def generate_report(self, payload: GenerateReportInput) -> GenerateReportOutput:
        """Generate a report from the flowsheet."""

        def _op() -> Dict[str, Any]:
            return self._client.generate_report(
                payload.session_id,
                template=payload.template,
                file_path=payload.file_path,
            )

        raw_result = await self._session_client.run_session_operation(payload.session_id, _op)
        result = _normalize_report_result(raw_result, payload.file_path)
        self._logger.info(
            "report_generated",
            session_id=payload.session_id,
            template=payload.template,
            file_path=payload.file_path,
            success=bool(result.success),
        )
        return result

    async def save_case(self, payload: SaveCaseInput) -> SaveCaseOutput:
        """Save the current flowsheet case."""

        def _op() -> Dict[str, Any]:
            return self._client.save_case(payload.session_id, file_path=payload.file_path)

        raw_result = await self._session_client.run_session_operation(payload.session_id, _op)
        result = _normalize_save_case_result(raw_result, payload.file_path)
        self._logger.info(
            "case_saved",
            session_id=payload.session_id,
            file_path=payload.file_path,
            success=bool(result.success),
        )
        return result

    async def validate_compounds(self, payload: ValidateCompoundsInput) -> ValidateCompoundsOutput:
        """Validate compound names against available compounds."""

        def _op() -> Dict[str, Any]:
            return self._client.validate_compounds(
                payload.session_id,
                compound_names=payload.compound_names,
            )

        raw_result = await self._session_client.run_session_operation(payload.session_id, _op)
        result = _normalize_validate_compounds_result(raw_result)
        valid_count = sum(1 for entry in result.results if entry.valid)
        self._logger.info(
            "compounds_validated",
            session_id=payload.session_id,
            requested_count=len(payload.compound_names),
            valid_count=valid_count,
        )
        return result

    async def list_compounds(self, payload: ListCompoundsInput) -> ListCompoundsOutput:
        """List available compounds with optional filters."""

        def _op() -> Dict[str, Any]:
            return self._client.list_compounds(
                payload.session_id,
                pattern=payload.pattern,
                category=payload.category,
                limit=payload.limit,
                offset=payload.offset,
            )

        raw_result = await self._session_client.run_session_operation(payload.session_id, _op)
        result = _normalize_list_compounds_result(raw_result)
        self._logger.info(
            "compounds_listed",
            session_id=payload.session_id,
            pattern=payload.pattern,
            category=payload.category,
            returned_count=len(result.compounds),
            total_count=result.total_count,
            has_more=result.has_more,
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

    async def flash_stream(self, payload: FlashStreamInput) -> FlashStreamOutput:
        """Flash a stream to compute phase equilibrium.

        This must be called on feed streams before running calculations.
        """

        def _op() -> bool:
            return bool(self._client.flash_stream(payload.session_id, payload.stream_id))

        flashed = await self._session_client.run_session_operation(payload.session_id, _op)
        self._logger.info(
            "stream_flashed",
            session_id=payload.session_id,
            stream_id=payload.stream_id,
            flashed=bool(flashed),
        )
        return FlashStreamOutput(stream_id=payload.stream_id, flashed=bool(flashed))

    async def set_binary_interaction_parameter(
        self, payload: SetBinaryInteractionParameterInput
    ) -> SetBinaryInteractionParameterOutput:
        """Set a binary interaction parameter for a compound pair."""

        def _op() -> bool:
            return bool(
                self._client.set_binary_interaction_parameter(
                    payload.session_id,
                    payload.compound1,
                    payload.compound2,
                    payload.value,
                )
            )

        applied = await self._session_client.run_session_operation(payload.session_id, _op)
        self._logger.info(
            "bip_set",
            session_id=payload.session_id,
            compound1=payload.compound1,
            compound2=payload.compound2,
            value=payload.value,
            applied=bool(applied),
        )
        return SetBinaryInteractionParameterOutput(
            compound1=payload.compound1,
            compound2=payload.compound2,
            value=payload.value,
            applied=bool(applied),
        )


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


def _normalize_export_csv_result(raw_result: Any, file_path: str) -> ExportCsvOutput:
    """Normalize export_csv output."""
    if isinstance(raw_result, ExportCsvOutput):
        return raw_result
    if isinstance(raw_result, dict):
        return ExportCsvOutput.model_validate(raw_result)
    if isinstance(raw_result, tuple) and len(raw_result) == 2:
        success, row_count = raw_result
        return ExportCsvOutput(success=bool(success), file_path=file_path, row_count=int(row_count))
    if isinstance(raw_result, bool):
        return ExportCsvOutput(success=raw_result, file_path=file_path, row_count=0)
    return ExportCsvOutput(success=False, file_path=file_path, row_count=0)


def _normalize_export_json_result(raw_result: Any) -> ExportJsonOutput:
    """Normalize export_json output."""
    if isinstance(raw_result, ExportJsonOutput):
        return raw_result
    if isinstance(raw_result, dict):
        if "data" in raw_result:
            return ExportJsonOutput.model_validate(raw_result)
        return ExportJsonOutput(data=raw_result)
    return ExportJsonOutput(data={"value": raw_result})


def _normalize_report_result(raw_result: Any, file_path: str) -> GenerateReportOutput:
    """Normalize generate_report output."""
    if isinstance(raw_result, GenerateReportOutput):
        return raw_result
    if isinstance(raw_result, dict):
        return GenerateReportOutput.model_validate(raw_result)
    if isinstance(raw_result, bool):
        return GenerateReportOutput(success=raw_result, file_path=file_path)
    return GenerateReportOutput(success=False, file_path=file_path)


def _normalize_save_case_result(raw_result: Any, file_path: str) -> SaveCaseOutput:
    """Normalize save_case output."""
    if isinstance(raw_result, SaveCaseOutput):
        return raw_result
    if isinstance(raw_result, dict):
        return SaveCaseOutput.model_validate(raw_result)
    if isinstance(raw_result, bool):
        return SaveCaseOutput(success=raw_result, file_path=file_path)
    return SaveCaseOutput(success=False, file_path=file_path)


def _normalize_validate_compounds_result(raw_result: Any) -> ValidateCompoundsOutput:
    """Normalize validate_compounds output."""
    if isinstance(raw_result, ValidateCompoundsOutput):
        return raw_result
    if isinstance(raw_result, dict):
        return ValidateCompoundsOutput.model_validate(raw_result)
    if isinstance(raw_result, list):
        return ValidateCompoundsOutput(results=raw_result)
    return ValidateCompoundsOutput(results=[])


def _normalize_list_compounds_result(raw_result: Any) -> ListCompoundsOutput:
    """Normalize list_compounds output."""
    if isinstance(raw_result, ListCompoundsOutput):
        return raw_result
    if isinstance(raw_result, dict):
        return ListCompoundsOutput.model_validate(raw_result)
    if isinstance(raw_result, tuple) and len(raw_result) == 3:
        compounds, total_count, has_more = raw_result
        return ListCompoundsOutput(
            compounds=compounds,
            total_count=int(total_count),
            has_more=bool(has_more),
        )
    if isinstance(raw_result, list):
        return ListCompoundsOutput(
            compounds=raw_result,
            total_count=len(raw_result),
            has_more=False,
        )
    return ListCompoundsOutput(compounds=[], total_count=0, has_more=False)
