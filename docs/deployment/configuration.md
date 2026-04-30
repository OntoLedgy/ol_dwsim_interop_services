<!--
SPDX-FileCopyrightText: 2018-2026 OntoLedgy Ltd.

SPDX-License-Identifier: AGPL-3.0-or-later
-->

# Configuration Reference

This document enumerates all configuration settings used by the DWSIM MCP server, including environment variables and JSON configuration files. Values are read at startup and are **case-insensitive** for environment variables.

## Environment Variables Overview

- **Source of truth:** `ServerSettings`, `ResourceLimitSettings`, and `ObservabilitySettings` classes in `mcp_service/server/dwsim_mcp_server`.
- **Case-insensitive:** Environment variable names are matched without case sensitivity.
- **Shared variables:** `DWSIM_LOG_LEVEL` is used by both server bootstrap and observability logging.

## Server Settings

| Name | Type | Default | Description |
| --- | --- | --- | --- |
| `DWSIM_LOG_LEVEL` | string | `INFO` | Logging level for the MCP server (e.g., `DEBUG`, `INFO`, `WARNING`). **Validation:** no explicit validation in `ServerSettings`. |
| `DWSIM_ENABLE_PYTHONNET` | bool | `true` | Enable pythonnet bridge for in-process worker calls. **Validation:** parsed as boolean by Pydantic. |
| `DWSIM_WORKER_ASSEMBLY_PATH` | string (optional) | (none) | Optional path to `DwsimWorker.dll` if not using default discovery. **Validation:** none; used as a filesystem path. |
| `DWSIM_CASE_STORAGE_ROOTS` | list of strings | `./cases` | Allowed base directories for case save/load operations. **Validation:** none; template uses a comma-separated list for env values. |
| `DWSIM_DOCS_PATH` | string | `./docs/resources` | Path to documentation resources directory. **Validation:** none; used as a filesystem path. |
| `DWSIM_SAMPLE_CASES_PATH` | string | `./cases/samples` | Path to sample cases metadata directory. **Validation:** none; used as a filesystem path. |
| `DWSIM_MAX_RESOURCE_SIZE_KB` | int | `1024` | Maximum size in KB for resource content responses. **Validation:** none. |

## Resource Limits

| Name | Type | Default | Validation | Description |
| --- | --- | --- | --- | --- |
| `DWSIM_MAX_SESSIONS` | int | `10` | `>= 1` | Maximum number of concurrent sessions. |
| `DWSIM_SESSION_TIMEOUT` | int (seconds) | `3600` | `60 <= value <= 86400` | Default session lifetime in seconds. |
| `DWSIM_OPERATION_TIMEOUT` | int (seconds) | `300` | `>= 1` | Default per-operation timeout in seconds. |
| `DWSIM_MEMORY_LIMIT_MB` | int (MB) | `2048` | `>= 1` | Process memory limit in MB before rejecting new operations. |
| `DWSIM_MEMORY_POLL_INTERVAL_SECONDS` | float (seconds) | `2.0` | `>= 0.1` | Interval in seconds for polling process memory usage. |
| `DWSIM_MEMORY_RECOVERY_RATIO` | float | `0.9` | `0.5 <= value <= 1.0` | Ratio of memory limit below which breach state clears. |

## Observability Settings

### Logging

| Name | Type | Default | Validation | Description |
| --- | --- | --- | --- | --- |
| `DWSIM_LOG_LEVEL` | string | `INFO` | Must be a valid Python logging level name (e.g., `DEBUG`, `INFO`, `WARNING`, `ERROR`, `CRITICAL`). | Logging level for the MCP server. |
| `DWSIM_LOG_FILE` | string (optional) | (none) | None | Optional log file path for rotating file logging. |
| `DWSIM_LOG_FILE_MAX_BYTES` | int | `10485760` | None | Maximum log file size in bytes before rotation. |
| `DWSIM_LOG_FILE_BACKUP_COUNT` | int | `5` | None | Number of rotated log files to keep. |

### Seq

| Name | Type | Default | Validation | Description |
| --- | --- | --- | --- | --- |
| `DWSIM_SEQ_URL` | string (optional) | (none) | None | Seq server URL for structured log shipping. |
| `DWSIM_SEQ_API_KEY` | string (optional) | (none) | None | Seq API key for authenticated log shipping. |

### Tracing

| Name | Type | Default | Validation | Description |
| --- | --- | --- | --- | --- |
| `DWSIM_TRACING_ENABLED` | bool | `false` | Parsed as boolean by Pydantic. | Enable OpenTelemetry tracing. |
| `DWSIM_TRACING_EXPORTER` | string | `none` | One of: `jaeger`, `zipkin`, `otlp`, `console`, `none`. | Tracing exporter type. |
| `DWSIM_TRACING_ENDPOINT` | string (optional) | (none) | None | Tracing exporter endpoint URL. |
| `DWSIM_TRACING_SAMPLE_RATE` | float | `1.0` | `0.0 <= value <= 1.0` | Tracing sampling rate. |

