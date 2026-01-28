"""Typer CLI entry point for dwsim-mcp-server."""

from __future__ import annotations

import difflib
import platform
import shutil
from pathlib import Path
from importlib import metadata

import typer
from rich.console import Console
from rich.syntax import Syntax
from rich.table import Table

from dwsim_mcp_server import server
from dwsim_mcp_server.cli.doctor import DoctorRunner
from dwsim_mcp_server.cli.setup import SetupManager
from dwsim_mcp_server.cli.service import WindowsServiceManager

app = typer.Typer(add_completion=False, no_args_is_help=False)
service_app = typer.Typer(help="Manage the dwsim-mcp Windows service.")

app.add_typer(service_app, name="service")


@app.callback(invoke_without_command=True)
def main(ctx: typer.Context) -> None:
    """Run the server when no subcommand is provided."""
    if ctx.invoked_subcommand is None and not ctx.resilient_parsing:
        server.run()


@app.command()
def run() -> None:
    """Run the MCP server."""
    server.run()


def _get_template_environment(template_dir: Path, console: Console):
    try:
        from jinja2 import Environment, FileSystemLoader  # type: ignore
    except Exception as exc:
        console.print("[red]Jinja2 is not available. Install 'jinja2' to render templates.[/red]")
        console.print(f"[red]{exc}[/red]")
        return None

    if not template_dir.exists():
        console.print(f"[red]Template directory not found: {template_dir}[/red]")
        return None

    return Environment(
        loader=FileSystemLoader(str(template_dir)),
        autoescape=False,
        keep_trailing_newline=True,
    )


def _render_template(console: Console, env, template_name: str, context: dict) -> str | None:
    try:
        template = env.get_template(template_name)
    except Exception as exc:
        console.print(f"[red]Template not found: {template_name}[/red]")
        console.print(f"[red]{exc}[/red]")
        return None

    try:
        return template.render(**context)
    except Exception as exc:
        console.print(f"[red]Failed to render template {template_name}.[/red]")
        console.print(f"[red]{exc}[/red]")
        return None


def _print_diff(console: Console, path: Path, new_content: str) -> None:
    try:
        existing = path.read_text(encoding="utf-8")
    except Exception as exc:
        console.print(f"[red]Unable to read existing file for diff: {path}[/red]")
        console.print(f"[red]{exc}[/red]")
        return

    diff = difflib.unified_diff(
        existing.splitlines(keepends=True),
        new_content.splitlines(keepends=True),
        fromfile=str(path),
        tofile=f"{path} (generated)",
    )
    diff_text = "".join(diff)
    if diff_text:
        console.print(Syntax(diff_text, "diff", word_wrap=True))
    else:
        console.print("[yellow]No differences detected.[/yellow]")


def _get_dotnet_status() -> str:
    try:
        import pythonnet  # type: ignore

        version = getattr(pythonnet, "__version__", None)
        try:
            import clr  # type: ignore  # noqa: F401

            if version:
                return f"Available (pythonnet {version})"
            return "Available (pythonnet version unknown)"
        except Exception:
            return "Not Available"
    except Exception:
        return "Not Available"


@app.command()
def version() -> None:
    """Show version information."""
    console = Console()
    try:
        package_version = metadata.version("dwsim-mcp-server")
    except metadata.PackageNotFoundError:
        package_version = "Unknown"

    table = Table(show_header=False, pad_edge=False, box=None)
    table.add_column("Label", style="bold cyan")
    table.add_column("Value")
    table.add_row("Package", package_version)
    table.add_row("Python", platform.python_version())
    table.add_row("Platform", platform.platform())
    table.add_row(".NET/pythonnet", _get_dotnet_status())
    console.print(table)


