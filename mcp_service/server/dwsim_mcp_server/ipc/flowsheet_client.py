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

"""pythonnet client for flowsheet operations."""

from __future__ import annotations

import threading
import json
from typing import Any, Dict, List

from dwsim_mcp_server.ipc.clr_loader import load_dwsim_worker
from dwsim_mcp_server.ipc.exceptions import SessionError, map_dotnet_exception
from dwsim_mcp_server.ipc.session_client import _create_logger, _parse_guid
from dwsim_mcp_server.ipc.session_client import SessionClient


class FlowsheetClient:
    """Calls into DwsimWorker.Engine.FlowsheetOperations via pythonnet.

    Uses lazy initialization to create COM objects only when first accessed,
    ensuring they are created on the thread that will use them.
    """

    def __init__(self, session_client: SessionClient) -> None:
        self._session_client = session_client
        self._ops = None
        self._lock = threading.Lock()
        self._init_thread_id = None

    def _ensure_initialized(self) -> None:
        """Ensure FlowsheetOperations is initialized (lazy initialization)."""
        import sys
        if self._ops is None:
            with self._lock:
                if self._ops is None:
                    self._init_thread_id = threading.get_ident()
                    print(f"[FlowsheetClient] Initializing on thread {self._init_thread_id}", file=sys.stderr)
                    worker = load_dwsim_worker()
                    try:
                        flowsheet_ops_cls = worker.Engine.FlowsheetOperations
                    except Exception as exc:  # pragma: no cover - pythonnet import path issues
                        raise map_dotnet_exception(exc, kind="interop") from exc

                    print(f"[FlowsheetClient] Getting session_manager from SessionClient", file=sys.stderr)
                    self._ops = flowsheet_ops_cls(self._session_client.session_manager)
                    print(f"[FlowsheetClient] Initialization complete on thread {self._init_thread_id}", file=sys.stderr)

        # Verify we're on the same thread
        current_thread_id = threading.get_ident()
        if self._init_thread_id is not None and current_thread_id != self._init_thread_id:
            raise RuntimeError(
                f"FlowsheetOperations accessed from wrong thread! "
                f"Initialized on {self._init_thread_id}, accessed from {current_thread_id}"
            )

    def add_compound(self, session_id: str, compound_name: str) -> bool:
        self._ensure_initialized()
        try:
            return bool(self._ops.AddCompound(session_id, compound_name))
        except Exception as exc:
            raise map_dotnet_exception(exc, kind="interop") from exc

    def set_property_package(self, session_id: str, package_name: str, options: Dict[str, str]) -> bool:
        self._ensure_initialized()
        try:
            # options currently ignored on the .NET side; included for parity
            return bool(self._ops.SetPropertyPackage(session_id, package_name))
        except Exception as exc:
            raise map_dotnet_exception(exc, kind="interop") from exc

    def add_stream(
        self,
        session_id: str,
        *,
        name: str,
        temperature: float | None,
        pressure: float | None,
        molar_flow: float | None,
        mass_flow: float | None,
        composition: Dict[str, float],
        phase_hint: str | None,
        is_source: bool = False,
    ) -> str:
        self._ensure_initialized()
        del phase_hint  # not used in current .NET adapter
        if mass_flow is not None and molar_flow is None:
            raise ValueError("mass_flow-only streams not supported; provide molar_flow")
        try:
            # Convert Python dict to .NET Dictionary<string, double>
            from System.Collections.Generic import Dictionary
            from System import String, Double

            net_composition = Dictionary[String, Double]()
            for key, value in composition.items():
                net_composition[key] = float(value)

            return str(
                self._ops.AddStream(
                    session_id,
                    name,
                    temperature or 0.0,
                    pressure or 0.0,
                    molar_flow or 0.0,
                    net_composition,
                    is_source,
                )
            )
        except Exception as exc:
            raise map_dotnet_exception(exc, kind="interop") from exc

    def add_unit(
        self,
        session_id: str,
        *,
        unit_type: str,
        name: str,
        parameters: Dict[str, Any],
    ) -> tuple[str, str]:
        self._ensure_initialized()
        try:
            # Convert Python dict to .NET Dictionary<string, object>
            from System.Collections.Generic import Dictionary
            from System import String, Object

            net_parameters = Dictionary[String, Object]()
            for key, value in parameters.items():
                net_parameters[key] = value

            # Call the .NET method which returns a Tuple<string, string>
            result = self._ops.AddUnit(session_id, unit_type, name, net_parameters)

            # Convert .NET Tuple to Python tuple
            # .NET Tuple properties are Item1, Item2, etc.
            return (str(result.Item1), str(result.Item2))
        except Exception as exc:
            raise map_dotnet_exception(exc, kind="interop") from exc

    def connect(self, session_id: str, *, source_id: str, target_id: str, port_name: str) -> bool:
        self._ensure_initialized()
        try:
            return bool(self._ops.Connect(session_id, source_id, target_id, port_name))
        except Exception as exc:
            raise map_dotnet_exception(exc, kind="interop") from exc

    def list_objects(self, session_id: str) -> Dict[str, Any]:
        self._ensure_initialized()
        try:
            # Get the .NET dictionary result
            result = self._ops.ListObjects(session_id)

            # Manually convert .NET collections to Python
            python_result = {}

            # Convert streams list - use ContainsKey instead of 'in'
            if result.ContainsKey("streams"):
                streams_list = []
                for stream_dict in result["streams"]:
                    py_dict = {str(k): str(v) for k, v in stream_dict.items()}
                    streams_list.append(py_dict)
                python_result["streams"] = streams_list

            # Convert units list
            if result.ContainsKey("units"):
                units_list = []
                for unit_dict in result["units"]:
                    py_dict = {str(k): str(v) for k, v in unit_dict.items()}
                    units_list.append(py_dict)
                python_result["units"] = units_list

            # Convert connections list
            if result.ContainsKey("connections"):
                connections_list = []
                for conn_dict in result["connections"]:
                    py_dict = {str(k): str(v) for k, v in conn_dict.items()}
                    connections_list.append(py_dict)
                python_result["connections"] = connections_list

            return python_result
        except Exception as exc:
            raise map_dotnet_exception(exc, kind="interop") from exc

    def set_object_parameter(
        self,
        session_id: str,
        *,
        object_id: str,
        parameter_name: str,
        value: Any,
    ) -> Any:
        self._ensure_initialized()
        try:
            return self._ops.SetObjectParameter(session_id, object_id, parameter_name, value)
        except Exception as exc:
            raise map_dotnet_exception(exc, kind="interop") from exc

    def delete_object(self, session_id: str, *, object_id: str) -> Dict[str, Any]:
        self._ensure_initialized()
        try:
            return dict(self._ops.DeleteObject(session_id, object_id))
        except Exception as exc:
            raise map_dotnet_exception(exc, kind="interop") from exc

    def flash_stream(self, session_id: str, stream_id: str) -> bool:
        """Flash a stream to compute phase equilibrium.

        This must be called on feed streams before running calculations
        to ensure DWSIM has computed the phase distribution.
        """
        self._ensure_initialized()
        try:
            return bool(self._ops.FlashStream(session_id, stream_id))
        except Exception as exc:
            raise map_dotnet_exception(exc, kind="interop") from exc

    def set_binary_interaction_parameter(
        self,
        session_id: str,
        compound1: str,
        compound2: str,
        value: float,
    ) -> bool:
        """Set a binary interaction parameter (BIP) for a pair of compounds.

        BIPs are critical for accurate phase equilibrium in multi-component systems.
        """
        self._ensure_initialized()
        try:
            return bool(self._ops.SetBinaryInteractionParameter(session_id, compound1, compound2, value))
        except Exception as exc:
            raise map_dotnet_exception(exc, kind="interop") from exc

    def export_csv(
        self,
        session_id: str,
        *,
        file_path: str,
        object_ids: List[str] | None,
    ) -> Dict[str, Any]:
        adapter = self._get_export_adapter(session_id)
        try:
            result = adapter.ExportToCsv(file_path, object_ids)
        except Exception as exc:
            raise map_dotnet_exception(exc, kind="interop") from exc
        if not getattr(result, "Success", False):
            message = getattr(result, "Message", "Export failed.")
            raise SessionError(message)
        data = getattr(result, "Data", None)
        return {
            "success": True,
            "file_path": str(getattr(data, "FilePath", file_path)),
            "row_count": int(getattr(data, "RowCount", 0)),
        }

    def export_json(self, session_id: str, *, format: str) -> Dict[str, Any]:
        adapter = self._get_export_adapter(session_id)
        try:
            result = adapter.ExportToJson(format)
        except Exception as exc:
            raise map_dotnet_exception(exc, kind="interop") from exc
        if not getattr(result, "Success", False):
            message = getattr(result, "Message", "Export failed.")
            raise SessionError(message)
        raw_payload = getattr(result, "Data", "{}")
        try:
            payload = json.loads(str(raw_payload))
        except json.JSONDecodeError:
            payload = {"value": str(raw_payload)}
        return {"data": payload}

    def generate_report(self, session_id: str, *, template: str, file_path: str) -> Dict[str, Any]:
        adapter = self._get_export_adapter(session_id)
        try:
            result = adapter.GenerateReport(template, file_path)
        except Exception as exc:
            raise map_dotnet_exception(exc, kind="interop") from exc
        if not getattr(result, "Success", False):
            message = getattr(result, "Message", "Report generation failed.")
            raise SessionError(message)
        data = getattr(result, "Data", None)
        return {"success": True, "file_path": str(getattr(data, "FilePath", file_path))}

    def save_case(self, session_id: str, *, file_path: str) -> Dict[str, Any]:
        adapter = self._get_export_adapter(session_id)
        try:
            result = adapter.SaveCase(file_path)
        except Exception as exc:
            raise map_dotnet_exception(exc, kind="interop") from exc
        if not getattr(result, "Success", False):
            message = getattr(result, "Message", "Save case failed.")
            raise SessionError(message)
        data = getattr(result, "Data", None)
        return {"success": True, "file_path": str(getattr(data, "FilePath", file_path))}

    def validate_compounds(self, session_id: str, *, compound_names: list[str]) -> Dict[str, Any]:
        self._ensure_initialized()
        context = self._get_session_context(session_id)
        try:
            from DwsimWorker.Adapters import CompoundAdapter  # type: ignore
        except Exception as exc:
            raise map_dotnet_exception(exc, kind="interop") from exc

        adapter = CompoundAdapter(_create_logger(), context)
        results = []
        for name in compound_names:
            result = adapter.ValidateCompound(name)
            canonical = str(getattr(result, "CanonicalName", "") or "")
            results.append(
                {
                    "input_name": str(getattr(result, "InputName", name)),
                    "valid": bool(getattr(result, "Valid", False)),
                    "canonical_name": canonical or None,
                    "alias_used": bool(getattr(result, "AliasUsed", False)),
                    "suggestions": [str(item) for item in getattr(result, "Suggestions", [])],
                }
            )
        return {"results": results}

    def list_compounds(
        self,
        session_id: str,
        *,
        pattern: str | None,
        category: str | None,
        limit: int,
        offset: int,
    ) -> Dict[str, Any]:
        self._ensure_initialized()
        context = self._get_session_context(session_id)
        try:
            from DwsimWorker.Adapters import CompoundAdapter  # type: ignore
        except Exception as exc:
            raise map_dotnet_exception(exc, kind="interop") from exc

        adapter = CompoundAdapter(_create_logger(), context)
        result = adapter.ListCompounds(pattern, category, limit, offset)
        compounds = []
        for item in getattr(result, "Compounds", []) or []:
            compounds.append(
                {
                    "name": str(getattr(item, "Name", "")),
                    "category": str(getattr(item, "Category", "")),
                    "aliases": [str(alias) for alias in getattr(item, "Aliases", [])],
                    "formula": None,
                }
            )
        total_count = int(getattr(result, "TotalCount", len(compounds)))
        result_offset = int(getattr(result, "Offset", offset))
        has_more = (result_offset + len(compounds)) < total_count
        return {"compounds": compounds, "total_count": total_count, "has_more": has_more}

    def _get_session_context(self, session_id: str):
        try:
            guid = _parse_guid(session_id)
            result = self._session_client.session_manager.GetSession(guid)
        except Exception as exc:
            raise map_dotnet_exception(exc, kind="session") from exc

        if not result.Success or result.Data is None:
            message = result.Message or "Session not found."
            raise SessionError(message)

        return result.Data

    def _get_export_adapter(self, session_id: str):
        self._ensure_initialized()
        context = self._get_session_context(session_id)
        try:
            from DwsimWorker.Adapters import ExportAdapter  # type: ignore
        except Exception as exc:
            raise map_dotnet_exception(exc, kind="interop") from exc
        return ExportAdapter(_create_logger(), context)
