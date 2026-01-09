"""Structured logging configuration for the MCP server."""

from __future__ import annotations

import logging
from typing import Optional

import structlog


def configure_logging(log_level: str, *, json_format: bool = True) -> None:
    """Configure stdlib logging and structlog."""
    level = getattr(logging, log_level.upper(), logging.INFO)
    logging.basicConfig(level=level, format="%(message)s")

    processors = [
        structlog.processors.TimeStamper(fmt="iso"),
        structlog.processors.add_log_level,
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
