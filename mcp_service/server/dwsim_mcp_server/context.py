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

"""Application context and lifespan management for FastMCP."""

from __future__ import annotations

from contextlib import asynccontextmanager
from dataclasses import dataclass
from typing import AsyncIterator

from dwsim_mcp_server.config import ServerSettings
from dwsim_mcp_server.ipc.flowsheet_client import FlowsheetClient
from dwsim_mcp_server.ipc.limited_session_client import LimitedSessionClient
from dwsim_mcp_server.observability.logging import get_logger, start_seq_sink, stop_seq_sink
from dwsim_mcp_server.observability.metrics_server import (
    start_metrics_server,
    stop_metrics_server,
)
from dwsim_mcp_server.observability.stderr_events import emit_event_to_stderr
from dwsim_mcp_server.observability.settings import ObservabilitySettings
from dwsim_mcp_server.release_info import get_release_info
from dwsim_mcp_server.service import FlowsheetService
from dwsim_mcp_server.service.diagnostics_service import DiagnosticsService
from dwsim_mcp_server.services import ThermodynamicsService
from dwsim_mcp_server.services.sensitivity_service import SensitivityService


@dataclass
class AppContext:
    settings: ServerSettings
    session_client: LimitedSessionClient
    flowsheet_client: FlowsheetClient
    flowsheet_service: FlowsheetService
    thermodynamics_service: ThermodynamicsService
    sensitivity_service: SensitivityService
    diagnostics_service: DiagnosticsService


@asynccontextmanager
async def app_lifespan(server) -> AsyncIterator[AppContext]:
    settings = ServerSettings()
    obs_settings = ObservabilitySettings.from_env()
    logger = get_logger(__name__)

    session_client = LimitedSessionClient(settings.resource_limits)
    flowsheet_client = FlowsheetClient(session_client)
    flowsheet_service = FlowsheetService(
        session_client=session_client,
        flowsheet_client=flowsheet_client,
    )
    thermodynamics_service = ThermodynamicsService(
        session_client=session_client,
    )
    sensitivity_service = SensitivityService(
        session_client=session_client,
        flowsheet_client=flowsheet_client,
        allowed_export_roots=settings.case_storage_roots
        if hasattr(settings, "case_storage_roots")
        else [],
    )
    diagnostics_service = DiagnosticsService(
        session_client=session_client,
    )

    app_context = AppContext(
        settings=settings,
        session_client=session_client,
        flowsheet_client=flowsheet_client,
        flowsheet_service=flowsheet_service,
        thermodynamics_service=thermodynamics_service,
        sensitivity_service=sensitivity_service,
        diagnostics_service=diagnostics_service,
    )

    metrics_server = await start_metrics_server() if obs_settings.metrics_enabled else None
    session_client.start_monitoring()
    await start_seq_sink()
    logger.info("server_lifespan_started")
    emit_event_to_stderr(
        event="dwsim_mcp_server_started",
        **get_release_info(),
    )

    try:
        yield app_context
    finally:
        if metrics_server:
            await stop_metrics_server(metrics_server)
        await stop_seq_sink()
        await session_client.stop_monitoring()
        session_client.dispose()
        logger.info("server_lifespan_stopped")
