# Security Policy

## Reporting a Vulnerability

If you discover a security vulnerability in this project, please report it responsibly.

**Do NOT open a public GitHub issue for security vulnerabilities.**

Instead, please use one of these methods:

1. **GitHub Private Vulnerability Reporting**: Use the "Report a vulnerability" button on the [Security tab](https://github.com/OntoLedgy/ol_dwsim_interop_services/security/advisories/new) of this repository.
2. **Email**: Send details to **security@ontoledgy.io**

### What to include

- Description of the vulnerability
- Steps to reproduce
- Potential impact
- Suggested fix (if any)

### Response timeline

- **Acknowledgement**: Within 48 hours
- **Initial assessment**: Within 5 business days
- **Fix timeline**: Depends on severity; critical issues are prioritised

### Scope

This policy covers the DWSIM MCP Server and its components:
- Python MCP server (`mcp_service/server/`)
- .NET DWSIM worker (`mcp_service/dwsim_worker/`)
- Configuration and deployment scripts

### Out of scope

- Vulnerabilities in DWSIM itself (report to the [DWSIM project](https://github.com/DanWBR/dwsim))
- Vulnerabilities in third-party dependencies (report upstream, but do let us know)
