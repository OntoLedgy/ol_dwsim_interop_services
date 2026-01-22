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


def register_resources(server: Server, dependencies: Any) -> None:
    """Register resource handlers with the MCP server.

    Args:
        server: MCP Server instance
        dependencies: ServerDependencies containing session_client and settings
    """
    logger = get_logger(__name__)
    settings = dependencies.settings

    # Initialize providers
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
        """Return all available resource templates."""
        templates: List[types.ResourceTemplate] = []
        for provider in providers:
            templates.extend(provider.get_resource_templates())
        return templates

    @server.list_resources()
    async def list_resources() -> List[types.Resource]:
        """Return all available resources across all providers."""
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
        """Read resource content by URI, routing to appropriate provider."""
        logger.debug("resource_read_request", uri=uri)

        # Parse URI to determine provider
        from urllib.parse import urlparse
        parsed = urlparse(uri)
        scheme = parsed.netloc  # e.g., "docs", "cases", "session"

        try:
            if scheme == "docs":
                return await docs_provider.read_resource(uri)
            elif scheme == "cases":
                return await samples_provider.read_resource(uri)
            elif scheme == "session":
                return await results_provider.read_resource(uri)
            else:
                raise ResourceNotFoundError(
                    f"Unknown resource scheme: '{scheme}'",
                    suggestions=["resource://docs", "resource://cases", "resource://session"],
                )

        except ResourceNotFoundError as e:
            logger.warning("resource_not_found", uri=uri, message=e.message)
            # Re-raise with suggestions for MCP error handling
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
