"""Resource registration for MCP server."""

from __future__ import annotations

from typing import Any, List

from mcp.server import Server
from mcp.server.fastmcp.server import Context
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

    providers = [docs_provider, samples_provider, results_provider]

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

        try:
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
    @server.resource(
        "resource://docs",
        name="Documentation Index",
        description="List all available DWSIM documentation topics",
        mime_type="application/json",
    )
    async def docs_index(ctx: Context | None = None):
        return await _read_provider_resource(ctx, "docs", "resource://docs")

    @server.resource(
        "resource://docs/{topic}",
        name="Documentation Topic",
        description="Get documentation for a specific topic (e.g., unit-operations, property-packages)",
        mime_type="text/markdown",
    )
    async def docs_topic(topic: str, ctx: Context | None = None):
        return await _read_provider_resource(ctx, "docs", f"resource://docs/{topic}")

    @server.resource(
        "resource://cases",
        name="Sample Cases Index",
        description="List all available DWSIM sample simulation cases",
        mime_type="application/json",
    )
    async def cases_index(ctx: Context | None = None):
        return await _read_provider_resource(ctx, "cases", "resource://cases")

    @server.resource(
        "resource://cases/{name}",
        name="Sample Case Metadata",
        description="Get metadata for a specific sample case (compounds, units, complexity)",
        mime_type="application/json",
    )
    async def case_metadata(name: str, ctx: Context | None = None):
        return await _read_provider_resource(ctx, "cases", f"resource://cases/{name}")

    @server.resource(
        "resource://cases/{name}/flowsheet",
        name="Sample Case Flowsheet",
        description="Get flowsheet topology (streams, units, connections) for a sample case",
        mime_type="application/json",
    )
    async def case_flowsheet(name: str, ctx: Context | None = None):
        return await _read_provider_resource(
            ctx, "cases", f"resource://cases/{name}/flowsheet"
        )

    @server.resource(
        "resource://session/{sessionId}/results",
        name="Session Results",
        description="Get all simulation results for a session (streams, units, overall status)",
        mime_type="application/json",
    )
    async def session_results(sessionId: str, ctx: Context | None = None):
        return await _read_provider_resource(
            ctx, "session", f"resource://session/{sessionId}/results"
        )

    @server.resource(
        "resource://session/{sessionId}/results/{objectId}",
        name="Object Results",
        description="Get detailed results for a specific stream or unit operation",
        mime_type="application/json",
    )
    async def object_results(sessionId: str, objectId: str, ctx: Context | None = None):
        uri = f"resource://session/{sessionId}/results/{objectId}"
        return await _read_provider_resource(ctx, "session", uri)


async def _read_provider_resource(ctx: Context | None, key: str, uri: str) -> str | bytes:
    providers = _get_providers(ctx)
    result = await providers[key].read_resource(uri)
    return _extract_resource_content(result)


def _extract_resource_content(result: types.ReadResourceResult) -> str | bytes:
    if not result.contents:
        return ""
    content = result.contents[0]
    if hasattr(content, "text"):
        return content.text
    return content.blob


def _get_providers(ctx: Context | None) -> Dict[str, Any]:
    app_context = ctx.request_context.lifespan_context
    providers = getattr(app_context, "_resource_providers", None)
    if providers is None:
        providers = _build_providers(app_context)
        setattr(app_context, "_resource_providers", providers)
    return providers


def _build_providers(app_context: Any) -> Dict[str, Any]:
    settings = app_context.settings
    return {
        "docs": DocsProvider(docs_path=getattr(settings, "docs_path", "./docs/resources")),
        "cases": SamplesProvider(
            samples_path=getattr(settings, "sample_cases_path", "./cases/samples"),
            case_storage_roots=getattr(settings, "case_storage_roots", []),
        ),
        "session": ResultsProvider(session_client=app_context.session_client),
    }