@app.command()
def setup(
    dwsim_path: str | None = typer.Option(
        None,
        "--dwsim-path",
        help="Path to DWSIM binaries (folder or DWSIM.exe).",
    ),
    download: bool = typer.Option(
        False,
        "--download",
        help="Download DWSIM binaries from GitHub releases.",
    ),
) -> None:
    """Detect DWSIM, validate installation, and create the configuration file."""
    console = Console()
    if platform.system().lower() != "windows":
        console.print("[red]Setup is only supported on Windows.[/red]")
        raise typer.Exit(code=1)

    manager = SetupManager(console=console)
    detected_path = None

    if download:
        try:
            detected_path = manager.download_dwsim_binaries()
            console.print("[green]DWSIM binaries downloaded.[/green]")
        except RuntimeError as exc:
            console.print(f"[red]{exc}[/red]")
            raise typer.Exit(code=1) from exc
    elif dwsim_path:
        detected_path = Path(dwsim_path).expanduser().resolve()
    else:
        console.print("[cyan]Detecting DWSIM installation...[/cyan]")
        detected_path = manager.detect_dwsim_path()

    if detected_path is None:
        console.print("[red]DWSIM installation not found.[/red]")
        console.print("Provide --dwsim-path or run setup after installing DWSIM.")
        raise typer.Exit(code=1)

    missing = manager.get_missing_requirements(detected_path)
    if missing:
        console.print("[red]DWSIM installation is missing required files.[/red]")
        console.print(f"Path: {detected_path}")
        console.print("Missing:")
        for filename in missing:
            console.print(f" - {filename}")
        raise typer.Exit(code=1)

    if detected_path.is_file() and detected_path.name.lower() == "dwsim.exe":
        detected_path = detected_path.parent

    config_path = manager.create_config_file(detected_path)
    console.print("[green]DWSIM configuration created.[/green]")
    console.print(f"Config: {config_path}")


@app.command()
def init(
    output: Path | None = typer.Option(
        None,
        "--output",
        help="Output directory for generated configuration files.",
    ),
    mcp_config: bool = typer.Option(
        False,
        "--mcp-config",
        help="Also generate mcp.json.",
    ),
    force: bool = typer.Option(
        False,
        "--force",
        help="Overwrite existing files.",
    ),
) -> None:
    """Generate configuration files from templates."""
    console = Console()
    output_dir = (output or Path.cwd()).expanduser().resolve()

    try:
        output_dir.mkdir(parents=True, exist_ok=True)
    except Exception as exc:
        console.print(f"[red]Failed to create output directory: {output_dir}[/red]")
        console.print(f"[red]{exc}[/red]")
        raise typer.Exit(code=1) from exc

    template_dir = Path(__file__).resolve().parents[1] / "templates"
    env = _get_template_environment(template_dir, console)
    if env is None:
        raise typer.Exit(code=1)

    files_to_generate: list[tuple[str, str, dict]] = [
        ("env.example.j2", ".env.example", {}),
    ]
    if mcp_config:
        files_to_generate.append(
            ("mcp.json.j2", "mcp.json", {"cwd": str(output_dir), "env": {}}),
        )

    for template_name, filename, context in files_to_generate:
        rendered = _render_template(console, env, template_name, context)
        if rendered is None:
            console.print(f"[yellow]Skipping {filename}.[/yellow]")
            continue

        destination = output_dir / filename
        if destination.exists() and not force:
            console.print(f"[yellow]File exists; skipping: {destination}[/yellow]")
            _print_diff(console, destination, rendered)
            continue

        try:
            destination.write_text(rendered, encoding="utf-8")
        except Exception as exc:
            console.print(f"[red]Failed to write file: {destination}[/red]")
            console.print(f"[red]{exc}[/red]")
            raise typer.Exit(code=1) from exc

        console.print(f"[green]Wrote {destination}[/green]")


