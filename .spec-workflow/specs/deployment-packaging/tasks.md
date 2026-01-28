# Tasks Document: Deployment Packaging and Distribution

## Phase 1: CLI Framework Foundation

- [x] 1. Create CLI module structure
  - Files: `dwsim_mcp_server/cli/__init__.py`, `dwsim_mcp_server/cli/main.py`
  - Create CLI module with typer app definition
  - Add `run` subcommand that calls existing `server.main()`
  - Ensure backward compatibility (default command runs server)
  - Purpose: Establish CLI framework for all subcommands
  - _Leverage: `dwsim_mcp_server/server.py`, existing typer dependency_
  - _Requirements: REQ-1_
  - _Prompt: Implement the task for spec deployment-packaging, first run spec-workflow-guide to get the workflow guide then implement the task:
    Role: Python Developer specializing in CLI tools with typer
    Task: Create CLI module structure in `dwsim_mcp_server/cli/` with typer app. Add `run` subcommand that calls existing async main from server.py. Update pyproject.toml entry point to use `dwsim_mcp_server.cli.main:app`.
    Restrictions: Do not modify server.py logic, only wrap it. Must maintain backward compatibility where `dwsim-mcp` without subcommand runs the server.
    _Leverage: `dwsim_mcp_server/server.py` for existing entry point, `pyproject.toml` for entry point configuration
    _Requirements: REQ-1 (Python Package Distribution)
    Success: `dwsim-mcp` runs server, `dwsim-mcp run` runs server, `dwsim-mcp --help` shows available commands
    Instructions: Mark task as in-progress in tasks.md before starting, use log-implementation tool after completion with artifacts, mark as complete when done._

- [x] 2. Add version command
  - File: `dwsim_mcp_server/cli/main.py`
  - Implement `version` command showing package version, Python version, .NET availability
  - Use rich for formatted console output
  - Purpose: Enable users to check installation status
  - _Leverage: `importlib.metadata`, `platform`, `rich`_
  - _Requirements: REQ-1_
  - _Prompt: Implement the task for spec deployment-packaging, first run spec-workflow-guide to get the workflow guide then implement the task:
    Role: Python Developer with CLI experience
    Task: Add `version` command to CLI that displays: package version (from importlib.metadata), Python version, platform info, pythonnet/.NET availability status.
    Restrictions: Handle pythonnet import failure gracefully, do not crash if .NET unavailable.
    _Leverage: `importlib.metadata.version()`, `platform` module, `rich.console.Console`
    _Requirements: REQ-1 (version command requirement)
    Success: `dwsim-mcp version` shows formatted version info including .NET status
    Instructions: Mark task as in-progress in tasks.md before starting, use log-implementation tool after completion with artifacts, mark as complete when done._

## Phase 2: Setup Command

- [x] 3. Create setup command module
  - File: `dwsim_mcp_server/cli/setup.py`
  - Implement SetupManager class with DWSIM detection and validation
  - Add `setup` command to CLI with `--dwsim-path` and `--download` options
  - Purpose: Enable automated DWSIM configuration
  - _Leverage: `prebuilt/setup.ps1` for logic reference_
  - _Requirements: REQ-2, REQ-3_
  - _Prompt: Implement the task for spec deployment-packaging, first run spec-workflow-guide to get the workflow guide then implement the task:
    Role: Python Developer with system integration experience
    Task: Create `dwsim_mcp_server/cli/setup.py` with SetupManager class. Implement: `detect_dwsim_path()` to check common locations, `validate_dwsim_installation(path)` to verify required assemblies exist, `create_config_file()` to generate dwsim.config.json. Add `setup` command to main.py.
    Restrictions: Must work on Windows only (document this), validate all paths before writing config.
    _Leverage: `prebuilt/setup.ps1` for validation logic, `pathlib.Path` for path handling
    _Requirements: REQ-2, REQ-3
    Success: `dwsim-mcp setup --dwsim-path C:\DWSIM\bin` creates valid config, validates assemblies
    Instructions: Mark task as in-progress in tasks.md before starting, use log-implementation tool after completion with artifacts, mark as complete when done._

