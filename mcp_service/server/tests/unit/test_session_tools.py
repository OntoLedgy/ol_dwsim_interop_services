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

import asyncio
from types import SimpleNamespace

import pytest
from mcp import types

from dwsim_mcp_server.ipc.exceptions import SessionError
from dwsim_mcp_server.limits.resource_limit_guard import ResourceLimitViolation
from dwsim_mcp_server.tools.session import handle_session_tool
from dwsim_mcp_server.models.errors.resource_limit_error import ResourceLimitError


class FakeSessionClient:
    def __init__(self):
        self.created = []
        self.closed = []
        self.saved = []
        self.loaded = []

    async def create_session(self, *, flowsheet_name=None, timeout_seconds=None):
        self.created.append((flowsheet_name, timeout_seconds))
        return "session-1234"

    async def close_session(self, session_id: str):
        self.closed.append(session_id)
        return True

    async def save_case(self, session_id: str, file_path: str):
        self.saved.append((session_id, file_path))
        return True

    async def load_case(self, session_id: str, file_path: str):
        self.loaded.append((session_id, file_path))
        return True


def _dependencies(case_root: str, session_client=None):
    return SimpleNamespace(
        session_client=session_client or FakeSessionClient(),
        settings=SimpleNamespace(case_storage_roots=[case_root]),
    )


def test_create_session_tool_happy_path(tmp_path):
    deps = _dependencies(str(tmp_path))
    result = asyncio.run(
        handle_session_tool("create_session", {"name": "Test Case", "timeout": 120}, deps)
    )
    assert result["session_id"] == "session-1234"
    assert deps.session_client.created == [("Test Case", 120)]


def test_close_session_tool_happy_path(tmp_path):
    deps = _dependencies(str(tmp_path))
    result = asyncio.run(handle_session_tool("close_session", {"session_id": "session-1234"}, deps))
    assert result["success"] is True
    assert deps.session_client.closed == ["session-1234"]


def test_save_case_tool_valid_path(tmp_path):
    deps = _dependencies(str(tmp_path))
    file_path = tmp_path / "cases" / "case.dwxml"
    result = asyncio.run(
        handle_session_tool(
            "save_case",
            {"session_id": "session-1234", "file_path": str(file_path)},
            deps,
        )
    )
    assert result["success"] is True
    assert deps.session_client.saved[0][0] == "session-1234"


def test_load_case_tool_invalid_path(tmp_path):
    deps = _dependencies(str(tmp_path / "allowed"))
    file_path = tmp_path / "other" / "case.dwxml"
    result = asyncio.run(
        handle_session_tool(
            "load_case",
            {"session_id": "session-1234", "file_path": str(file_path)},
            deps,
        )
    )
    assert isinstance(result, types.CallToolResult)
    assert result.isError is True
    assert result.structuredContent["code"] == "INVALID_PATH"


def test_close_session_validation_error(tmp_path):
    deps = _dependencies(str(tmp_path))
    result = asyncio.run(handle_session_tool("close_session", {}, deps))
    assert isinstance(result, types.CallToolResult)
    assert result.isError is True
    assert result.structuredContent["code"] == "VALIDATION_ERROR"


def test_session_error_mapping(tmp_path):
    class ErrorSessionClient(FakeSessionClient):
        async def close_session(self, session_id: str):
            raise SessionError("Session not found")

    deps = _dependencies(str(tmp_path), session_client=ErrorSessionClient())
    result = asyncio.run(handle_session_tool("close_session", {"session_id": "missing"}, deps))
    assert isinstance(result, types.CallToolResult)
    assert result.isError is True
    assert result.structuredContent["code"] == "SESSION_ERROR"


def test_resource_limit_violation_mapping(tmp_path):
    class LimitSessionClient(FakeSessionClient):
        async def close_session(self, session_id: str):
            error = ResourceLimitError(
                code="TIMEOUT",
                message="Operation timed out.",
                session_id=session_id,
                details={"timeout_seconds": 1},
            )
            raise ResourceLimitViolation(error)

    deps = _dependencies(str(tmp_path), session_client=LimitSessionClient())
    result = asyncio.run(handle_session_tool("close_session", {"session_id": "timeout"}, deps))
    assert isinstance(result, types.CallToolResult)
    assert result.isError is True
    assert result.structuredContent["code"] == "TIMEOUT"
