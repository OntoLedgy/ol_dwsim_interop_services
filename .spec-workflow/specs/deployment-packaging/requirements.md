# Requirements Document: Deployment Packaging and Distribution

## Introduction

This specification defines the requirements for packaging and distributing the DWSIM MCP Server for easy deployment. The goal is to enable users to install and run the server with minimal manual configuration, supporting multiple deployment scenarios from local development to production environments.

The current state includes a working Python package configuration (pyproject.toml), C# build scripts, and pre-built binaries. This spec extends the existing infrastructure to provide production-ready packaging, containerization, and comprehensive installation documentation.

## Alignment with Product Vision

This feature directly supports the product vision outlined in product.md:

- **Enable AI-Powered Chemical Engineering**: Easy deployment removes barriers to adoption, allowing more users to integrate DWSIM simulation into their AI workflows
- **Support Developer Adoption**: One-command installation and clear documentation accelerate integration into AI applications
- **Ensure Safe AI Integration**: Containerized deployment with proper resource limits provides secure, isolated execution environments
- **Maintain DWSIM Compatibility**: Proper packaging of DWSIM assemblies ensures consistent, tested configurations across deployments

## Requirements

### REQ-1: Python Package Distribution

**User Story:** As a developer, I want to install the DWSIM MCP server via pip, so that I can integrate it into my Python environment without manual file management.

#### Acceptance Criteria

1. WHEN user runs `pip install dwsim-mcp-server` THEN the system SHALL install all Python dependencies and create the `dwsim-mcp` CLI command
2. IF pythonnet is unavailable (Linux/macOS) THEN the system SHALL display a clear error message explaining Windows requirement
3. WHEN package is installed THEN the system SHALL validate Python version (>=3.11, <3.13) and fail with helpful message if incompatible
4. WHEN user runs `dwsim-mcp --version` THEN the system SHALL display the package version, Python version, and .NET Framework availability
5. WHEN user runs `dwsim-mcp --help` THEN the system SHALL display all available commands and configuration options

### REQ-2: C# Worker Distribution

**User Story:** As a user, I want pre-built C# binaries included in the distribution, so that I don't need Visual Studio or MSBuild to deploy the server.

#### Acceptance Criteria

1. WHEN Python package is installed THEN the system SHALL include pre-compiled DwsimWorker.dll and dependencies in the package
2. IF DWSIM binaries are not configured THEN the system SHALL provide `dwsim-mcp setup` command to download/configure them
3. WHEN `dwsim-mcp setup --dwsim-path <path>` is run THEN the system SHALL validate DWSIM installation and copy required assemblies
4. WHEN `dwsim-mcp setup --download` is run THEN the system SHALL download compatible DWSIM binaries from GitHub releases
5. WHEN setup completes successfully THEN the system SHALL create `dwsim.config.json` with validated paths

### REQ-3: Configuration Management

**User Story:** As an operator, I want to configure the server via environment variables or config files, so that I can customize behavior without modifying code.

#### Acceptance Criteria

1. WHEN `.env` file exists in working directory THEN the system SHALL load configuration from it
2. WHEN environment variables are set THEN the system SHALL use them, overriding `.env` file values
3. WHEN `dwsim-mcp init` is run THEN the system SHALL create `.env.example` template with all configuration options documented
4. IF required configuration is missing THEN the system SHALL display specific error message indicating which settings are needed
5. WHEN configuration includes invalid values THEN the system SHALL validate and report errors at startup before accepting connections

### REQ-4: Docker Containerization

**User Story:** As a DevOps engineer, I want to deploy the DWSIM MCP server in a Docker container, so that I can ensure consistent environments and easy scaling.

#### Acceptance Criteria

1. WHEN `docker build` is run with provided Dockerfile THEN the system SHALL create a working container image with Python and .NET Framework support
2. WHEN container starts THEN the system SHALL automatically configure DWSIM paths and start the MCP server
3. WHEN `docker-compose up` is run THEN the system SHALL start the server with proper volume mounts for simulation files
4. IF container health check fails THEN Docker SHALL restart the container according to restart policy
5. WHEN container logs are inspected THEN the system SHALL output structured JSON logs to stdout

### REQ-5: Installation Documentation

**User Story:** As a new user, I want clear installation instructions, so that I can successfully deploy the server on my first attempt.

#### Acceptance Criteria

1. WHEN user reads installation guide THEN they SHALL find step-by-step instructions for Windows, Docker, and development setups
2. IF installation fails THEN the documentation SHALL include troubleshooting section addressing common failure modes
3. WHEN prerequisites are listed THEN the system SHALL include version requirements and download links
4. WHEN user follows quick-start guide THEN they SHALL be able to run a test simulation within 15 minutes
5. WHEN configuration options are documented THEN each option SHALL include description, default value, and example usage

### REQ-6: Service Installation (Windows)

**User Story:** As a Windows administrator, I want to run the DWSIM MCP server as a Windows service, so that it starts automatically and runs reliably in background.

