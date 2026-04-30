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

"""SessionManager wrapper for pythonnet bridge."""

from __future__ import annotations

import threading
from typing import Any, Dict, List, Optional

from dwsim_mcp_server.ipc.clr_loader import load_dwsim_worker
from dwsim_mcp_server.ipc.exceptions import SessionError, map_dotnet_exception


def _ensure_sta_thread() -> None:
    """Ensure current thread is set to STA mode for COM interop.

    Must be called AFTER pythonnet/CLR is loaded, which happens in load_dwsim_worker().
    """
    import sys
    try:
        # Load CLR first if not already loaded
        load_dwsim_worker()

        from System.Threading import ApartmentState, Thread  # type: ignore

        current_state = Thread.CurrentThread.GetApartmentState()
        print(f"[STA] Thread {threading.get_ident()} current state: {current_state}", file=sys.stderr)

        if str(current_state) != "STA":
            Thread.CurrentThread.SetApartmentState(ApartmentState.STA)
            new_state = Thread.CurrentThread.GetApartmentState()
            print(f"[STA] Thread {threading.get_ident()} apartment state set to: {new_state}", file=sys.stderr)
        else:
            print(f"[STA] Thread {threading.get_ident()} already in STA mode", file=sys.stderr)
    except Exception as e:
        print(f"[STA] Failed to set apartment state: {e}", file=sys.stderr)
        # Don't fail - continue and hope it works


