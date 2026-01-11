"""pythonnet client for flowsheet operations."""

from __future__ import annotations

from typing import Any, Dict, List

from dwsim_mcp_server.ipc.clr_loader import load_dwsim_worker
from dwsim_mcp_server.ipc.exceptions import map_dotnet_exception
from dwsim_mcp_server.ipc.session_client import SessionClient


class FlowsheetClient:
    """Calls into DwsimWorker.Engine.FlowsheetOperations via pythonnet."""

    def __init__(self, session_client: SessionClient) -> None:
        worker = load_dwsim_worker()
        try:
            flowsheet_ops_cls = worker.Engine.FlowsheetOperations
        except Exception as exc:  # pragma: no cover - pythonnet import path issues
            raise map_dotnet_exception(exc, kind="interop") from exc

        self._ops = flowsheet_ops_cls(session_client.session_manager)

    def add_compound(self, session_id: str, compound_name: str) -> bool:
        try:
            return bool(self._ops.AddCompound(session_id, compound_name))
        except Exception as exc:
            raise map_dotnet_exception(exc, kind="interop") from exc

    def set_property_package(self, session_id: str, package_name: str, options: Dict[str, str]) -> bool:
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
    ) -> str:
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
        try:
            return bool(self._ops.Connect(session_id, source_id, target_id, port_name))
        except Exception as exc:
            raise map_dotnet_exception(exc, kind="interop") from exc

    def list_objects(self, session_id: str) -> Dict[str, Any]:
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
        try:
            return self._ops.SetObjectParameter(session_id, object_id, parameter_name, value)
        except Exception as exc:
            raise map_dotnet_exception(exc, kind="interop") from exc

    def delete_object(self, session_id: str, *, object_id: str) -> Dict[str, Any]:
        try:
            return dict(self._ops.DeleteObject(session_id, object_id))
        except Exception as exc:
            raise map_dotnet_exception(exc, kind="interop") from exc