#### Acceptance Criteria

1. WHEN `dwsim-mcp service install` is run THEN the system SHALL register a Windows service named "DwsimMcpServer"
2. WHEN Windows service starts THEN the system SHALL load configuration and begin accepting MCP connections
3. WHEN `dwsim-mcp service uninstall` is run THEN the system SHALL remove the Windows service registration
4. IF service crashes THEN Windows Service Control Manager SHALL restart it according to recovery settings
5. WHEN service status is queried THEN the system SHALL report running/stopped state and last error if any

### REQ-7: Release Automation

**User Story:** As a maintainer, I want automated release builds, so that I can publish new versions consistently without manual steps.

#### Acceptance Criteria

1. WHEN git tag matching `v*.*.*` is pushed THEN CI/CD pipeline SHALL build and publish Python wheel to PyPI
2. WHEN release is created THEN CI/CD pipeline SHALL build C# binaries and attach to GitHub release
3. WHEN Docker image is built for release THEN CI/CD pipeline SHALL push to container registry with version tag
4. IF any build step fails THEN the pipeline SHALL halt and report specific error
5. WHEN release is published THEN the system SHALL update changelog with version notes

### REQ-8: Dependency Validation

**User Story:** As a user, I want the system to validate all dependencies at startup, so that I get clear feedback about missing components before attempting operations.

#### Acceptance Criteria

1. WHEN server starts THEN the system SHALL verify pythonnet can load .NET Framework 4.8
2. WHEN server starts THEN the system SHALL verify DwsimWorker.dll is accessible and loadable
3. WHEN server starts THEN the system SHALL verify DWSIM assemblies are present and correct version
4. IF any dependency is missing THEN the system SHALL display diagnostic message with remediation steps
5. WHEN `dwsim-mcp doctor` is run THEN the system SHALL perform comprehensive dependency check and report status

## Non-Functional Requirements

### Code Architecture and Modularity

- **Single Responsibility Principle**: Packaging scripts, service wrappers, and setup utilities should be separate modules
- **Modular Design**: Docker configuration, Windows service wrapper, and CLI tools should be independently maintainable
- **Dependency Management**: Pin exact versions for reproducible builds; document any platform-specific dependencies
- **Clear Interfaces**: Setup and configuration APIs should be stable across versions

### Performance

- **Startup Time**: Server SHALL be ready to accept connections within 10 seconds on standard hardware
- **Image Size**: Docker image SHALL be under 2GB (compressed), targeting <1GB for optimized builds
- **Installation Time**: `pip install` SHALL complete within 60 seconds on reasonable network connection

### Security

- **Minimal Privileges**: Windows service SHALL run under a low-privilege service account, not LocalSystem
- **File Permissions**: Configuration files containing paths SHALL not be world-readable on Linux
- **Container Security**: Docker container SHALL run as non-root user where possible
- **Dependency Scanning**: CI/CD pipeline SHALL scan dependencies for known vulnerabilities

### Reliability

- **Graceful Shutdown**: Service SHALL handle SIGTERM/service stop and close sessions cleanly
- **Crash Recovery**: Windows service and Docker container SHALL have automatic restart policies
- **Configuration Validation**: Invalid configuration SHALL be detected at startup, not at runtime
- **Rollback Support**: Package versions SHALL be pinned to allow downgrade if needed

### Usability

- **Error Messages**: All error messages SHALL include actionable remediation steps
- **Progress Feedback**: Long-running operations (setup, download) SHALL display progress indicators
- **Offline Support**: Installation SHALL work in air-gapped environments with pre-downloaded dependencies
- **Documentation Discoverability**: `--help` output SHALL reference documentation URLs

## Dependencies

### Existing Infrastructure (Leverage)

- `mcp_service/server/pyproject.toml` - Python package configuration
- `mcp_service/dwsim_worker/build.bat` - C# build scripts
- `prebuilt/setup.ps1` - Automated setup script (reference implementation)
- `mcp_service/server/dwsim_mcp_server/config/` - Pydantic settings infrastructure

### New Components Required

- Dockerfile and docker-compose.yml in `deployments/docker/`
- Windows service wrapper module
- `dwsim-mcp setup` CLI command implementation
- `dwsim-mcp doctor` diagnostic command
- `.env.example` configuration template
- GitHub Actions workflow for releases
- Installation guide in `docs/deployment/`

## Out of Scope

- Kubernetes deployment manifests (future spec)
- Linux systemd service files (Windows-only initial release)
- Helm charts for Kubernetes
- Ansible/Terraform deployment automation
- Multi-architecture Docker images (ARM64)
- macOS support (requires .NET Core port)

## References

- [Product Vision](../../steering/product.md)
- [Technology Stack](../../steering/tech.md)
- [Spec Plan - 8.2](../plan.md)
- [Python Packaging Guide](https://packaging.python.org/)
- [Docker Best Practices](https://docs.docker.com/develop/develop-images/dockerfile_best-practices/)
