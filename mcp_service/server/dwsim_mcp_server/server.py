"""MCP server bootstrap entry point."""

from __future__ import annotations

from fastmcp import FastMCP
from fastmcp.server.auth import RemoteAuthProvider
from pydantic import AnyHttpUrl

from dwsim_mcp_server.auth import AuthConfig, ClerkTokenVerifier
from dwsim_mcp_server.config import ServerSettings
from dwsim_mcp_server.config.server_settings import TransportMode
from dwsim_mcp_server.context import app_lifespan
from dwsim_mcp_server.ipc.flowsheet_client import FlowsheetClient
from dwsim_mcp_server.ipc.limited_session_client import LimitedSessionClient
from dwsim_mcp_server.observability import configure_logging, get_logger
from dwsim_mcp_server.observability.settings import ObservabilitySettings
from dwsim_mcp_server.observability.tracing import configure_tracing
from dwsim_mcp_server.resources.registry import register_resources
from dwsim_mcp_server.service import FlowsheetService
from dwsim_mcp_server.service.diagnostics_service import DiagnosticsService
from dwsim_mcp_server.services import ThermodynamicsService
from dwsim_mcp_server.services.sensitivity_service import SensitivityService
from dwsim_mcp_server.tools.registry import register_all_tools


def create_mcp_server(
    settings: ServerSettings,
    auth_config: AuthConfig,
    obs_settings: ObservabilitySettings,
) -> FastMCP:
    auth_provider = _build_auth_provider(settings, auth_config)
    server = FastMCP(
        name="dwsim-mcp-server",
        lifespan=app_lifespan,
        auth=auth_provider,
    )
    register_all_tools(server)
    register_resources(server)
    return server


def create_server(settings: ServerSettings, dependencies) -> FastMCP:
    auth_config = AuthConfig()
    obs_settings = ObservabilitySettings.from_env()
    return create_mcp_server(settings, auth_config, obs_settings)


class ServerDependencies:
    def __init__(self, settings: ServerSettings) -> None:
        self.settings = settings
        self.session_client = LimitedSessionClient(settings.resource_limits)
        self.flowsheet_client = FlowsheetClient(self.session_client)
        (
            self.flowsheet_service,
            self.thermodynamics_service,
            self.sensitivity_service,
            self.diagnostics_service,
        ) = _build_services(settings, self.session_client, self.flowsheet_client)

    async def start(self) -> None:
        self.session_client.start_monitoring()

    async def close(self) -> None:
        await self.session_client.stop_monitoring()
        self.session_client.dispose()


def _build_services(
    settings: ServerSettings,
    session_client: LimitedSessionClient,
    flowsheet_client: FlowsheetClient,
) -> tuple[FlowsheetService, ThermodynamicsService, SensitivityService, DiagnosticsService]:
    flowsheet_service = FlowsheetService(
        session_client=session_client,
        flowsheet_client=flowsheet_client,
    )
    thermodynamics_service = ThermodynamicsService(
        session_client=session_client,
    )
    sensitivity_service = SensitivityService(
        session_client=session_client,
        flowsheet_client=flowsheet_client,
        allowed_export_roots=settings.case_storage_roots
        if hasattr(settings, "case_storage_roots")
        else [],
    )
    diagnostics_service = DiagnosticsService(session_client=session_client)
    return (
        flowsheet_service,
        thermodynamics_service,
        sensitivity_service,
        diagnostics_service,
    )


def _get_base_url(settings: ServerSettings) -> str:
    """Get the base URL for OAuth discovery, using public URL if configured."""
    if settings.public_base_url:
        return settings.public_base_url.rstrip("/")
    return f"http://{settings.http_host}:{settings.http_port}"


def _build_auth_provider(
    settings: ServerSettings, auth_config: AuthConfig
) -> RemoteAuthProvider | None:
    if not auth_config.enabled:
        return None
    if auth_config.issuer_url is None:
        raise ValueError("CLERK_ISSUER_URL is required when auth is enabled.")

    token_verifier = ClerkTokenVerifier(auth_config)
    base_url = _get_base_url(settings)

    return RemoteAuthProvider(
        token_verifier=token_verifier,
        authorization_servers=[AnyHttpUrl(str(auth_config.issuer_url))],
        base_url=base_url,
    )


def _log_observability(logger, obs_settings: ObservabilitySettings) -> None:
    logger.info(
        "observability_configured",
        log_level=obs_settings.log_level,
        log_file=obs_settings.log_file,
        seq_enabled=bool(obs_settings.seq_url),
        tracing_enabled=obs_settings.tracing_enabled,
        tracing_exporter=obs_settings.tracing_exporter,
        tracing_sample_rate=obs_settings.tracing_sample_rate,
        metrics_enabled=obs_settings.metrics_enabled,
        metrics_port=obs_settings.metrics_port,
    )


def _log_server_start(logger, settings: ServerSettings, auth_enabled: bool) -> None:
    logger.info(
        "server_starting",
        log_level=settings.log_level,
        transport_mode=settings.transport_mode.value,
        auth_enabled=auth_enabled,
    )


def _transport(settings: ServerSettings) -> str:
    if settings.transport_mode == TransportMode.STREAMABLE_HTTP:
        return "streamable-http"
    return "stdio"


def main() -> None:
    settings = ServerSettings()
    obs_settings = ObservabilitySettings.from_env()
    auth_config = AuthConfig()

    configure_logging(obs_settings.log_level)
    logger = get_logger(__name__)

    configure_tracing(
        exporter=obs_settings.tracing_exporter,
        endpoint=obs_settings.tracing_endpoint,
        sample_rate=obs_settings.tracing_sample_rate,
    )

    _log_observability(logger, obs_settings)
    server = create_mcp_server(settings, auth_config, obs_settings)
    _log_server_start(logger, settings, auth_config.enabled)

    transport = _transport(settings)
    if transport == "streamable-http":
        _run_http_with_oauth(server, settings, auth_config)
    else:
        server.run(transport=transport)


def _run_http_with_oauth(
    server: FastMCP, settings: ServerSettings, auth_config: AuthConfig
) -> None:
    """Run HTTP server with OAuth discovery endpoints at root level."""
    import uvicorn
    from starlette.applications import Starlette
    from starlette.routing import Mount, Route
    from starlette.responses import JSONResponse

    mcp_path = "/mcp"
    mcp_app = server.http_app(path=mcp_path)

    routes = []

    # Add OAuth discovery routes at root if auth is enabled
    auth_provider = _build_auth_provider(settings, auth_config)
    if auth_provider is not None:
        # Manually create the protected resource endpoint
        # If public_base_url is set, use it directly (it should include /mcp)
        # Otherwise construct from host:port + mcp_path
        if settings.public_base_url:
            resource_url = settings.public_base_url.rstrip("/")
        else:
            resource_url = f"http://{settings.http_host}:{settings.http_port}{mcp_path}"

        async def oauth_protected_resource(request):
            return JSONResponse({
                "resource": resource_url,
                "authorization_servers": [str(auth_config.issuer_url)],
                "scopes_supported": auth_config.required_scopes or [],
            })

        routes.append(
            Route("/.well-known/oauth-protected-resource", oauth_protected_resource)
        )

    # Mount MCP app
    routes.append(Mount("/", app=mcp_app))

    app = Starlette(
        routes=routes,
        lifespan=mcp_app.lifespan,
    )

    uvicorn.run(
        app,
        host=settings.http_host,
        port=settings.http_port,
        log_level="info",
    )


def run() -> None:
    """Synchronous entry point for CLI usage."""
    try:
        main()
    except KeyboardInterrupt:
        pass


if __name__ == "__main__":
    run()