class SessionClient:
    """Thin wrapper around DwsimWorker.Engine.SessionManager.

    Uses lazy initialization to create COM objects only when first accessed,
    ensuring they are created on the thread that will use them.
    """

    def __init__(self, default_flowsheet_name: Optional[str] = None) -> None:
        self._default_flowsheet_name = default_flowsheet_name
        self._session_manager = None
        self._logger = None
        self._cape_open_converter = None
        self._lock = threading.Lock()
        self._init_thread_id = None

    def _ensure_initialized(self) -> None:
        """Ensure COM objects are initialized (lazy initialization)."""
        import sys
        if self._session_manager is None:
            with self._lock:
                if self._session_manager is None:
                    self._init_thread_id = threading.get_ident()
                    print(f"[SessionClient] Initializing on thread {self._init_thread_id}", file=sys.stderr)

                    # CRITICAL: Set thread to STA AFTER loading CLR
                    # The ThreadPoolExecutor initializer runs before CLR is loaded, so we do it here
                    _ensure_sta_thread()

                    self._session_manager = self._create_session_manager(self._default_flowsheet_name)
                    self._logger = _create_logger()
                    self._cape_open_converter = _create_cape_open_converter()
                    print(f"[SessionClient] Initialization complete on thread {self._init_thread_id}", file=sys.stderr)

        # Verify we're on the same thread
        current_thread_id = threading.get_ident()
        if self._init_thread_id is not None and current_thread_id != self._init_thread_id:
            raise RuntimeError(
                f"COM object accessed from wrong thread! "
                f"Initialized on {self._init_thread_id}, accessed from {current_thread_id}"
            )

    def create_session(self, flowsheet_name: Optional[str] = None) -> str:
        self._ensure_initialized()
        try:
            result = self._session_manager.CreateSession(flowsheet_name)
        except Exception as exc:
            raise map_dotnet_exception(exc, kind="session") from exc

        if not result.Success:
            message = result.Message or "Failed to create session."
            if result.Error is not None:
                raise map_dotnet_exception(result.Error, kind="session")
            raise SessionError(message)

        session_id = result.Data
        to_string = getattr(session_id, "ToString", None)
        return to_string() if callable(to_string) else str(session_id)

    def close_session(self, session_id: str) -> bool:
        self._ensure_initialized()
        try:
            guid = _parse_guid(session_id)
            result = self._session_manager.CloseSession(guid)
        except Exception as exc:
            raise map_dotnet_exception(exc, kind="session") from exc

        if not result.Success:
            message = result.Message or "Failed to close session."
            if result.Error is not None:
                raise map_dotnet_exception(result.Error, kind="session")
            raise SessionError(message)

        return bool(result.Data)

    def save_case(self, session_id: str, file_path: str) -> bool:
        self._ensure_initialized()
        try:
            guid = _parse_guid(session_id)
            result = self._session_manager.SaveCase(guid, file_path)
        except Exception as exc:
            raise map_dotnet_exception(exc, kind="session") from exc

        if not result.Success:
            message = result.Message or "Failed to save case."
            if result.Error is not None:
                raise map_dotnet_exception(result.Error, kind="session")
            raise SessionError(message)

        return bool(result.Data)

    def load_case(self, session_id: str, file_path: str) -> bool:
        self._ensure_initialized()
        try:
            guid = _parse_guid(session_id)
            result = self._session_manager.LoadCase(guid, file_path)
        except Exception as exc:
            raise map_dotnet_exception(exc, kind="session") from exc

        if not result.Success:
            message = result.Message or "Failed to load case."
            if result.Error is not None:
                raise map_dotnet_exception(result.Error, kind="session")
            raise SessionError(message)

        return bool(result.Data)

    def run_calculation(self, session_id: str, timeout_seconds: Optional[int] = None) -> Dict[str, Any]:
        self._ensure_initialized()
        context = self._get_session_context(session_id)
        adapter = _create_calculation_adapter(self._logger, context)

        try:
            timeout = _to_timespan(timeout_seconds)
            result = adapter.RunCalculation(timeout)
        except Exception as exc:
            raise map_dotnet_exception(exc, kind="interop") from exc

        stream_results = getattr(result, "StreamResults", None)
        if stream_results is None:
            raise SessionError("No streams available for simulation results.")
        count = getattr(stream_results, "Count", None)
        if count is not None and count == 0:
            raise SessionError("No streams available for simulation results.")

        return _calculation_result_to_payload(result, context=context)

    def get_calculation_status(self, session_id: str) -> Dict[str, Any]:
        self._ensure_initialized()
        context = self._get_session_context(session_id)
        adapter = _create_calculation_adapter(self._logger, context)

        try:
            status = adapter.GetCurrentStatus()
        except Exception as exc:
            raise map_dotnet_exception(exc, kind="interop") from exc

        return _status_to_payload(status, context)

    def get_calculation_results(
        self,
        session_id: str,
        *,
        object_id: Optional[str] = None,
    ) -> Dict[str, Any]:
        self._ensure_initialized()
        context = self._get_session_context(session_id)
        adapter = _create_calculation_adapter(self._logger, context)

        try:
            result = adapter.GetCachedResult()
        except Exception as exc:
            raise map_dotnet_exception(exc, kind="interop") from exc

        if result is None:
            raise SessionError("No cached simulation results available.")

        return _calculation_result_to_payload(result, object_id=object_id, context=context)

    def dispose(self) -> None:
        try:
            if self._session_manager is not None:
                self._session_manager.Dispose()
        except Exception as exc:
            raise map_dotnet_exception(exc, kind="session") from exc

    @property
    def session_manager(self):
        """Expose the underlying SessionManager for dependent clients."""
        self._ensure_initialized()
        return self._session_manager

    def _get_session_context(self, session_id: str):
        try:
            guid = _parse_guid(session_id)
            result = self._session_manager.GetSession(guid)
        except Exception as exc:
            raise map_dotnet_exception(exc, kind="session") from exc

        if not result.Success or result.Data is None:
            message = result.Message or "Session not found."
            raise SessionError(message)

        return result.Data

    @staticmethod
    def _create_session_manager(default_flowsheet_name: Optional[str]):
        # Load DwsimWorker assembly first
        worker = load_dwsim_worker()
        try:
            SessionManager = worker.Engine.SessionManager
        except Exception as exc:
            raise SessionError("Failed to import SessionManager.") from exc

        logger = _create_logger()
        config = _create_default_config(default_flowsheet_name)
        return SessionManager(logger, config)


def _parse_guid(value: str):
    try:
        from System import Guid  # type: ignore
    except Exception as exc:
        raise SessionError("Failed to import System.Guid for session parsing.") from exc

    try:
        return Guid.Parse(value)
    except Exception as exc:
        raise SessionError(f"Invalid session id: {value}") from exc


def _create_logger():
    try:
        from Serilog import LoggerConfiguration  # type: ignore
    except Exception as exc:
        raise SessionError("Failed to import Serilog LoggerConfiguration.") from exc

    return LoggerConfiguration().MinimumLevel.Debug().CreateLogger()


