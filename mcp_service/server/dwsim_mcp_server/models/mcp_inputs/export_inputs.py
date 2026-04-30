# SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.
#
# SPDX-License-Identifier: AGPL-3.0-or-later

"""Pydantic models for export-related MCP tool inputs and outputs."""

from typing import Any, Dict, List, Optional, Literal

from pydantic import BaseModel, Field, field_validator

CSV_EXTENSION = ".csv"
REPORT_EXTENSION = ".md"
SAVECASE_EXTENSIONS = (".dwxmz", ".dwxml")
SUPPORTED_EXPORT_JSON_FORMATS = ("summary", "full")
SUPPORTED_REPORT_TEMPLATES = ("summary", "detailed")


def _ensure_extension(value: str, expected: str) -> str:
    if not value.lower().endswith(expected):
        raise ValueError(f"file_path must end with '{expected}'")
    return value


def _ensure_extensions(value: str, expected: tuple[str, ...]) -> str:
    normalized = value.lower()
    if not any(normalized.endswith(ext) for ext in expected):
        extensions = ", ".join(expected)
        raise ValueError(f"file_path must end with one of: {extensions}")
    return value


class ExportCsvInput(BaseModel):
    """Input for export_csv MCP tool."""

    session_id: str = Field(..., description="Session identifier", min_length=1)
    file_path: str = Field(..., description="Destination CSV file path", min_length=1)
    object_ids: Optional[List[str]] = Field(
        None,
        description="Optional object identifiers to export; when omitted export all objects",
    )

    @field_validator("file_path")
    @classmethod
    def validate_file_path(cls, value: str) -> str:
        """Ensure CSV file extension."""
        return _ensure_extension(value, CSV_EXTENSION)


class ExportCsvOutput(BaseModel):
    """Output for export_csv MCP tool."""

    success: bool = Field(..., description="True when CSV export succeeded")
    file_path: str = Field(..., description="CSV file path written")
    row_count: int = Field(..., description="Number of rows written", ge=0)


class ExportJsonInput(BaseModel):
    """Input for export_json MCP tool."""

    session_id: str = Field(..., description="Session identifier", min_length=1)
    format: Literal["summary", "full"] = Field(
        ..., description="Export format: summary or full"
    )

    @field_validator("format", mode="before")
    @classmethod
    def validate_format(cls, value: str) -> str:
        """Normalize and validate format."""
        if isinstance(value, str):
            normalized = value.lower()
            if normalized in SUPPORTED_EXPORT_JSON_FORMATS:
                return normalized
        allowed = ", ".join(SUPPORTED_EXPORT_JSON_FORMATS)
        raise ValueError(f"format must be one of: {allowed}")


class ExportJsonOutput(BaseModel):
    """Output for export_json MCP tool."""

    data: Dict[str, Any] = Field(..., description="Exported JSON payload")


class GenerateReportInput(BaseModel):
    """Input for generate_report MCP tool."""

    session_id: str = Field(..., description="Session identifier", min_length=1)
    template: Literal["summary", "detailed"] = Field(
        ..., description="Report template to use"
    )
    file_path: str = Field(..., description="Destination Markdown file path", min_length=1)

    @field_validator("template", mode="before")
    @classmethod
    def validate_template(cls, value: str) -> str:
        """Normalize and validate template."""
        if isinstance(value, str):
            normalized = value.lower()
            if normalized in SUPPORTED_REPORT_TEMPLATES:
                return normalized
        allowed = ", ".join(SUPPORTED_REPORT_TEMPLATES)
        raise ValueError(f"template must be one of: {allowed}")

    @field_validator("file_path")
    @classmethod
    def validate_file_path(cls, value: str) -> str:
        """Ensure Markdown file extension."""
        return _ensure_extension(value, REPORT_EXTENSION)


class GenerateReportOutput(BaseModel):
    """Output for generate_report MCP tool."""

    success: bool = Field(..., description="True when report generation succeeded")
    file_path: str = Field(..., description="Report file path written")


class SaveCaseInput(BaseModel):
    """Input for save_case MCP tool."""

    session_id: str = Field(..., description="Session identifier", min_length=1)
    file_path: str = Field(..., description="Destination case file path", min_length=1)

    @field_validator("file_path")
    @classmethod
    def validate_file_path(cls, value: str) -> str:
        """Ensure case file extension."""
        return _ensure_extensions(value, SAVECASE_EXTENSIONS)


class SaveCaseOutput(BaseModel):
    """Output for save_case MCP tool."""

    success: bool = Field(..., description="True when the case was saved")
    file_path: str = Field(..., description="Case file path written")
