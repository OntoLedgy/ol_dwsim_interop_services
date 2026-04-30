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

from __future__ import annotations

import asyncio
from typing import Optional

from aiohttp import web

from dwsim_mcp_server.observability import get_logger
from dwsim_mcp_server.observability.metrics import MetricsSettings, get_metrics_collector


class MetricsServer:
    """Async HTTP server for Prometheus metrics endpoints."""

    def __init__(self, settings: Optional[MetricsSettings] = None) -> None:
        self.settings = settings or MetricsSettings.from_env()
        self._runner: Optional[web.AppRunner] = None
        self._site: Optional[web.TCPSite] = None
        self._app: Optional[web.Application] = None
        self._started = False
        self._logger = get_logger(__name__)

    async def start(self) -> None:
        if self._started:
            return
        if not self.settings.enabled:
            self._logger.info("metrics_disabled")
            return

        self._app = web.Application()
        self._app.router.add_get("/metrics", self._handle_metrics)
        self._app.router.add_get("/health", self._handle_health)

        self._runner = web.AppRunner(self._app)
        await self._runner.setup()
        self._site = web.TCPSite(self._runner, host="0.0.0.0", port=self.settings.port)

        try:
            await self._site.start()
        except OSError as exc:
            self._logger.warning("metrics_server_start_failed", port=self.settings.port, error=str(exc))
            await self._safe_cleanup()
            return
        except Exception as exc:
            self._logger.warning("metrics_server_start_error", port=self.settings.port, error=str(exc))
            await self._safe_cleanup()
            return

        self._started = True
        self._logger.info("metrics_server_started", port=self.settings.port)

    async def stop(self) -> None:
        if not self._started:
            await self._safe_cleanup()
            return
        self._started = False
        await self._safe_cleanup()
        self._logger.info("metrics_server_stopped")

    async def _handle_metrics(self, _request: web.Request) -> web.Response:
        collector = get_metrics_collector()
        collector.record_memory_usage()
        metrics_text = collector.get_metrics_text()
        headers = {"Content-Type": "text/plain; version=0.0.4; charset=utf-8"}
        return web.Response(text=metrics_text, headers=headers)

    async def _handle_health(self, _request: web.Request) -> web.Response:
        return web.Response(text="ok")

    async def _safe_cleanup(self) -> None:
        if self._site is not None:
            await self._site.stop()
            self._site = None
        if self._runner is not None:
            await self._runner.cleanup()
            self._runner = None
        self._app = None


_metrics_server: Optional[MetricsServer] = None
_metrics_lock = asyncio.Lock()


async def start_metrics_server() -> Optional[MetricsServer]:
    global _metrics_server
    async with _metrics_lock:
        if _metrics_server is None:
            _metrics_server = MetricsServer()
        await _metrics_server.start()
        return _metrics_server


async def stop_metrics_server(server: Optional[MetricsServer]) -> None:
    if server is None:
        return
    await server.stop()