def _create_default_config(flowsheet_name: Optional[str]):
    try:
        from DwsimWorker.Engine import FlowsheetContextConfigBuilder  # type: ignore
    except Exception as exc:
        raise SessionError("Failed to import FlowsheetContextConfigBuilder.") from exc

    builder = FlowsheetContextConfigBuilder()
    if flowsheet_name:
        builder = builder.WithFlowsheetName(flowsheet_name)
    return builder.Build()


def _create_cape_open_converter():
    try:
        from DwsimWorker.Converters import CapeOpenConverter  # type: ignore
    except Exception as exc:
        raise SessionError("Failed to import CapeOpenConverter.") from exc

    return CapeOpenConverter()


def _create_calculation_adapter(logger, context):
    try:
        from DwsimWorker.Adapters import CalculationAdapter, StreamAdapter  # type: ignore
    except Exception as exc:
        raise SessionError("Failed to import CalculationAdapter dependencies.") from exc

    stream_adapter = StreamAdapter(logger, context)
    return CalculationAdapter(logger, context, stream_adapter)


def _to_timespan(timeout_seconds: Optional[int]):
    try:
        from System import TimeSpan  # type: ignore
    except Exception as exc:
        raise SessionError("Failed to import System.TimeSpan.") from exc

    if timeout_seconds is None:
        return TimeSpan.Zero
    if timeout_seconds <= 0:
        return TimeSpan.Zero
    return TimeSpan.FromSeconds(float(timeout_seconds))


def _dotnet_to_string(value: Any) -> str:
    to_string = getattr(value, "ToString", None)
    return to_string() if callable(to_string) else str(value)


def _phase_dto_to_dict(phase) -> Dict[str, Any]:
    composition = []
    for item in getattr(phase, "Composition", None) or []:
        composition.append(
            {
                "compound": str(getattr(item, "Compound", "")),
                "mole_fraction": float(getattr(item, "MoleFraction", 0.0)),
            }
        )

    raw_properties = getattr(phase, "Properties", None) or {}
    try:
        properties = {str(k): float(v) for k, v in raw_properties.items()}
    except AttributeError:
        properties = {str(k): float(v) for k, v in raw_properties}

    return {
        "phase_label": str(getattr(phase, "PhaseLabel", "")),
        "phase_fraction": float(getattr(phase, "PhaseFraction", 0.0)),
        "composition": composition,
        "properties": properties,
    }


def _build_stream_results_from_calculation(
    result,
    *,
    object_id: Optional[str] = None,
    compound_names: Optional[List[str]] = None,
) -> List[Dict[str, Any]]:
    stream_results = getattr(result, "StreamResults", None) or []
    outputs: List[Dict[str, Any]] = []
    for stream in stream_results:
        stream_id = _dotnet_to_string(getattr(stream, "StreamId", ""))
        if object_id and stream_id != object_id:
            continue
        phases = _build_phase_results(stream, compound_names=compound_names)
        outputs.append(
            {
                "id": stream_id,
                "name": _dotnet_to_string(getattr(stream, "StreamName", "")),
                "temperature_k": float(getattr(stream, "TemperatureK", 0.0)),
                "pressure_pa": float(getattr(stream, "PressurePa", 0.0)),
                "total_molar_flow_mol_per_s": float(getattr(stream, "MolarFlowMolPerSec", 0.0)),
                "phases": phases,
            }
        )
    if object_id and not outputs:
        raise SessionError(f"Stream '{object_id}' not found in cached results.")
    return outputs


def _build_phase_results(stream, *, compound_names: Optional[List[str]] = None) -> List[Dict[str, Any]]:
    phase_map = getattr(stream, "Phases", None)
    if not phase_map:
        return []

    phases: List[Dict[str, Any]] = []
    for key, phase in _iter_dotnet_dict(phase_map):
        phases.append(_phase_properties_to_dict(key, phase, compound_names or []))
    return phases