@app.command()
def doctor(
    verbose: bool = typer.Option(
        False,
        "--verbose",
        help="Show remediation details for all checks.",
    ),
    quiet: bool = typer.Option(
        False,
        "--quiet",
        help="Only show warnings and failures.",
    ),
) -> None:
    """Validate DWSIM MCP dependencies and configuration."""
    runner = DoctorRunner()
    exit_code = runner.run(verbose=verbose, quiet=quiet)
    if exit_code != 0:
        raise typer.Exit(code=exit_code)


def _ensure_service_environment(
    manager: WindowsServiceManager,
    console: Console,
    *,
    require_admin: bool = True,
) -> None:
    if platform.system().lower() != "windows":
        console.print("[red]Service management is only supported on Windows.[/red]")
        raise typer.Exit(code=1)

    if require_admin and not manager.check_admin():
        console.print("[red]Administrative privileges are required for service actions.[/red]")
        console.print("Re-run this command in an elevated terminal (Run as Administrator).")
        raise typer.Exit(code=1)

    if not manager.check_nssm():
        console.print("[red]NSSM was not found in PATH.[/red]")
        console.print("Install NSSM and ensure it is available on PATH.")
        raise typer.Exit(code=1)


@service_app.command("install")
def service_install(
    executable: str | None = typer.Option(
        None,
        "--executable",
        "-e",
        help="Path to the dwsim-mcp executable to run as a service.",
    ),
) -> None:
    """Install the dwsim-mcp server Windows service."""
    console = Console()
    manager = WindowsServiceManager(console=console)
    _ensure_service_environment(manager, console)

    executable_path = executable or shutil.which("dwsim-mcp")
    if not executable_path:
        console.print("[red]Executable not provided and 'dwsim-mcp' not found in PATH.[/red]")
        console.print("Provide --executable <path> to the CLI entry point.")
        raise typer.Exit(code=1)

    try:
        manager.install(executable_path)
    except RuntimeError as exc:
        console.print(f"[red]{exc}[/red]")
        raise typer.Exit(code=1) from exc

    console.print(f"[green]Service installed: {manager.SERVICE_NAME}[/green]")


@service_app.command("uninstall")
def service_uninstall() -> None:
    """Remove the dwsim-mcp server Windows service."""
    console = Console()
    manager = WindowsServiceManager(console=console)
    _ensure_service_environment(manager, console)

    try:
        manager.uninstall()
    except RuntimeError as exc:
        console.print(f"[red]{exc}[/red]")
        raise typer.Exit(code=1) from exc

    console.print(f"[green]Service removed: {manager.SERVICE_NAME}[/green]")


@service_app.command("start")
def service_start() -> None:
    """Start the dwsim-mcp server Windows service."""
    console = Console()
    manager = WindowsServiceManager(console=console)
    _ensure_service_environment(manager, console)

    try:
        manager.start()
    except RuntimeError as exc:
        console.print(f"[red]{exc}[/red]")
        raise typer.Exit(code=1) from exc

    console.print(f"[green]Service started: {manager.SERVICE_NAME}[/green]")


@service_app.command("stop")
def service_stop() -> None:
    """Stop the dwsim-mcp server Windows service."""
    console = Console()
    manager = WindowsServiceManager(console=console)
    _ensure_service_environment(manager, console)

    try:
        manager.stop()
    except RuntimeError as exc:
        console.print(f"[red]{exc}[/red]")
        raise typer.Exit(code=1) from exc

    console.print(f"[green]Service stopped: {manager.SERVICE_NAME}[/green]")


@service_app.command("status")
def service_status() -> None:
    """Show the status of the dwsim-mcp server Windows service."""
    console = Console()
    manager = WindowsServiceManager(console=console)
    _ensure_service_environment(manager, console)

    try:
        status = manager.status()
    except RuntimeError as exc:
        console.print(f"[red]{exc}[/red]")
        raise typer.Exit(code=1) from exc

    if status:
        console.print(f"[cyan]{status}[/cyan]")
    else:
        console.print("[yellow]No status output returned by NSSM.[/yellow]")
