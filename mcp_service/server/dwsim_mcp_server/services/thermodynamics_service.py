"""ThermodynamicsService bridges MCP inputs to pythonnet thermodynamics adapter."""

from __future__ import annotations

from typing import Any, Dict, Iterable, List, Optional, Tuple

from dwsim_mcp_server.models.enums.flash_calculation_types import FlashCalculationTypes
from dwsim_mcp_server.models.enums.phase_types import PhaseTypes
from dwsim_mcp_server.models.enums.physical_quantity_types import PhysicalQuantityTypes
from dwsim_mcp_server.models.mcp_inputs import FlashPHInputs, FlashPSInputs, FlashTPInputs
from dwsim_mcp_server.models.measurements.measurements import Measurements
from dwsim_mcp_server.models.measurements.physical_properties import PhysicalProperties
from dwsim_mcp_server.models.measurements.units_of_measure import UnitsOfMeasure
from dwsim_mcp_server.models.responses import FlashResults, PhaseResults

from dwsim_mcp_server.ipc.clr_loader import load_dwsim_worker
from dwsim_mcp_server.ipc.exceptions import SessionError, map_dotnet_exception
from dwsim_mcp_server.ipc.limited_session_client import LimitedSessionClient
from dwsim_mcp_server.observability import get_logger


_PHYSICAL_PROPERTY_MAP: Dict[str, Tuple[PhysicalQuantityTypes, str]] = {
    "temperature": (PhysicalQuantityTypes.TEMPERATURE, "K"),
    "pressure": (PhysicalQuantityTypes.PRESSURE, "Pa"),
    "density": (PhysicalQuantityTypes.DENSITY, "kg/m3"),
    "viscosity": (PhysicalQuantityTypes.DYNAMIC_VISCOSITY, "Pa*s"),
    "thermal_conductivity": (PhysicalQuantityTypes.THERMAL_CONDUCTIVITY, "W/(m*K)"),
    "molar_heat_capacity_cp": (PhysicalQuantityTypes.MOLAR_HEAT_CAPACITY_CP, "J/(mol*K)"),
    "molar_heat_capacity_cv": (PhysicalQuantityTypes.MOLAR_HEAT_CAPACITY_CV, "J/(mol*K)"),
    "molecular_weight": (PhysicalQuantityTypes.MOLECULAR_WEIGHT, "kg/mol"),
    "compressibility_factor": (PhysicalQuantityTypes.COMPRESSIBILITY_FACTOR, "dimensionless"),
    "gibbs_energy": (PhysicalQuantityTypes.GIBBS_ENERGY, "J/mol"),
    "surface_tension": (PhysicalQuantityTypes.SURFACE_TENSION, "N/m"),
    "molar_flow": (PhysicalQuantityTypes.MOLAR_FLOW_RATE, "mol/s"),
    "mass_flow": (PhysicalQuantityTypes.MASS_FLOW_RATE, "kg/s"),
    "volumetric_flow": (PhysicalQuantityTypes.VOLUMETRIC_FLOW_RATE, "m3/s"),
    "enthalpy": (PhysicalQuantityTypes.MOLAR_ENTHALPY, "J/mol"),
    "molar_enthalpy": (PhysicalQuantityTypes.MOLAR_ENTHALPY, "J/mol"),
    "entropy": (PhysicalQuantityTypes.MOLAR_ENTROPY, "J/(mol*K)"),
    "molar_entropy": (PhysicalQuantityTypes.MOLAR_ENTROPY, "J/(mol*K)"),
    "cp": (PhysicalQuantityTypes.MOLAR_HEAT_CAPACITY_CP, "J/(mol*K)"),
    "cv": (PhysicalQuantityTypes.MOLAR_HEAT_CAPACITY_CV, "J/(mol*K)"),
}


