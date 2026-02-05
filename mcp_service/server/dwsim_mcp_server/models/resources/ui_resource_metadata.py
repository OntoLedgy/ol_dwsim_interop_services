"""Pydantic models for UI resource metadata and app configuration."""

from __future__ import annotations

from typing import List, Literal, Optional
import ipaddress
import re
from urllib.parse import urlparse

from pydantic import BaseModel, ConfigDict, Field, field_validator

_RESOURCE_URI_PATTERN = re.compile(r"^ui://dwsim/[a-z0-9]+(?:-[a-z0-9]+)*(?:/[a-z0-9{}_-]+)*$")
_HOSTNAME_LABEL_PATTERN = re.compile(r"^[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?$")


def _is_valid_hostname(value: str) -> bool:
    if len(value) > 253:
        return False
    labels = value.split(".")
    return all(_HOSTNAME_LABEL_PATTERN.match(label) for label in labels)


def _is_valid_ip(value: str) -> bool:
    try:
        ipaddress.ip_address(value)
    except ValueError:
        return False
    return True


def _validate_csp_domain(value: str) -> str:
    candidate = value.strip()
    if not candidate:
        raise ValueError("CSP domain entries cannot be empty")
    if candidate in ("*", "'self'", "'none'"):
        return candidate
    if candidate.startswith(("http://", "https://")):
        parsed = urlparse(candidate)
        if parsed.scheme not in ("http", "https") or not parsed.hostname:
            raise ValueError(f"Invalid CSP domain origin: {candidate}")
        hostname = parsed.hostname
        if hostname != "localhost" and not (_is_valid_hostname(hostname) or _is_valid_ip(hostname)):
            raise ValueError(f"Invalid CSP domain origin: {candidate}")
        return candidate
    if candidate.startswith("*."):
        candidate = candidate[2:]
    host = candidate
    port = None
    if ":" in candidate:
        host, port = candidate.rsplit(":", 1)
        if not port.isdigit():
            raise ValueError(f"Invalid CSP domain port: {candidate}")
        port_value = int(port)
        if port_value < 1 or port_value > 65535:
            raise ValueError(f"Invalid CSP domain port: {candidate}")
    if host == "localhost":
        return value.strip()
    if not (_is_valid_hostname(host) or _is_valid_ip(host)):
        raise ValueError(f"Invalid CSP domain entry: {value}")
    return value.strip()


class CspConfig(BaseModel):
    """Content Security Policy configuration for an app."""

    connect_domains: List[str] = Field(
        default_factory=list,
        alias="connectDomains",
        description="Allowed connect-src domains for the app.",
    )
    resource_domains: List[str] = Field(
        default_factory=list,
        alias="resourceDomains",
        description="Allowed script/style/image domains for the app.",
    )
    frame_domains: List[str] = Field(
        default_factory=list,
        alias="frameDomains",
        description="Allowed frame-src domains for the app.",
    )

    @field_validator("connect_domains", "resource_domains", "frame_domains", mode="before")
    @classmethod
    def _normalize_domains(cls, value: Optional[List[str]]) -> List[str]:
        if value is None:
            return []
        return list(value)

    @field_validator("connect_domains", "resource_domains", "frame_domains")
    @classmethod
    def _validate_domains(cls, value: List[str]) -> List[str]:
        return [_validate_csp_domain(entry) for entry in value]

    model_config = ConfigDict(
        populate_by_name=True,
        json_schema_extra={
            "example": {
                "connectDomains": ["https://api.example.com"],
                "resourceDomains": ["https://cdn.jsdelivr.net"],
                "frameDomains": ["https://app.example.com"],
            }
        },
    )


class UiResourceMetadata(BaseModel):
    """Metadata for UI resources returned with tool results."""

    resource_uri: str = Field(
        ...,
        alias="resourceUri",
        description="URI of the UI resource (ui://dwsim/...)",
    )
    visibility: List[Literal["model", "app"]] = Field(
        default_factory=lambda: ["model", "app"],
        description="Who can see/call the associated tool.",
    )
    csp: Optional[CspConfig] = Field(
        default=None,
        description="CSP configuration for the app.",
    )
    permissions: List[str] = Field(
        default_factory=list,
        description="Required browser permissions.",
    )
    prefers_border: bool = Field(
        default=False,
        alias="prefersBorder",
        description="Whether app prefers visual border.",
    )

    @field_validator("resource_uri")
    @classmethod
    def _validate_resource_uri(cls, value: str) -> str:
        candidate = value.strip()
        if not _RESOURCE_URI_PATTERN.match(candidate):
            raise ValueError("resource_uri must match ui://dwsim/{app-name} or parameterized URI pattern")
        return candidate

    model_config = ConfigDict(
        populate_by_name=True,
        json_schema_extra={
            "example": {
                "resourceUri": "ui://dwsim/simulation-results",
                "visibility": ["model", "app"],
                "csp": {"connectDomains": []},
                "permissions": [],
                "prefersBorder": False,
            }
        },
    )


class AppConfig(BaseModel):
    """Configuration for an app read from app.json."""

    name: str = Field(
        ...,
        description="App identifier (kebab-case, used in ui://dwsim/{name}).",
    )
    title: str = Field(
        ...,
        description="Human-readable app title.",
    )
    description: str = Field(
        ...,
        description="Brief description of the app.",
    )
    version: str = Field(
        default="1.0.0",
        description="Semantic version for the app configuration.",
    )
    csp: Optional[CspConfig] = Field(
        default=None,
        description="Optional CSP configuration for the app.",
    )
    prefers_border: bool = Field(
        default=False,
        alias="prefersBorder",
        description="Whether app prefers visual border.",
    )
    entry_point: str = Field(
        default="index.html",
        alias="entryPoint",
        description="App entry point HTML file.",
    )

    model_config = ConfigDict(
        populate_by_name=True,
        json_schema_extra={
            "example": {
                "name": "simulation-results",
                "title": "Simulation Results",
                "description": "Visualizes DWSIM simulation output.",
                "version": "1.0.0",
                "entryPoint": "index.html",
            }
        },
    )
