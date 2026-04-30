# SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
#
# SPDX-License-Identifier: AGPL-3.0-or-later

"""Utilities for adding UI metadata to MCP tools and results."""

from __future__ import annotations

from typing import List, Optional

from mcp import types
from pydantic import ValidationError

from dwsim_mcp_server.models.resources.ui_resource_metadata import (
    CspConfig,
    UiResourceMetadata,
)


def add_ui_metadata(
    tool: types.Tool,
    resource_uri: str,
    *,
    visibility: Optional[List[str]] = None,
    csp: Optional[CspConfig | dict] = None,
    prefers_border: bool = False,
    permissions: Optional[List[str]] = None,
) -> types.Tool:
    """Return a copy of the tool with _meta.ui metadata attached."""
    meta = _build_ui_meta(
        resource_uri,
        visibility=visibility,
        csp=csp,
        prefers_border=prefers_border,
        permissions=permissions,
    )
    current_meta = dict(tool.meta or {})
    current_meta["ui"] = meta["ui"]
    return tool.model_copy(update={"meta": current_meta})


def get_ui_result_annotation(
    resource_uri: str,
    *,
    visibility: Optional[List[str]] = None,
    csp: Optional[CspConfig | dict] = None,
    prefers_border: bool = False,
    permissions: Optional[List[str]] = None,
) -> dict:
    """Build _meta annotation for tool results."""
    return _build_ui_meta(
        resource_uri,
        visibility=visibility,
        csp=csp,
        prefers_border=prefers_border,
        permissions=permissions,
    )


def _build_ui_meta(
    resource_uri: str,
    *,
    visibility: Optional[List[str]],
    csp: Optional[CspConfig | dict],
    prefers_border: bool,
    permissions: Optional[List[str]],
) -> dict:
    ui = UiResourceMetadata(
        resource_uri=resource_uri,
        visibility=visibility or ["model", "app"],
        csp=_coerce_csp(csp),
        prefers_border=prefers_border,
        permissions=permissions or [],
    )
    return {"ui": ui.model_dump(by_alias=True, exclude_none=True)}


def _coerce_csp(value: Optional[CspConfig | dict]) -> Optional[CspConfig]:
    if value is None or isinstance(value, CspConfig):
        return value
    try:
        return CspConfig.model_validate(value)
    except ValidationError as exc:
        raise ValueError(f"Invalid CSP configuration: {exc}") from exc
