# Requirements Document: FastMCP OAuth Migration

## Introduction

This specification covers the migration from the current low-level MCP Python SDK (`Server` + `StreamableHTTPSessionManager`) architecture to FastMCP with Clerk OAuth integration. The migration enables secure remote access to the DWSIM MCP server via authenticated HTTP transport, allowing integration with ChatGPT, OpenAI Codex, and other MCP clients that require OAuth.

**Current State:**
- Using `mcp>=1.0.0` standard Python SDK
- Streamable HTTP transport with no authentication (fully exposed)
- 33 tools across 8 modules using verbose `@server.call_tool()` pattern
- Low-level `Server` + `StreamableHTTPSessionManager` architecture

**Target State:**
- FastMCP with decorator-based tools (`@mcp.tool()`)
- Clerk OAuth 2.1 integration for secure access
- Support for `mcp-remote` client authentication flow
- Compatible with ChatGPT Developer Mode and other OAuth-requiring clients

## Alignment with Product Vision

This feature directly supports several goals from product.md:

1. **Enable AI-Powered Chemical Engineering**: By adding OAuth, the server becomes compatible with ChatGPT and other commercial AI platforms that require authentication.

2. **Ensure Safe AI Integration**: OAuth prevents unauthorized access to the simulation server, protecting against resource abuse and unauthorized operations.

3. **Support Developer Adoption**: FastMCP's decorator-based tools provide a cleaner API that is easier to maintain and extend.

4. **Enterprise Features** (from Future Vision): OAuth authentication is a prerequisite for multi-tenancy, audit logging, and rate limiting.

## Requirements

### REQ-1: FastMCP Migration

**User Story:** As a developer maintaining the DWSIM MCP server, I want to use FastMCP's decorator-based tool definitions, so that the codebase is cleaner and easier to maintain.

#### Acceptance Criteria

1. WHEN the server starts THEN the system SHALL load all 33 tools via FastMCP `@mcp.tool()` decorators
2. WHEN a tool is invoked THEN the system SHALL access dependencies via FastMCP's typed `Context` object
3. IF a tool exists in the current implementation THEN the system SHALL provide identical functionality after migration
4. WHEN the server shuts down THEN the system SHALL properly dispose all resources via the lifespan context manager

### REQ-2: Clerk OAuth Token Verification

**User Story:** As a server operator, I want the server to verify Clerk-issued JWT tokens, so that only authenticated users can access the MCP tools.

#### Acceptance Criteria

1. WHEN a request arrives with a valid Clerk JWT token THEN the system SHALL allow access to MCP tools
2. WHEN a request arrives with an expired token THEN the system SHALL return HTTP 401 Unauthorized
3. WHEN a request arrives with an invalid token signature THEN the system SHALL return HTTP 401 Unauthorized
4. WHEN a request arrives without a token AND authentication is enabled THEN the system SHALL return HTTP 401 Unauthorized
5. IF the token lacks required scopes THEN the system SHALL return HTTP 401 Unauthorized

### REQ-3: OAuth Discovery Endpoints

**User Story:** As an MCP client using `mcp-remote`, I want to discover the OAuth configuration automatically, so that I can authenticate with the correct identity provider.

#### Acceptance Criteria

1. WHEN a client requests `/.well-known/oauth-protected-resource` THEN the system SHALL return valid OAuth protected resource metadata
2. WHEN a client requests the metadata THEN the response SHALL include the Clerk authorization server URL
3. WHEN a client requests the metadata THEN the response SHALL include the resource server URL matching the server's HTTP endpoint

### REQ-4: Backward Compatibility

**User Story:** As a developer using stdio transport locally, I want to continue using the server without OAuth, so that my local development workflow is not disrupted.

#### Acceptance Criteria

1. WHEN `DWSIM_AUTH_ENABLED=false` THEN the system SHALL accept unauthenticated requests
2. WHEN `DWSIM_TRANSPORT_MODE=stdio` THEN the system SHALL operate without OAuth regardless of auth settings
3. IF existing tool calls are made with the same parameters THEN the system SHALL return equivalent results

### REQ-5: Configuration Management

**User Story:** As a server operator, I want to configure OAuth settings via environment variables, so that I can easily deploy to different environments.

#### Acceptance Criteria

1. WHEN `DWSIM_AUTH_ENABLED=true` THEN the system SHALL require Clerk OAuth configuration
2. WHEN `CLERK_ISSUER_URL` is set THEN the system SHALL use it for JWKS discovery
3. WHEN `CLERK_REQUIRED_SCOPES` is set THEN the system SHALL validate tokens against those scopes
4. IF `CLERK_AUDIENCE` is set THEN the system SHALL validate the JWT audience claim

## Non-Functional Requirements

### Code Architecture and Modularity
- **Single Responsibility Principle**: The auth module SHALL be separate from tool implementations
- **Modular Design**: OAuth components SHALL be isolated and reusable
- **Dependency Management**: FastMCP SHALL be the only new framework dependency (pyjwt already exists)
- **Clear Interfaces**: Token verification SHALL follow MCP SDK's `TokenVerifier` abstract base class

### Performance
- JWKS keys SHALL be cached for at least 1 hour to minimize external requests
- Token verification SHALL complete in under 10ms for cached keys
- First request latency SHALL not exceed 500ms for JWKS fetch

### Security
- Tokens SHALL be verified using RS256 algorithm only
- JWKS client SHALL validate TLS certificates
- Token expiration SHALL be enforced
- Sensitive configuration (keys, tokens) SHALL NOT be logged

### Reliability
- JWKS fetch failures SHALL be logged and reported
- Temporary JWKS unavailability SHALL use cached keys if available
- Invalid tokens SHALL be rejected with appropriate error messages

### Usability
- OAuth configuration SHALL be clearly documented in `.env.example`
- Error messages for auth failures SHALL be actionable
- Migration SHALL preserve all existing tool names and parameters
