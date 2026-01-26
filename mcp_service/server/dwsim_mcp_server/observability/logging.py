"""Structured logging configuration for the MCP server."""

from __future__ import annotations

import logging
from typing import Optional

import structlog

from .correlation import get_current_context


def _add_correlation_context(logger, method_name, event_dict):
    context = get_current_context()
    if context is None:
        return event_dict

    event_dict["requestId"] = context.request_id
    if context.session_id:
        event_dict["sessionId"] = context.session_id
    if context.tool_name:
        event_dict["toolName"] = context.tool_name
    return event_dict


def configure_logging(log_level: str, *, json_format: bool = True) -> None:
    """Configure stdlib logging and structlog."""
    level = getattr(logging, log_level.upper(), logging.INFO)
    logging.basicConfig(level=level, format="%(message)s")

    processors = [
        structlog.processors.TimeStamper(fmt="iso"),
        structlog.processors.add_log_level,
        _add_correlation_context,
        structlog.processors.StackInfoRenderer(),
        structlog.processors.format_exc_info,
    ]

    renderer = structlog.processors.JSONRenderer() if json_format else structlog.dev.ConsoleRenderer()

    structlog.configure(
        processors=processors + [renderer],
        wrapper_class=structlog.make_filtering_bound_logger(level),
        cache_logger_on_first_use=True,
    )


def get_logger(name: Optional[str] = None) -> structlog.BoundLogger:
    """Return a structlog logger with optional name binding."""
    logger = structlog.get_logger()
    if name:
        return logger.bind(logger=name)
    return logger
