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

"""UI resource provider for MCP app templates."""

from __future__ import annotations

import asyncio
import json
from pathlib import Path
from typing import Dict, List, Optional, Tuple
from urllib.parse import urlparse

import aiofiles
from mcp import types
from pydantic import ValidationError

from dwsim_mcp_server.models.resources.ui_resource_metadata import AppConfig
from dwsim_mcp_server.resources.base import (
    BaseResourceProvider,
    ResourceInvalidStateError,
    ResourceNotFoundError,
)


class UiResourceProvider(BaseResourceProvider):
    """Resource provider for MCP UI apps.

    Serves HTML app bundles from the configured apps directory.
    Resource URIs:
        - ui://dwsim/{app-name}
        - ui://dwsim/{app-name}/{param}
    """

    SCHEME = "ui"
    AUTHORITY = "dwsim"
    MIME_TYPE = "text/html;profile=mcp-app"

    def __init__(self, apps_path: str, cache_enabled: bool = True) -> None:
        """Initialize UiResourceProvider.

        Args:
            apps_path: Path to the apps templates directory.
            cache_enabled: Whether to cache app configs and HTML content.
        """
        super().__init__()
        self._apps_path = Path(apps_path)
        self._cache_enabled = cache_enabled
        self._app_cache: Dict[str, AppConfig] = {}
        self._html_cache: Dict[str, str] = {}
        self._apps_index: Optional[List[AppConfig]] = None

    def get_resource_templates(self) -> List[types.ResourceTemplate]:
        """Return resource templates for UI app URIs."""
        return [
            types.ResourceTemplate(
                uriTemplate="ui://dwsim/{app}",
                name="UI App",
                description="Load a DWSIM UI app by name (e.g., simulation-results)",
                mimeType=self.MIME_TYPE,
            ),
            types.ResourceTemplate(
                uriTemplate="ui://dwsim/{app}/{param}",
                name="UI App (parameterized)",
                description="Load a DWSIM UI app with an additional parameter segment",
                mimeType=self.MIME_TYPE,
            ),
        ]

    async def list_resources(self) -> List[types.Resource]:
        """Return all available UI app resources."""
        resources: List[types.Resource] = []

        apps = await self._get_available_apps()
        for app in apps:
            resources.append(
                self.create_resource(
                    uri=f"ui://dwsim/{app.name}",
                    name=app.title,
                    description=app.description,
                    mime_type=self.MIME_TYPE,
                )
            )

        return resources

    async def read_resource(self, uri: str) -> types.ReadResourceResult:
        """Read UI app content by URI."""
        scheme, authority, segments = self._parse_ui_uri(uri)

        if scheme != self.SCHEME or authority != self.AUTHORITY:
            raise ResourceNotFoundError(
                f"Invalid UI resource URI '{uri}'",
                suggestions=["ui://dwsim/simulation-results"],
            )

        if not segments:
            raise ResourceNotFoundError(
                "UI resource URI must include an app name",
                suggestions=["ui://dwsim/simulation-results"],
            )

        app_name = segments[0]
        app_config = await self._get_app_config(app_name)
        if app_config is None:
            available = await self._get_available_apps()
            suggestions = [f"ui://dwsim/{app.name}" for app in available][:5]
            raise ResourceNotFoundError(
                f"UI app '{app_name}' not found",
                suggestions=suggestions,
            )

        html = await self._get_app_html(app_config)
        meta = self._build_csp_metadata(app_config)

        return types.ReadResourceResult(
            contents=[
                types.TextResourceContents(
                    uri=uri,
                    text=html,
                    mimeType=self.MIME_TYPE,
                    _meta=meta,
                )
            ]
        )

    async def _get_available_apps(self) -> List[AppConfig]:
        """Get list of available apps from the apps directory."""
        if self._cache_enabled and self._apps_index is not None:
            return self._apps_index

        if not self._apps_path.exists():
            self._logger.warning("apps_path_missing", path=str(self._apps_path))
            return []

        loop = asyncio.get_event_loop()
        app_dirs = await loop.run_in_executor(
            None, lambda: [p for p in self._apps_path.iterdir() if p.is_dir()]
        )

        apps: List[AppConfig] = []
        for app_dir in app_dirs:
            app_config = await self._load_app_config(app_dir)
            if app_config is not None:
                apps.append(app_config)

        apps.sort(key=lambda app: app.name)
        if self._cache_enabled:
            self._apps_index = apps

        self._logger.info("ui_apps_loaded", count=len(apps))
        return apps

    async def _get_app_config(self, app_name: str) -> Optional[AppConfig]:
        if self._cache_enabled and app_name in self._app_cache:
            return self._app_cache[app_name]

        app_dir = self._apps_path / app_name
        if not app_dir.exists():
            return None

        app_config = await self._load_app_config(app_dir)
        if app_config is None:
            return None

        if self._cache_enabled:
            self._app_cache[app_name] = app_config
        return app_config

    async def _load_app_config(self, app_dir: Path) -> Optional[AppConfig]:
        app_name = app_dir.name
        config_path = app_dir / "app.json"

        if config_path.exists():
            try:
                async with aiofiles.open(config_path, "r", encoding="utf-8") as f:
                    raw = await f.read()
                data = json.loads(raw)
                config = AppConfig.model_validate(data)
                return config
            except (OSError, json.JSONDecodeError, ValidationError) as exc:
                self._logger.warning(
                    "ui_app_config_invalid",
                    app=app_name,
                    error=str(exc),
                )

        entry_point = app_dir / "index.html"
        if not entry_point.exists():
            return None

        return AppConfig(
            name=app_name,
            title=app_name.replace("-", " ").title(),
            description=f"UI app for {app_name.replace('-', ' ')}",
        )

    async def _get_app_html(self, app_config: AppConfig) -> str:
        entry_point = app_config.entry_point or "index.html"
        cache_key = f"{app_config.name}:{entry_point}"

        if self._cache_enabled and cache_key in self._html_cache:
            return self._html_cache[cache_key]

        html_path = self._apps_path / app_config.name / entry_point
        if not html_path.exists():
            self._logger.error(
                "ui_app_entry_missing",
                app=app_config.name,
                entry_point=entry_point,
                path=str(html_path),
            )
            raise ResourceInvalidStateError(
                f"UI app '{app_config.name}' missing entry point '{entry_point}'"
            )

        try:
            async with aiofiles.open(html_path, "r", encoding="utf-8") as f:
                html = await f.read()
        except OSError as exc:
            self._logger.error(
                "ui_app_read_error",
                app=app_config.name,
                entry_point=entry_point,
                error=str(exc),
            )
            raise ResourceInvalidStateError(
                f"Failed to read UI app '{app_config.name}': {str(exc)}"
            ) from exc

        if self._cache_enabled:
            self._html_cache[cache_key] = html
        return html

    def _build_csp_metadata(self, app_config: AppConfig) -> Optional[dict]:
        ui_meta: Dict[str, object] = {}
        if app_config.csp is not None:
            ui_meta["csp"] = app_config.csp.model_dump(
                by_alias=True,
                exclude_none=True,
            )
        if app_config.prefers_border:
            ui_meta["prefersBorder"] = True

        if not ui_meta:
            return None
        return {"ui": ui_meta}

    @staticmethod
    def _parse_ui_uri(uri: str) -> Tuple[str, str, List[str]]:
        parsed = urlparse(uri)
        segments = [seg for seg in parsed.path.strip("/").split("/") if seg]
        return parsed.scheme, parsed.netloc, segments