- [x] 4. Add DWSIM binary download functionality
  - File: `dwsim_mcp_server/cli/setup.py`
  - Implement download_dwsim_binaries() with progress bar
  - Add SHA256 checksum verification
  - Purpose: Enable one-command setup for users without DWSIM
  - _Leverage: `httpx`, `rich.progress`, `hashlib`_
  - _Requirements: REQ-2_
  - _Prompt: Implement the task for spec deployment-packaging, first run spec-workflow-guide to get the workflow guide then implement the task:
    Role: Python Developer with networking experience
    Task: Add `download_dwsim_binaries()` method to SetupManager. Download from GitHub releases URL, show progress bar with rich, verify SHA256 checksum, extract to target directory.
    Restrictions: Handle network errors gracefully, provide clear retry instructions on failure.
    _Leverage: `httpx` for async downloads, `rich.progress.Progress`, `zipfile`
    _Requirements: REQ-2 (download option)
    Success: `dwsim-mcp setup --download` downloads ~280MB zip, extracts, validates
    Instructions: Mark task as in-progress in tasks.md before starting, use log-implementation tool after completion with artifacts, mark as complete when done._

## Phase 3: Doctor Command

- [x] 5. Create doctor command module
  - File: `dwsim_mcp_server/cli/doctor.py`
  - Implement DoctorRunner class with individual check methods
  - Add `doctor` command with `--verbose` and `--quiet` flags
  - Purpose: Comprehensive dependency validation
  - _Leverage: `dwsim_mcp_server/ipc/clr_loader.py` for assembly checks_
  - _Requirements: REQ-8_
  - _Prompt: Implement the task for spec deployment-packaging, first run spec-workflow-guide to get the workflow guide then implement the task:
    Role: Python Developer with diagnostics experience
    Task: Create `dwsim_mcp_server/cli/doctor.py` with DoctorRunner class. Implement checks: `check_python_version()`, `check_dotnet_framework()`, `check_pythonnet()`, `check_dwsim_worker_dll()`, `check_dwsim_assemblies()`, `check_config_file()`. Each returns DoctorCheck with status, message, remediation.
    Restrictions: Must not crash on any check failure, provide actionable remediation for each failure.
    _Leverage: `clr_loader.py` for assembly loading patterns, `winreg` for .NET registry check
    _Requirements: REQ-8
    Success: `dwsim-mcp doctor` runs all checks, shows pass/fail/warn with remediation hints
    Instructions: Mark task as in-progress in tasks.md before starting, use log-implementation tool after completion with artifacts, mark as complete when done._

## Phase 4: Init Command

- [x] 6. Create configuration templates
  - Files: `dwsim_mcp_server/templates/__init__.py`, `dwsim_mcp_server/templates/env.example.j2`
  - Create .env template with all ServerSettings options documented
  - Add MCP client config template (mcp.json)
  - Purpose: Provide configuration scaffolding for users
  - _Leverage: `dwsim_mcp_server/config/server_settings.py` for options_
  - _Requirements: REQ-3_
  - _Prompt: Implement the task for spec deployment-packaging, first run spec-workflow-guide to get the workflow guide then implement the task:
    Role: Python Developer with configuration management experience
    Task: Create `dwsim_mcp_server/templates/` module with Jinja2 templates. Create `env.example.j2` with all ServerSettings and ObservabilitySettings fields, including descriptions and default values as comments. Create `mcp.json.j2` for VS Code/Claude Desktop config.
    Restrictions: Document every config option, group related settings, use consistent formatting.
    _Leverage: `server_settings.py`, `observability/settings.py` for all configuration fields
    _Requirements: REQ-3
    Success: Templates include all config options with helpful descriptions
    Instructions: Mark task as in-progress in tasks.md before starting, use log-implementation tool after completion with artifacts, mark as complete when done._

- [x] 7. Add init command
  - File: `dwsim_mcp_server/cli/main.py`
  - Implement `init` command that generates config files from templates
  - Support `--output` flag for custom output path
  - Purpose: Easy configuration file generation
  - _Leverage: `dwsim_mcp_server/templates/`, `jinja2`_
  - _Requirements: REQ-3_
  - _Prompt: Implement the task for spec deployment-packaging, first run spec-workflow-guide to get the workflow guide then implement the task:
    Role: Python Developer
    Task: Add `init` command to CLI that renders templates and writes to specified output. Generate `.env.example` by default, optionally `mcp.json` with `--mcp-config` flag.
    Restrictions: Do not overwrite existing files without `--force` flag, show diff if file exists.
    _Leverage: Templates from task 6, `jinja2.Environment`
    _Requirements: REQ-3
    Success: `dwsim-mcp init` creates .env.example, `dwsim-mcp init --mcp-config` creates both
    Instructions: Mark task as in-progress in tasks.md before starting, use log-implementation tool after completion with artifacts, mark as complete when done._