def _phase_properties_to_dict(phase_key, phase, compound_names: List[str]) -> Dict[str, Any]:
    phase_name = _dotnet_to_string(getattr(phase, "PhaseName", phase_key))
    phase_fraction = float(getattr(phase, "PhaseFraction", 0.0))

    composition = []
    phase_composition = getattr(phase, "Composition", None)
    mole_fractions = getattr(phase_composition, "MoleFractions", None) or []
    for idx, fraction in enumerate(mole_fractions):
        compound = compound_names[idx] if idx < len(compound_names) else f"component_{idx + 1}"
        composition.append({"compound": str(compound), "mole_fraction": float(fraction)})

    from dwsim_mcp_server.adapter.translators import build_phase_properties_from_dto

    properties = build_phase_properties_from_dto(phase)

    return {
        "phase_label": phase_name,
        "phase_fraction": phase_fraction,
        "composition": composition,
        "properties": properties,
    }


def _iter_dotnet_dict(mapping):
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

    for entry in mapping:
        key = getattr(entry, "Key", None)
        value = getattr(entry, "Value", None)
        if key is not None:
            yield key, value
            continue
        if isinstance(entry, (list, tuple)) and len(entry) == 2:
            yield entry[0], entry[1]


def _calculation_result_to_payload(
    result,
    *,
    object_id: Optional[str] = None,
    context: Optional[Any] = None,
) -> Dict[str, Any]:
    convergence_status = getattr(result, "ConvergenceStatus", None)
    convergence_state = _dotnet_to_string(getattr(convergence_status, "State", "Unknown"))

    if getattr(result, "Success", False) and convergence_state == "Converged":
        status = "converged"
    elif convergence_state == "NotConverged":
        status = "failed"
    elif convergence_state == "Error":
        status = "failed"
    else:
        status = "failed"

    timing = getattr(result, "Timing", None)
    elapsed_ms = float(getattr(timing, "TotalMilliseconds", 0.0) or 0.0)

    messages = []
    for message in getattr(result, "Messages", None) or []:
        messages.append(_dotnet_to_string(message))

    mass_balance = getattr(result, "MassBalance", None)
    mass_balance_valid = None
    mass_balance_error_percent = None
    if mass_balance is not None:
        mass_balance_valid = bool(getattr(mass_balance, "IsValid", False))
        mass_balance_error_percent = float(getattr(mass_balance, "RelativeErrorPercent", 0.0))

    compound_names = _get_compound_names(context) if context is not None else []

    return {
        "status": status,
        "convergence_state": convergence_state,
        "elapsed_ms": elapsed_ms,
        "stream_results": _build_stream_results_from_calculation(
            result,
            object_id=object_id,
            compound_names=compound_names,
        ),
        "messages": messages,
        "mass_balance_valid": mass_balance_valid,
        "mass_balance_error_percent": mass_balance_error_percent,
    }


def _get_compound_names(context) -> List[str]:
    try:
        compounds = list(context.GetCompounds())
        return [str(compound) for compound in compounds]
    except Exception:
        return []


def _status_to_payload(status, context) -> Dict[str, Any]:
    state = _dotnet_to_string(getattr(status, "State", "NotStarted"))
    message = str(getattr(status, "Message", "") or "")

    if state == "InProgress":
        status_code = "running"
        is_running = True
    elif state == "Converged":
        status_code = "converged"
        is_running = False
    elif state == "NotStarted":
        status_code = "idle"
        is_running = False
    else:
        status_code = "failed"
        is_running = False

    if "timeout" in message.lower():
        status_code = "timeout"

    last_run = context.GetLastCalculationTimestamp()
    last_run_timestamp = None
    elapsed_ms = None
    if last_run is not None:
        try:
            last_run_timestamp = last_run.ToString("o")
        except Exception:
            last_run_timestamp = _dotnet_to_string(last_run)

    if is_running and last_run is not None:
        try:
            elapsed_ms = float((_utcnow() - last_run).TotalMilliseconds)
        except Exception:
            elapsed_ms = None

    return {
        "status": status_code,
        "is_running": is_running,
        "last_run_timestamp": last_run_timestamp,
        "elapsed_ms": elapsed_ms,
    }


def _utcnow():
    try:
        from System import DateTime  # type: ignore
    except Exception as exc:
        raise SessionError("Failed to import System.DateTime.") from exc

    return DateTime.UtcNow
