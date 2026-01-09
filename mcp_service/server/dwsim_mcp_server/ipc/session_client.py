"""SessionManager wrapper for pythonnet bridge."""

from __future__ import annotations

from typing import Optional

from dwsim_mcp_server.ipc.clr_loader import load_dwsim_worker
from dwsim_mcp_server.ipc.exceptions import SessionError, map_dotnet_exception


class SessionClient:
    """Thin wrapper around DwsimWorker.Engine.SessionManager."""

    def __init__(self, default_flowsheet_name: Optional[str] = None) -> None:
        self._dwsim_worker = load_dwsim_worker()
        self._session_manager = self._create_session_manager(default_flowsheet_name)

    def create_session(self, flowsheet_name: Optional[str] = None) -> str:
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

    def dispose(self) -> None:
        try:
            if self._session_manager is not None:
                self._session_manager.Dispose()
        except Exception as exc:
            raise map_dotnet_exception(exc, kind="session") from exc

    @staticmethod
    def _create_session_manager(default_flowsheet_name: Optional[str]):
        try:
            from DwsimWorker.Engine import SessionManager  # type: ignore
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