## Phase 5: Windows Service Support

- [x] 8. Create Windows service manager
  - File: `dwsim_mcp_server/cli/service.py`
  - Implement WindowsServiceManager class using NSSM
  - Add `service` command group with install/uninstall/start/stop/status subcommands
  - Purpose: Enable running as Windows service
  - _Leverage: `subprocess` for NSSM calls_
  - _Requirements: REQ-6_
  - _Prompt: Implement the task for spec deployment-packaging, first run spec-workflow-guide to get the workflow guide then implement the task:
    Role: Python Developer with Windows systems experience
    Task: Create `dwsim_mcp_server/cli/service.py` with WindowsServiceManager class. Implement NSSM-based service management: `install()` registers service with nssm, `uninstall()` removes it, `start()`/`stop()` control service, `status()` queries state. Add `service` command group to CLI.
    Restrictions: Check for admin privileges, verify NSSM is available, handle errors gracefully.
    _Leverage: NSSM command-line interface, `subprocess.run()`, `ctypes` for admin check
    _Requirements: REQ-6
    Success: `dwsim-mcp service install` creates Windows service, `service status` shows state
    Instructions: Mark task as in-progress in tasks.md before starting, use log-implementation tool after completion with artifacts, mark as complete when done._

## Phase 6: Docker Configuration

- [x] 9. Create Dockerfile
  - File: `deployments/docker/Dockerfile`
  - Implement multi-stage build with Python and Wine/.NET support
  - Add health check using doctor command
  - Purpose: Enable containerized deployment
  - _Leverage: Python 3.11 base image, Wine for .NET_
  - _Requirements: REQ-4_
  - _Prompt: Implement the task for spec deployment-packaging, first run spec-workflow-guide to get the workflow guide then implement the task:
    Role: DevOps Engineer with Docker experience
    Task: Create `deployments/docker/Dockerfile` with multi-stage build. Stage 1: Copy pre-built DwsimWorker.dll. Stage 2: Python 3.11 + Wine + .NET Framework 4.8 via winetricks. Install dwsim-mcp-server package, configure health check with `dwsim-mcp doctor --quiet`.
    Restrictions: Minimize image size, run as non-root where possible, document Wine/.NET limitations.
    _Leverage: mcr.microsoft.com base images, Wine documentation
    _Requirements: REQ-4
    Success: `docker build` creates working image, container starts and passes health check
    Instructions: Mark task as in-progress in tasks.md before starting, use log-implementation tool after completion with artifacts, mark as complete when done._

- [x] 10. Create docker-compose configuration
  - Files: `deployments/docker/docker-compose.yml`, `deployments/docker/.dockerignore`, `deployments/docker/entrypoint.sh`
  - Configure volumes for cases and logs
  - Add environment variable support
  - Purpose: Simplified local development and testing
  - _Leverage: Docker Compose v3 syntax_
  - _Requirements: REQ-4_
  - _Prompt: Implement the task for spec deployment-packaging, first run spec-workflow-guide to get the workflow guide then implement the task:
    Role: DevOps Engineer
    Task: Create docker-compose.yml with service definition, volume mounts for cases/logs, environment variables. Create .dockerignore to exclude unnecessary files. Create entrypoint.sh for container startup logic.
    Restrictions: Use named volumes for persistence, document required environment variables.
    _Leverage: Docker Compose documentation, existing .env configuration
    _Requirements: REQ-4
    Success: `docker-compose up` starts server with proper volume mounts and env config
    Instructions: Mark task as in-progress in tasks.md before starting, use log-implementation tool after completion with artifacts, mark as complete when done._

## Phase 7: Package Distribution