### Metrics

| Name | Type | Default | Validation | Description |
| --- | --- | --- | --- | --- |
| `DWSIM_METRICS_ENABLED` | bool | `true` | Parsed as boolean by Pydantic. | Enable Prometheus metrics collection. |
| `DWSIM_METRICS_PORT` | int | `9090` | `1 <= value <= 65535` | Metrics HTTP server port. |

## Configuration Files

### `mcp_service/dwsim_worker/dwsim.config.json`

Machine-specific configuration for DWSIM worker binaries. The `dwsim_path` key is required.

| Key | Type | Required | Validation | Description |
| --- | --- | --- | --- | --- |
| `dwsim_path` | string | yes | Must exist on disk; can be a directory or `DWSIM.exe` path. | Path to DWSIM build or installation directory. |
| `msbuild_path` | string | no | If provided, must exist on disk. | Optional path to `MSBuild.exe` for worker builds. |

Example:

```json
{
  "dwsim_path": "D:/path/to/DWSIM/bin/x64/Debug",
  "msbuild_path": "D:/Apps/Microsoft Visual Studio/2022/Professional/MSBuild/Current/Bin/MSBuild.exe"
}
```

### `.env`

Use a `.env` file to provide environment variables for the server process. You can generate a template with `dwsim-mcp init` (produces `.env.example`) and copy it to `.env`.

## Common Configurations

### Development

```env
# Server
DWSIM_LOG_LEVEL=DEBUG
DWSIM_ENABLE_PYTHONNET=true
DWSIM_WORKER_ASSEMBLY_PATH=

# Resources
DWSIM_CASE_STORAGE_ROOTS=./cases
DWSIM_DOCS_PATH=./docs/resources
DWSIM_SAMPLE_CASES_PATH=./cases/samples
DWSIM_MAX_RESOURCE_SIZE_KB=1024

# Limits
DWSIM_MAX_SESSIONS=5
DWSIM_SESSION_TIMEOUT=3600
DWSIM_OPERATION_TIMEOUT=300
DWSIM_MEMORY_LIMIT_MB=2048
DWSIM_MEMORY_POLL_INTERVAL_SECONDS=2.0
DWSIM_MEMORY_RECOVERY_RATIO=0.9

# Observability
DWSIM_LOG_FILE=./logs/dwsim-mcp.log
DWSIM_LOG_FILE_MAX_BYTES=10485760
DWSIM_LOG_FILE_BACKUP_COUNT=5
DWSIM_TRACING_ENABLED=false
DWSIM_TRACING_EXPORTER=none
DWSIM_TRACING_SAMPLE_RATE=1.0
DWSIM_METRICS_ENABLED=true
DWSIM_METRICS_PORT=9090
```

### Production (Windows Server)

```env
# Server
DWSIM_LOG_LEVEL=INFO
DWSIM_ENABLE_PYTHONNET=true
DWSIM_WORKER_ASSEMBLY_PATH=

# Resources
DWSIM_CASE_STORAGE_ROOTS=D:\dwsim\cases
DWSIM_DOCS_PATH=D:\dwsim\docs\resources
DWSIM_SAMPLE_CASES_PATH=D:\dwsim\cases\samples
DWSIM_MAX_RESOURCE_SIZE_KB=2048

# Limits
DWSIM_MAX_SESSIONS=20
DWSIM_SESSION_TIMEOUT=7200
DWSIM_OPERATION_TIMEOUT=600
DWSIM_MEMORY_LIMIT_MB=4096
DWSIM_MEMORY_POLL_INTERVAL_SECONDS=2.0
DWSIM_MEMORY_RECOVERY_RATIO=0.9

# Observability
DWSIM_LOG_FILE=D:\dwsim\logs\dwsim-mcp.log
DWSIM_LOG_FILE_MAX_BYTES=52428800
DWSIM_LOG_FILE_BACKUP_COUNT=10
DWSIM_SEQ_URL=http://seq:5341
DWSIM_SEQ_API_KEY=your-seq-api-key
DWSIM_TRACING_ENABLED=true
DWSIM_TRACING_EXPORTER=otlp
DWSIM_TRACING_ENDPOINT=http://otel-collector:4318/v1/traces
DWSIM_TRACING_SAMPLE_RATE=0.2
DWSIM_METRICS_ENABLED=true
DWSIM_METRICS_PORT=9090
```

