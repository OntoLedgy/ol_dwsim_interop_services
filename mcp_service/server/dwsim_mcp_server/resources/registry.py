"""Resource registration for MCP server."""

from __future__ import annotations

from typing import Any, List

from mcp.server import Server
from mcp import types

from dwsim_mcp_server.observability import get_logger
from dwsim_mcp_server.resources.base import (
    ResourceError,
    ResourceNotFoundError,
    ResourceInvalidStateError,
)
from dwsim_mcp_server.resources.docs import DocsProvider
from dwsim_mcp_server.resources.samples import SamplesProvider
from dwsim_mcp_server.resources.results import ResultsProvider
from dwsim_mcp_server.resources.ui_resource_provider import UiResourceProvider


def register_resources(server: Server, dependencies: Any | None = None) -> None:
    """Register resource handlers with the MCP server."""
    if dependencies is None and hasattr(server, "resource"):
        _register_fastmcp_resources(server)
        return
    if dependencies is None:
        raise ValueError("dependencies are required for legacy Server resources.")
    _register_server_resources(server, dependencies)


def _register_server_resources(server: Server, dependencies: Any) -> None:
    logger = get_logger(__name__)
    settings = dependencies.settings

    docs_provider = DocsProvider(
        docs_path=getattr(settings, "docs_path", "./docs/resources"),
    )
    samples_provider = SamplesProvider(
        samples_path=getattr(settings, "sample_cases_path", "./cases/samples"),
        case_storage_roots=getattr(settings, "case_storage_roots", []),
    )
    results_provider = ResultsProvider(
        session_client=dependencies.session_client,
    )
    ui_provider = UiResourceProvider(
        apps_path=getattr(settings, "apps_path", "./apps/templates"),
        cache_enabled=getattr(settings, "apps_cache_enabled", True),
    )

    providers = [docs_provider, samples_provider, results_provider, ui_provider]

    @server.list_resource_templates()
    async def list_resource_templates() -> List[types.ResourceTemplate]:
        templates: List[types.ResourceTemplate] = []
        for provider in providers:
            templates.extend(provider.get_resource_templates())
        return templates

    @server.list_resources()
    async def list_resources() -> List[types.Resource]:
        resources: List[types.Resource] = []

        for provider in providers:
            try:
                provider_resources = await provider.list_resources()
                resources.extend(provider_resources)
            except Exception as e:
                logger.warning(
                    "resource_list_error",
                    provider=provider.__class__.__name__,
                    error=str(e),
                )

        logger.debug("resources_listed", count=len(resources))
        return resources

    @server.read_resource()
    async def read_resource(uri: str) -> types.ReadResourceResult:
        logger.debug("resource_read_request", uri=uri)

        from urllib.parse import urlparse
        parsed = urlparse(uri)
        scheme = parsed.netloc  # e.g., "docs", "cases", "session"
        protocol = parsed.scheme  # e.g., "resource", "ui"

        try:
            if protocol == "ui":
                return await ui_provider.read_resource(uri)
            if scheme == "docs":
                return await docs_provider.read_resource(uri)
            if scheme == "cases":
                return await samples_provider.read_resource(uri)
            if scheme == "session":
                return await results_provider.read_resource(uri)
            raise ResourceNotFoundError(
                f"Unknown resource scheme: '{scheme}'",
                suggestions=["resource://docs", "resource://cases", "resource://session"],
            )
        except ResourceNotFoundError as e:
            logger.warning("resource_not_found", uri=uri, message=e.message)
            raise
        except ResourceInvalidStateError as e:
            logger.warning("resource_invalid_state", uri=uri, message=e.message)
            raise
        except ResourceError as e:
            logger.error("resource_error", uri=uri, code=e.code, message=e.message)
            raise
        except Exception as e:
            logger.error("resource_read_failed", uri=uri, error=str(e))
            raise ResourceError(f"Failed to read resource: {str(e)}")

    logger.info(
        "resources_registered",
        provider_count=len(providers),
        templates_count=sum(len(p.get_resource_templates()) for p in providers),
    )


def _register_fastmcp_resources(server) -> None:
    # Build providers once at registration time
    # Use paths relative to the package directory, not CWD
    from pathlib import Path

    _pkg_root = Path(__file__).resolve().parent.parent
    _docs_provider = DocsProvider(docs_path=str(_pkg_root / "docs" / "resources"))
    _samples_provider = SamplesProvider(
        samples_path=str(_pkg_root / "cases" / "samples"),
        case_storage_roots=[],
    )
    _ui_provider = UiResourceProvider(apps_path=str(_pkg_root / "apps" / "templates"))

    @server.resource(
        "resource://docs",
        name="Documentation Index",
        description="List all available DWSIM documentation topics",
        mime_type="application/json",
    )
    async def docs_index() -> str:
        result = await _docs_provider.read_resource("resource://docs")
        return _extract_resource_content(result)

    @server.resource(
        "resource://docs/{topic}",
        name="Documentation Topic",
        description="Get documentation for a specific topic (e.g., unit-operations, property-packages)",
        mime_type="text/markdown",
    )
    async def docs_topic(topic: str) -> str:
        result = await _docs_provider.read_resource(f"resource://docs/{topic}")
        return _extract_resource_content(result)

    @server.resource(
        "resource://cases",
        name="Sample Cases Index",
        description="List all available DWSIM sample simulation cases",
        mime_type="application/json",
    )
    async def cases_index() -> str:
        result = await _samples_provider.read_resource("resource://cases")
        return _extract_resource_content(result)

    @server.resource(
        "resource://cases/{name}",
        name="Sample Case Metadata",
        description="Get metadata for a specific sample case (compounds, units, complexity)",
        mime_type="application/json",
    )
    async def case_metadata(name: str) -> str:
        result = await _samples_provider.read_resource(f"resource://cases/{name}")
        return _extract_resource_content(result)

    @server.resource(
        "resource://cases/{name}/flowsheet",
        name="Sample Case Flowsheet",
        description="Get flowsheet topology (streams, units, connections) for a sample case",
        mime_type="application/json",
    )
    async def case_flowsheet(name: str) -> str:
        result = await _samples_provider.read_resource(
            f"resource://cases/{name}/flowsheet"
        )
        return _extract_resource_content(result)

    @server.resource(
        "ui://dwsim/{app}",
        name="UI App",
        description="Load a DWSIM UI app by name (e.g., simulation-results)",
        mime_type=UiResourceProvider.MIME_TYPE,
    )
    async def ui_app(app: str) -> str:
        result = await _ui_provider.read_resource(f"ui://dwsim/{app}")
        return _extract_resource_content(result)

    @server.resource(
        "ui://dwsim/{app}/{param}",
        name="UI App (parameterized)",
        description="Load a DWSIM UI app with an additional parameter segment",
        mime_type=UiResourceProvider.MIME_TYPE,
    )
    async def ui_app_parameterized(app: str, param: str) -> str:
        result = await _ui_provider.read_resource(f"ui://dwsim/{app}/{param}")
        return _extract_resource_content(result)

    # NOTE: Session resources require session_client from lifespan context.
    # FastMCP resources are registered at startup before lifespan runs.
    # These resources are available via the legacy Server path when dependencies
    # are provided. For FastMCP, use the get_results tool instead.
    #
    # TODO: Implement session resources for FastMCP when FastMCP supports
    # accessing lifespan context from resource handlers.


def _extract_resource_content(result: types.ReadResourceResult) -> str | bytes:
    """Extract text or blob content from a ReadResourceResult."""
    if not result.contents:
        return ""
    content = result.contents[0]
    if hasattr(content, "text"):
        return content.text
    return content.blob