class ThermodynamicsService:
    """Service layer for thermodynamic flash MCP tools."""

    def __init__(self, *, session_client: LimitedSessionClient) -> None:
        self._session_client = session_client
        self._logger = get_logger(__name__)

    async def flash_tp(self, payload: FlashTPInputs) -> FlashResults:
        """Run a temperature-pressure flash calculation."""

        def _op() -> FlashResults:
            self._logger.info(
                "flash_tp_starting",
                session_id=payload.session_id,
                compounds=list(payload.compounds),
                composition=[float(v) for v in payload.composition],
                temperature=float(payload.temperature.value),
                pressure=float(payload.pressure.value),
            )
            adapter = _create_thermodynamics_adapter(self._session_client.session_manager, payload.session_id)
            self._logger.info("flash_tp_adapter_created", adapter_type=type(adapter).__name__)
            dto = adapter.FlashTP(
                list(payload.compounds),
                [float(value) for value in payload.composition],
                float(payload.temperature.value),
                float(payload.pressure.value),
            )
            self._logger.info(
                "flash_tp_dto_returned",
                converged=getattr(dto, "Converged", None),
                temperature=getattr(dto, "TemperatureK", None),
                pressure=getattr(dto, "PressurePa", None),
                phase_count=len(list(getattr(dto, "Phases", None) or [])),
            )
            return _flash_results_from_dto(dto, logger=self._logger)

        results = await self._session_client.run_session_operation(payload.session_id, _op)
        self._logger.info(
            "flash_tp_completed",
            session_id=payload.session_id,
            converged=results.converged,
            phase_count=len(results.phases),
        )
        return results

    async def flash_ph(self, payload: FlashPHInputs) -> FlashResults:
        """Run a pressure-enthalpy flash calculation."""

        def _op() -> FlashResults:
            adapter = _create_thermodynamics_adapter(self._session_client.session_manager, payload.session_id)
            dto = adapter.FlashPH(
                list(payload.compounds),
                [float(value) for value in payload.composition],
                float(payload.pressure.value),
                float(payload.enthalpy.value),
            )
            return _flash_results_from_dto(dto, logger=self._logger)

        results = await self._session_client.run_session_operation(payload.session_id, _op)
        self._logger.info(
            "flash_ph_completed",
            session_id=payload.session_id,
            converged=results.converged,
            phase_count=len(results.phases),
        )
        return results

    async def flash_ps(self, payload: FlashPSInputs) -> FlashResults:
        """Run a pressure-entropy flash calculation."""

        def _op() -> FlashResults:
            adapter = _create_thermodynamics_adapter(self._session_client.session_manager, payload.session_id)
            dto = adapter.FlashPS(
                list(payload.compounds),
                [float(value) for value in payload.composition],
                float(payload.pressure.value),
                float(payload.entropy.value),
            )
            return _flash_results_from_dto(dto, logger=self._logger)

        results = await self._session_client.run_session_operation(payload.session_id, _op)
        self._logger.info(
            "flash_ps_completed",
            session_id=payload.session_id,
            converged=results.converged,
            phase_count=len(results.phases),
        )
        return results


def _create_thermodynamics_adapter(session_manager, session_id: str):
    context = _get_session_context(session_manager, session_id)
    logger = _create_serilog_logger()
    try:
        worker = load_dwsim_worker()
        adapter_type = worker.Adapters.ThermodynamicsAdapter
        return adapter_type(logger, context)
    except Exception as exc:
        raise map_dotnet_exception(exc, kind="interop") from exc


def _get_session_context(session_manager, session_id: str):
    try:
        from System import Guid  # type: ignore
    except Exception as exc:
        raise SessionError("Failed to import System.Guid for session parsing.") from exc

    try:
        guid = Guid.Parse(session_id)
    except Exception as exc:
        raise SessionError(f"Invalid session id: {session_id}") from exc

    try:
        result = session_manager.GetSession(guid)
    except Exception as exc:
        raise map_dotnet_exception(exc, kind="session") from exc

    if not result.Success or result.Data is None:
        message = getattr(result, "Message", None) or "Session not found."
        raise SessionError(message)

    return result.Data


def _create_serilog_logger():
    """Create a Serilog logger for C# adapter diagnostics.
    
    Note: Extension methods like .WriteTo.Console() don't work via pythonnet.
    We use the base LoggerConfiguration which still enables internal C# logging.
    """
    try:
        from Serilog import LoggerConfiguration  # type: ignore
    except Exception as exc:
        raise SessionError("Failed to import Serilog LoggerConfiguration.") from exc
    return LoggerConfiguration().MinimumLevel.Debug().CreateLogger()


def _flash_results_from_dto(dto, *, logger) -> FlashResults:
    calculation_type = _map_calculation_type(_dotnet_to_string(getattr(dto, "CalculationType", "")), logger=logger)
    temperature = _build_physical_property(
        "temperature",
        PhysicalQuantityTypes.TEMPERATURE,
        getattr(dto, "TemperatureK", 0.0),
        "K",
    )
    pressure = _build_physical_property(
        "pressure",
        PhysicalQuantityTypes.PRESSURE,
        getattr(dto, "PressurePa", 0.0),
        "Pa",
    )
    phases = _build_phase_results(getattr(dto, "Phases", None) or [], logger=logger)
    converged = bool(getattr(dto, "Converged", False))
    return FlashResults(
        calculation_type=calculation_type,
        temperature=temperature,
        pressure=pressure,
        converged=converged,
        phases=phases,
        message=None,
        iteration_count=None,
    )