- [x] 11. Update pyproject.toml for distribution
  - File: `mcp_service/server/pyproject.toml`
  - Add package_data for pre-built DLLs and templates
  - Update entry points for new CLI structure
  - Add MANIFEST.in for source distribution
  - Purpose: Ensure all files included in package
  - _Leverage: Python packaging documentation_
  - _Requirements: REQ-1, REQ-2_
  - _Prompt: Implement the task for spec deployment-packaging, first run spec-workflow-guide to get the workflow guide then implement the task:
    Role: Python Packaging Expert
    Task: Update pyproject.toml: change entry point to `dwsim_mcp_server.cli.main:app`, add `[tool.poetry.include]` for prebuilt/ directory, add package_data for templates. Create MANIFEST.in including prebuilt/*.dll, templates/*.j2, docs/*.md.
    Restrictions: Ensure both wheel and sdist include all necessary files, test with `pip install .`
    _Leverage: Python packaging guide, Poetry documentation
    _Requirements: REQ-1, REQ-2
    Success: `pip install .` installs package with DLLs and templates accessible
    Instructions: Mark task as in-progress in tasks.md before starting, use log-implementation tool after completion with artifacts, mark as complete when done._

- [x] 12. Create pre-built DLL packaging script
  - File: `scripts/package_prebuilt.py`
  - Copy DwsimWorker.dll and dependencies to prebuilt/ for packaging
  - Verify all required files present
  - Purpose: Automate pre-built binary preparation
  - _Leverage: `mcp_service/dwsim_worker/build.bat` output_
  - _Requirements: REQ-2_
  - _Prompt: Implement the task for spec deployment-packaging, first run spec-workflow-guide to get the workflow guide then implement the task:
    Role: Build Engineer
    Task: Create `scripts/package_prebuilt.py` that copies built DwsimWorker.dll and dependencies from `mcp_service/dwsim_worker/DwsimWorker/bin/Debug/` to `prebuilt/DwsimWorker/`. Verify all required assemblies present, generate manifest file.
    Restrictions: Exclude PDB files from package, validate assembly versions.
    _Leverage: `shutil.copytree()`, assembly manifest from build output
    _Requirements: REQ-2
    Success: Script copies all required DLLs, validates completeness
    Instructions: Mark task as in-progress in tasks.md before starting, use log-implementation tool after completion with artifacts, mark as complete when done._

## Phase 8: Release Automation

- [x] 13. Create CI workflow
  - File: `.github/workflows/ci.yml`
  - Add build, lint, and test jobs for Python and C#
  - Run on pull requests to main
  - Purpose: Automated quality checks on every PR
  - _Leverage: GitHub Actions, existing test infrastructure_
  - _Requirements: REQ-7_
  - _Prompt: Implement the task for spec deployment-packaging, first run spec-workflow-guide to get the workflow guide then implement the task:
    Role: DevOps Engineer with CI/CD experience
    Task: Create `.github/workflows/ci.yml` with jobs: `lint` (ruff, mypy), `test-python` (pytest on ubuntu), `build-csharp` (MSBuild on windows-latest), `test-integration` (full stack on windows). Use matrix for Python 3.11/3.12.
    Restrictions: Cache dependencies for speed, fail fast on lint errors, upload test results.
    _Leverage: actions/setup-python, actions/cache, pytest --junitxml
    _Requirements: REQ-7
    Success: CI runs on PR, tests both Python and C# components
    Instructions: Mark task as in-progress in tasks.md before starting, use log-implementation tool after completion with artifacts, mark as complete when done._

- [x] 14. Create release workflow
  - File: `.github/workflows/release.yml`
  - Build C# on Windows, package Python wheel, publish to PyPI
  - Create GitHub release with artifacts
  - Purpose: Automated release publishing
  - _Leverage: GitHub Actions, PyPI publish action_
  - _Requirements: REQ-7_
  - _Prompt: Implement the task for spec deployment-packaging, first run spec-workflow-guide to get the workflow guide then implement the task:
    Role: DevOps Engineer
    Task: Create `.github/workflows/release.yml` triggered on tag push (v*.*.*). Jobs: `build-csharp` (Windows, build.bat), `build-python` (needs csharp artifacts, build wheel), `publish-pypi` (pypa/gh-action-pypi-publish), `create-release` (softprops/action-gh-release with wheel and zip).
    Restrictions: Require tag to match version in pyproject.toml, sign releases if possible.
    _Leverage: pypa/gh-action-pypi-publish, softprops/action-gh-release
    _Requirements: REQ-7
    Success: Pushing v1.0.0 tag builds, publishes to PyPI, creates GitHub release
    Instructions: Mark task as in-progress in tasks.md before starting, use log-implementation tool after completion with artifacts, mark as complete when done._

## Phase 9: Documentation

- [x] 15. Create installation documentation
  - File: `docs/deployment/installation.md`
  - Write step-by-step installation guide for Windows, Docker, development
  - Include prerequisites, troubleshooting, verification steps
  - Purpose: Enable users to install successfully
  - _Leverage: `prebuilt/README.md` as starting point_
  - _Requirements: REQ-5_
  - _Prompt: Implement the task for spec deployment-packaging, first run spec-workflow-guide to get the workflow guide then implement the task:
    Role: Technical Writer
    Task: Create `docs/deployment/installation.md` with sections: Prerequisites, Quick Start (pip install), Manual Setup, Docker Setup, Development Setup, Verification (dwsim-mcp doctor), Troubleshooting. Include command examples and expected output.
    Restrictions: Test all commands before documenting, include screenshots where helpful.
    _Leverage: `prebuilt/README.md`, `docs/resources/getting-started.md`
    _Requirements: REQ-5
    Success: New user can follow guide and run first simulation within 15 minutes
    Instructions: Mark task as in-progress in tasks.md before starting, use log-implementation tool after completion with artifacts, mark as complete when done._

- [x] 16. Create configuration reference
  - File: `docs/deployment/configuration.md`
  - Document all environment variables and config file options
  - Include examples for common scenarios
  - Purpose: Complete configuration reference
  - _Leverage: `dwsim_mcp_server/config/` settings classes_
  - _Requirements: REQ-5_
  - _Prompt: Implement the task for spec deployment-packaging, first run spec-workflow-guide to get the workflow guide then implement the task:
    Role: Technical Writer
    Task: Create `docs/deployment/configuration.md` documenting all settings from ServerSettings, ResourceLimitSettings, ObservabilitySettings. For each: name, type, default, description, example. Add sections for common configurations (development, production, Docker).
    Restrictions: Keep in sync with actual settings classes, include validation rules.
    _Leverage: Pydantic settings classes, existing .env files
    _Requirements: REQ-5
    Success: All config options documented with examples and validation rules
    Instructions: Mark task as in-progress in tasks.md before starting, use log-implementation tool after completion with artifacts, mark as complete when done._

## Phase 10: Testing

- [x] 17. Add CLI unit tests
  - File: `tests/cli/test_cli.py`
  - Test all CLI commands using typer.testing.CliRunner
  - Mock external dependencies (file system, network, .NET)
  - Purpose: Ensure CLI reliability
  - _Leverage: `typer.testing`, `pytest-mock`_
  - _Requirements: All_
  - _Prompt: Implement the task for spec deployment-packaging, first run spec-workflow-guide to get the workflow guide then implement the task:
    Role: QA Engineer with Python testing experience
    Task: Create `tests/cli/` directory with tests for: `test_main.py` (run, version commands), `test_setup.py` (setup command, mocked downloads), `test_doctor.py` (all checks with various states), `test_service.py` (Windows service commands, mocked subprocess).
    Restrictions: Mock all external dependencies, test both success and failure paths, achieve >80% coverage.
    _Leverage: `typer.testing.CliRunner`, `pytest.fixture`, `unittest.mock`
    _Requirements: All REQs
    Success: All CLI commands tested, mocks prevent real system modifications
    Instructions: Mark task as in-progress in tasks.md before starting, use log-implementation tool after completion with artifacts, mark as complete when done._

- [x] 18. Add integration tests for packaging
  - File: `tests/integration/test_package.py`
  - Test package installation in clean environment
  - Verify all files included in wheel
  - Purpose: Ensure package completeness
  - _Leverage: `subprocess`, `venv`, `zipfile`_
  - _Requirements: REQ-1, REQ-2_
  - _Prompt: Implement the task for spec deployment-packaging, first run spec-workflow-guide to get the workflow guide then implement the task:
    Role: QA Engineer
    Task: Create `tests/integration/test_package.py` with tests: build wheel, inspect contents (DLLs, templates present), install in fresh venv, verify `dwsim-mcp --help` works, verify `dwsim-mcp version` shows correct version.
    Restrictions: Use temporary directories, clean up after tests, skip on non-Windows if DLL tests fail.
    _Leverage: `subprocess.run()`, `zipfile.ZipFile`, `tempfile.TemporaryDirectory`
    _Requirements: REQ-1, REQ-2
    Success: Package builds, installs, and runs in clean environment
    Instructions: Mark task as in-progress in tasks.md before starting, use log-implementation tool after completion with artifacts, mark as complete when done._
