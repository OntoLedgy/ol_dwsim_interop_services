# SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
#
# SPDX-License-Identifier: AGPL-3.0-or-later

import asyncio
from datetime import datetime, timezone
import uuid

import pytest

from dwsim_mcp_server.observability.correlation import (
    CorrelationContext,
    correlation_scope,
    generate_request_id,
    get_current_context,
    set_current_context,
)


def test_generate_request_id_returns_uuid4() -> None:
    request_id = generate_request_id()

    parsed = uuid.UUID(request_id)

    assert parsed.version == 4
    assert str(parsed) == request_id


def test_get_current_context_returns_none_when_unset() -> None:
    set_current_context(None)

    assert get_current_context() is None


def test_set_current_context_round_trip() -> None:
    context = CorrelationContext(
        request_id="request-123",
        session_id="session-456",
        tool_name="tool-name",
        start_time=datetime(2024, 1, 1, tzinfo=timezone.utc),
    )

    set_current_context(context)

    assert get_current_context() == context

    set_current_context(None)


def test_correlation_scope_sets_context_during_scope() -> None:
    with correlation_scope(request_id="request-789", session_id="session-000", tool_name="tool-x") as context:
        current = get_current_context()

        assert current == context
        assert current is not None
        assert current.request_id == "request-789"
        assert current.session_id == "session-000"
        assert current.tool_name == "tool-x"

    assert get_current_context() is None


def test_correlation_scope_cleans_up_after_scope() -> None:
    with correlation_scope():
        assert get_current_context() is not None

    assert get_current_context() is None


def test_correlation_scope_cleans_up_on_exception() -> None:
    with pytest.raises(RuntimeError):
        with correlation_scope():
            raise RuntimeError("boom")

    assert get_current_context() is None


@pytest.mark.asyncio
async def test_async_context_isolation_between_tasks() -> None:
    async def _worker(request_id: str) -> str:
        with correlation_scope(request_id=request_id):
            await asyncio.sleep(0)
            current = get_current_context()
            assert current is not None
            assert current.request_id == request_id

        assert get_current_context() is None
        return request_id

    results = await asyncio.gather(
        _worker("request-a"),
        _worker("request-b"),
    )

    assert set(results) == {"request-a", "request-b"}
    assert get_current_context() is None


def test_nested_correlation_scope_restores_previous_context() -> None:
    with correlation_scope(request_id="outer") as outer_context:
        assert get_current_context() == outer_context

        with correlation_scope(request_id="inner") as inner_context:
            assert get_current_context() == inner_context
            assert inner_context.request_id == "inner"

        assert get_current_context() == outer_context
        assert outer_context.request_id == "outer"

    assert get_current_context() is None
