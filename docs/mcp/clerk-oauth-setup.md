# Clerk OAuth Setup for DWSIM MCP Server

This guide walks through configuring Clerk OAuth for the DWSIM MCP server,
including Clerk dashboard settings, environment variables, and mcp-remote
client configuration.

## Prerequisites

- A Clerk account with access to the Clerk dashboard
- DWSIM MCP server installed locally
- A client that can send OAuth bearer tokens (e.g., mcp-remote)

## Clerk Dashboard Configuration

1. Create or open your Clerk application.
2. In the Clerk dashboard, configure the OAuth/OpenID Connect settings:
   - **Issuer URL**: Copy the issuer URL for your tenant.
   - **Audience**: Add an audience value for the MCP server (e.g., `dwsim-mcp`).
   - **Scopes**: Ensure the required scopes are enabled (e.g., `user`).
3. Confirm your application can issue tokens for the audience and scopes
   you plan to require in the MCP server.

## Environment Variables

Set the following variables in your `.env` file (or the runtime environment):

```env
DWSIM_AUTH_ENABLED=true
CLERK_ISSUER_URL=https://<your-tenant>.clerk.accounts.dev
CLERK_JWKS_URL=
CLERK_AUDIENCE=dwsim-mcp
CLERK_REQUIRED_SCOPES=user
```

Notes:
- `CLERK_JWKS_URL` is optional. If omitted, the server derives the JWKS URL
  from the issuer URL.
- `CLERK_REQUIRED_SCOPES` is a comma-separated list. Use the same scopes you
  configure in Clerk.

## mcp-remote Client Configuration

Update your client configuration to request tokens from Clerk and attach
them to MCP requests. A typical mcp-remote setup includes:

- OAuth issuer URL
- Client credentials (if using a confidential client)
- Audience and scopes that match the server configuration
- Bearer token header (`Authorization: Bearer <token>`)

Example request header:

```http
Authorization: Bearer <access-token>
```

## Running the Server with OAuth

1. Ensure the environment variables above are set.
2. Start the server using the usual command or service entry point.
3. Verify the OAuth discovery endpoint:
   - `/.well-known/oauth-protected-resource`
4. Confirm that unauthenticated requests to `/mcp` are rejected (401) and
   authenticated requests succeed.

## Troubleshooting

- **401 Unauthorized**: Ensure the token audience and scopes match the server
  configuration, and the token is not expired.
- **JWKS fetch errors**: Verify the issuer URL or set `CLERK_JWKS_URL` explicitly.
- **Missing scopes**: Update `CLERK_REQUIRED_SCOPES` to match the token payload.
- **Clock skew issues**: Confirm system time is accurate on both client and server.
