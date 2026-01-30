"""Base protocol and utilities for MCP resource providers."""

from __future__ import annotations

import re
from abc import ABC, abstractmethod
from typing import Any, Dict, List, Optional, Protocol, Tuple, runtime_checkable
from urllib.parse import unquote, urlparse

from mcp import types
from pydantic import AnyUrl

from dwsim_mcp_server.observability import get_logger


@runtime_checkable
class ResourceProvider(Protocol):
    """Protocol defining the interface for MCP resource providers."""

    def get_resource_templates(self) -> List[types.ResourceTemplate]:
        """Return resource templates for URI pattern discovery.

        Returns:
            List of ResourceTemplate objects describing URI patterns this provider handles.
        """
        ...

    async def list_resources(self) -> List[types.Resource]:
        """Return all currently available resources.

        Returns:
            List of Resource objects that can be read.
        """
        ...

    async def read_resource(self, uri: str) -> types.ReadResourceResult:
        """Read resource content by URI.

        Args:
            uri: Resource URI to read (e.g., resource://docs/unit-operations)

        Returns:
            ReadResourceResult containing the resource contents.

        Raises:
            ResourceNotFoundError: If the resource does not exist.
            ResourceError: If reading fails for other reasons.
        """
        ...


class ResourceError(Exception):
    """Base exception for resource provider errors."""

    def __init__(self, message: str, code: str = "ResourceError") -> None:
        super().__init__(message)
        self.message = message
        self.code = code


class ResourceNotFoundError(ResourceError):
    """Raised when a requested resource does not exist."""

    def __init__(self, message: str, suggestions: Optional[List[str]] = None) -> None:
        super().__init__(message, code="NotFound")
        self.suggestions = suggestions or []


class ResourceInvalidStateError(ResourceError):
    """Raised when a resource cannot be accessed due to invalid state."""

    def __init__(self, message: str) -> None:
        super().__init__(message, code="InvalidState")


class BaseResourceProvider(ABC):
    """Abstract base class providing common utilities for resource providers.

    Subclasses must implement:
        - get_resource_templates()
        - list_resources()
        - read_resource()
    """

    def __init__(self) -> None:
        self._logger = get_logger(self.__class__.__name__)

    @abstractmethod
    def get_resource_templates(self) -> List[types.ResourceTemplate]:
        """Return resource templates for URI pattern discovery."""
        ...

    @abstractmethod
    async def list_resources(self) -> List[types.Resource]:
        """Return all currently available resources."""
        ...

    @abstractmethod
    async def read_resource(self, uri: str) -> types.ReadResourceResult:
        """Read resource content by URI."""
        ...

    def parse_uri(self, uri: str) -> Tuple[str, List[str]]:
        """Parse a resource URI into scheme and path segments.

        Args:
            uri: Resource URI (e.g., resource://docs/unit-operations)

        Returns:
            Tuple of (scheme, path_segments) where path_segments is a list
            of decoded path components.

        Example:
            >>> provider.parse_uri("resource://docs/unit-operations")
            ("docs", ["unit-operations"])
            >>> provider.parse_uri("resource://session/abc-123/results/stream-001")
            ("session", ["abc-123", "results", "stream-001"])
        """
        parsed = urlparse(uri)
        scheme = parsed.netloc  # e.g., "docs", "session", "cases"
        path = parsed.path.strip("/")
        segments = [unquote(seg) for seg in path.split("/") if seg]
        return scheme, segments

    def matches_uri_pattern(self, uri: str, pattern: str) -> Optional[Dict[str, str]]:
        """Check if a URI matches a pattern and extract variables.

        Pattern syntax:
            - Literal segments: "docs", "results"
            - Variable segments: "{sessionId}", "{topic}"

        Args:
            uri: Resource URI to match
            pattern: Pattern with optional {variable} placeholders

        Returns:
            Dict of extracted variables if match, None otherwise.

        Example:
            >>> provider.matches_uri_pattern(
            ...     "resource://session/abc-123/results",
            ...     "resource://session/{sessionId}/results"
            ... )
            {"sessionId": "abc-123"}
        """
        # Convert pattern to regex
        regex_pattern = pattern.replace("/", r"\/")
        variable_pattern = r"\{(\w+)\}"
        variables = re.findall(variable_pattern, regex_pattern)

        # Replace {var} with capture groups
        for var in variables:
            regex_pattern = regex_pattern.replace(f"{{{var}}}", r"([^/]+)")

        regex_pattern = f"^{regex_pattern}$"

        match = re.match(regex_pattern, uri)
        if not match:
            return None

        return dict(zip(variables, match.groups()))

    def create_resource(
        self,
        uri: str,
        name: str,
        description: str,
        mime_type: str = "application/json",
    ) -> types.Resource:
        """Create an MCP Resource object.

        Args:
            uri: Resource URI
            name: Human-readable name
            description: Brief description
            mime_type: Content MIME type

        Returns:
            MCP Resource object.
        """
        return types.Resource(
            uri=AnyUrl(uri),
            name=name,
            description=description,
            mimeType=mime_type,
        )

    def create_text_result(
        self,
        uri: str,
        text: str,
        mime_type: str = "text/plain",
    ) -> types.ReadResourceResult:
        """Create a ReadResourceResult with text content.

        Args:
            uri: Resource URI
            text: Text content
            mime_type: Content MIME type

        Returns:
            ReadResourceResult with TextResourceContents.
        """
        return types.ReadResourceResult(
            contents=[
                types.TextResourceContents(
                    uri=AnyUrl(uri),
                    text=text,
                    mimeType=mime_type,
                )
            ]
        )

    def create_json_result(
        self,
        uri: str,
        data: Any,
    ) -> types.ReadResourceResult:
        """Create a ReadResourceResult with JSON content.

        Args:
            uri: Resource URI
            data: JSON-serializable data

        Returns:
            ReadResourceResult with TextResourceContents (JSON).
        """
        import json

        text = json.dumps(data, indent=2, default=str)
        return self.create_text_result(uri, text, "application/json")