def _build_phase_results(phases: Iterable[Any], *, logger) -> List[PhaseResults]:
    results: List[PhaseResults] = []
    for phase in phases:
        phase_label = _dotnet_to_string(getattr(phase, "PhaseLabel", ""))
        phase_type = _map_phase_label(phase_label, logger=logger)
        fraction = float(getattr(phase, "PhaseFraction", 0.0) or 0.0)
        composition = _build_composition(getattr(phase, "Composition", None) or [])
        properties = _build_phase_properties(getattr(phase, "Properties", None) or {}, logger=logger)
        results.append(
            PhaseResults(
                phase_type=phase_type,
                fraction=fraction,
                composition=composition,
                properties=properties,
            )
        )
    return results


def _build_composition(composition: Iterable[Any]) -> Dict[str, float]:
    output: Dict[str, float] = {}
    for item in composition:
        compound = _dotnet_to_string(getattr(item, "Compound", "")) or None
        if not compound:
            continue
        output[compound] = float(getattr(item, "MoleFraction", 0.0) or 0.0)
    return output


def _build_phase_properties(properties: Any, *, logger) -> List[PhysicalProperties]:
    output: List[PhysicalProperties] = []
    for name, value in _iter_dotnet_dict(properties):
        property_name = _dotnet_to_string(name)
        normalized = _normalize_property_name(property_name)
        mapping = _PHYSICAL_PROPERTY_MAP.get(normalized)
        if mapping is None:
            output.append(PhysicalProperties(name=property_name, measurement=None))
            logger.debug("flash_property_unmapped", property_name=property_name)
            continue
        quantity_type, unit_name = mapping
        output.append(_build_physical_property(property_name, quantity_type, value, unit_name))
    return output


def _build_physical_property(
    name: str,
    quantity_type: PhysicalQuantityTypes,
    value: Any,
    unit_name: str,
) -> PhysicalProperties:
    measurement = _build_measurement(quantity_type, value, unit_name)
    return PhysicalProperties(name=name, measurement=measurement)


def _build_measurement(
    quantity_type: PhysicalQuantityTypes,
    value: Any,
    unit_name: str,
) -> Measurements:
    unit = UnitsOfMeasure(unit_name=str(unit_name), quantity_type=quantity_type)
    return Measurements(quantity_type=quantity_type, value=float(value or 0.0), unit=unit)


def _map_calculation_type(value: str, *, logger) -> FlashCalculationTypes:
    normalized = (value or "").strip().upper()
    try:
        return FlashCalculationTypes(normalized)
    except ValueError:
        logger.warning("unknown_flash_calculation_type", calculation_type=value)
        return FlashCalculationTypes.TEMPERATURE_PRESSURE


def _map_phase_label(label: str, *, logger) -> PhaseTypes:
    normalized = (label or "").strip().lower()
    if normalized in ("vapor", "vapour", "gas"):
        return PhaseTypes.VAPOR
    if normalized in ("liquid", "liquid1", "liq", "l1"):
        return PhaseTypes.LIQUID
    if normalized in ("liquid2", "liq2", "l2"):
        return PhaseTypes.LIQUID2
    if normalized in ("aqueous", "water", "aq"):
        return PhaseTypes.AQUEOUS
    if normalized in ("solid", "sol", "s"):
        return PhaseTypes.SOLID
    if normalized in ("overall", "mixture", "bulk", ""):
        return PhaseTypes.LIQUID
    logger.warning("unknown_phase_label", phase_label=label)
    return PhaseTypes.LIQUID


def _normalize_property_name(name: str) -> str:
    return name.strip().lower().replace(" ", "_").replace("-", "_")


def _dotnet_to_string(value: Any) -> str:
    to_string = getattr(value, "ToString", None)
    return to_string() if callable(to_string) else str(value)


def _iter_dotnet_dict(mapping: Any):
    try:
        for key in mapping.Keys:
            yield key, mapping[key]
        return
    except Exception:
        pass

    try:
        for key, value in mapping.items():
            yield key, value
        return
    except Exception:
        pass

    for entry in mapping or []:
        key = getattr(entry, "Key", None)
        value = getattr(entry, "Value", None)
        if key is not None:
            yield key, value
            continue
        if isinstance(entry, (list, tuple)) and len(entry) == 2:
            yield entry[0], entry[1]
