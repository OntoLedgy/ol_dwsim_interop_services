# MCP Security & Compatibility Assessment

*Assessment Date: 2026-01-31*

## 1. Current State of DWSIM MCP Server

**What we have:**
- Using `mcp>=1.0.0` (standard Python SDK)
- Streamable HTTP transport with no auth (fully exposed)
- 33 tools across 8 modules
- Low-level `Server` + `StreamableHTTPSessionManager` architecture

**Security Risk:** Running without auth means anyone with network access can create sessions, run simulations, and consume server resources.

---

## 2. FastMCP Assessment

**Is FastMCP best-in-class?** Yes, with caveats.

| Aspect | Rating | Notes |
|--------|--------|-------|
| Adoption | ⭐⭐⭐⭐⭐ | [Powers ~70% of MCP servers](https://gofastmcp.com/getting-started/welcome), 1M+ daily downloads |
| Developer Experience | ⭐⭐⭐⭐⭐ | Minimal boilerplate, decorator-based tools |
| Auth Support | ⭐⭐⭐⭐ | Built-in OAuth 2.1 with [multiple providers](https://github.com/jlowin/fastmcp) (Google, GitHub, Auth0, Azure, WorkOS) |
| Production Readiness | ⭐⭐⭐⭐ | v2.x is stable; v3.0 in beta |
| Control/Flexibility | ⭐⭐⭐ | Less visibility into internals than raw SDK |

**Recommendation:** FastMCP is the right choice for this project. The migration spec in `.spec-workflow/001-fastmcp-oauth-migration.md` is well-designed.

---

## 3. Client Compatibility Matrix

| Client | MCP Support | DWSIM Server Compatible? | Notes |
|--------|-------------|------------------------|-------|
| **Claude Desktop** | ✅ Native | ✅ Yes | Works via stdio or `mcp-remote` for HTTP |
| **Claude Code** | ✅ Native | ✅ Yes | Current setup works |
| **ChatGPT** | ✅ [Full support (Oct 2025)](https://www.infoq.com/news/2025/10/chat-gpt-mcp/) | ✅ Yes* | Requires OAuth for Developer Mode |
| **OpenAI Codex** | ✅ [Native](https://developers.openai.com/codex/mcp/) | ✅ Yes | Supports remote MCP servers |
| **Cursor** | ✅ Native | ✅ Yes | Works with streamable-http |
| **VS Code (Copilot)** | ✅ Native | ✅ Yes | Via MCP extension |
| **Gemini** | ✅ Native | ✅ Yes | First-class client support |
| **Ollama** | ⚠️ [Community bridges](https://github.com/patruff/ollama-mcp-bridge) | ⚠️ Partial | Requires `ollama-mcp-bridge` + tool-calling model |

*ChatGPT Developer Mode requires OAuth authentication - the current no-auth setup won't work.

---

## 4. Security Options

### A. Native Python SDK Security (No Middleware)

The [MCP spec (2025-11-25)](https://modelcontextprotocol.io/specification/2025-03-26/basic/authorization) includes **built-in OAuth 2.1 support**:

```python
# From mcp.server.auth
from mcp.server.auth.provider import TokenVerifier, AccessToken
from mcp.server.auth.settings import AuthSettings
```

**What you can do natively:**
- ✅ JWT token verification (via custom `TokenVerifier` implementation)
- ✅ JWKS key rotation and caching
- ✅ Scope-based access control
- ✅ Protected Resource Metadata (`/.well-known/oauth-protected-resource`)
- ✅ Dynamic Client Registration support
- ✅ PKCE enforcement (mandatory in spec)

**What the SDK provides:**
- `TokenVerifier` abstract base class - implement for any OAuth provider
- `AuthSettings` for configuration
- Automatic 401 responses for invalid/expired tokens
- Integration with `mcp-remote` for client-side OAuth flows

**The FastMCP + Clerk approach in our migration spec is the correct native approach.** No middleware needed.

### B. Security Comparison

| Approach | Pros | Cons |
|----------|------|------|
| **Native SDK OAuth** | No dependencies, spec-compliant, works with `mcp-remote` | Requires implementation work |
| **Reverse Proxy (nginx/Traefik)** | Quick to deploy, battle-tested | Adds infrastructure complexity |
| **API Gateway (Kong/Envoy)** | Enterprise features, rate limiting | Overkill for single server |
| **mTLS (mutual TLS)** | Strong auth, no tokens | Complex cert management |

**Recommendation:** Native OAuth approach with Clerk is the cleanest solution.

---

## 5. Current SDK vs FastMCP: Direct Comparison

**Key Question:** Can the current Python SDK (`mcp>=1.0.0`) do OAuth without FastMCP?

**Answer: Yes.** Both approaches use the same underlying MCP spec. The difference is implementation effort.

### Side-by-Side Comparison

| Aspect | Current SDK (Manual) | FastMCP |
|--------|---------------------|---------|
| **OAuth Support** | ✅ Yes - implement `TokenVerifier` | ✅ Yes - built-in providers |
| **Tool Definition** | `@server.call_tool()` + manual routing | `@mcp.tool()` decorator |
| **Clerk Integration** | ~100 lines (JWKS fetch, JWT verify) | ~10 lines (built-in) |
| **Code Volume** | ~500 lines for auth + 33 tool handlers | ~200 lines total |
| **Learning Curve** | Understand low-level SDK internals | Simple decorator patterns |
| **Debugging** | More visibility into request flow | Abstracts away details |
| **Community Support** | SDK docs only | 70% market share, many examples |

### Current SDK OAuth Implementation (What It Would Look Like)

```python
# You would need to implement this yourself
from mcp.server.auth.provider import TokenVerifier, AccessToken
from mcp.server.auth.settings import AuthSettings
import httpx, jwt

class ClerkTokenVerifier(TokenVerifier):
    def __init__(self, jwks_url: str):
        self.jwks_url = jwks_url
        self._jwks_cache = None

    async def verify(self, token: str) -> AccessToken | None:
        # Fetch JWKS (with caching)
        if not self._jwks_cache:
            async with httpx.AsyncClient() as client:
                resp = await client.get(self.jwks_url)
                self._jwks_cache = resp.json()

        # Decode and verify JWT
        try:
            payload = jwt.decode(token, self._jwks_cache, algorithms=["RS256"])
            return AccessToken(
                subject=payload["sub"],
                scopes=payload.get("scopes", []),
                expires_at=payload.get("exp")
            )
        except jwt.InvalidTokenError:
            return None

# Wire into existing server
auth_settings = AuthSettings(
    issuer="https://your-app.clerk.accounts.dev",
    token_verifier=ClerkTokenVerifier("https://your-app.clerk.accounts.dev/.well-known/jwks.json")
)
```

### FastMCP OAuth Implementation (From Migration Spec)

```python
from fastmcp import FastMCP
from fastmcp.server.auth import ClerkOAuthProvider

mcp = FastMCP("DWSIM", auth=ClerkOAuthProvider(
    client_id=os.getenv("CLERK_CLIENT_ID"),
    client_secret=os.getenv("CLERK_CLIENT_SECRET"),
))

@mcp.tool()
async def create_session(name: str = None) -> dict:
    # Tool implementation - no routing boilerplate
    ...
```

### Recommendation

| If You Want... | Choose |
|---------------|--------|
| Minimal change to existing code | Current SDK + manual `TokenVerifier` |
| Cleaner long-term architecture | FastMCP migration |
| Fastest time to OAuth | FastMCP (less code to write) |
| Maximum control over internals | Current SDK |

**For this project:** FastMCP migration is recommended because the tool definition cleanup alone (33 tools from verbose handlers to decorators) justifies the effort, and OAuth comes "for free."

---

## 7. Additional Considerations for Migration

The FastMCP OAuth migration spec is comprehensive, but consider adding:

### a) Rate Limiting

```python
# Add to AuthSettings or as middleware
from slowapi import Limiter
limiter = Limiter(key_func=get_remote_address)

@mcp.tool()
@limiter.limit("10/minute")  # Per-user rate limit
async def create_session(...):
```

### b) Resource Indicators (RFC 8707)

[Required by June 2025 spec update](https://auth0.com/blog/mcp-specs-update-all-about-auth/):

```python
# Clients must include resource indicator in token request
# Server should validate audience claim matches
```

### c) Audit Logging

```python
# Log all tool invocations with user identity
logger.info("tool_invoked", user=ctx.auth.subject, tool=tool_name)
```

### d) Multi-Tenant Support (if needed)

Clerk organizations can be used for team-based access control.

---

## 8. Recommended Action Plan

| Priority | Action | Effort |
|----------|--------|--------|
| **P0** | Implement FastMCP OAuth migration (see spec) | 8-10 hours |
| **P1** | Add rate limiting | 2 hours |
| **P1** | Add audit logging | 1 hour |
| **P2** | Test with ChatGPT Developer Mode | 2 hours |
| **P2** | Create Ollama bridge documentation | 1 hour |
| **P3** | Add mTLS option for enterprise deployments | 4 hours |

---

## 9. Summary

| Question | Answer |
|----------|--------|
| **Is FastMCP best-in-class?** | Yes - 70% market share, excellent DX, built-in auth |
| **Can current SDK do OAuth without FastMCP?** | Yes - implement `TokenVerifier` from `mcp.server.auth` |
| **Why choose FastMCP over current SDK?** | Less code (33 tools as decorators), built-in Clerk integration, cleaner architecture |
| **ChatGPT compatibility?** | Yes, but requires OAuth (migration enables this) |
| **Ollama compatibility?** | Partial - requires community bridges |
| **Should we proceed with the spec?** | Yes - tool cleanup alone justifies migration |

---

## References

- [MCP Authorization Spec](https://modelcontextprotocol.io/specification/2025-03-26/basic/authorization)
- [FastMCP GitHub](https://github.com/jlowin/fastmcp)
- [FastMCP Documentation](https://gofastmcp.com/getting-started/welcome)
- [OpenAI MCP Support](https://www.infoq.com/news/2025/10/chat-gpt-mcp/)
- [OpenAI Codex MCP](https://developers.openai.com/codex/mcp/)
- [MCP June 2025 Spec Updates](https://auth0.com/blog/mcp-specs-update-all-about-auth/)
- [MCP November 2025 Anniversary](http://blog.modelcontextprotocol.io/posts/2025-11-25-first-mcp-anniversary/)
- [Ollama MCP Bridge](https://github.com/patruff/ollama-mcp-bridge)
- [MCP Client for Ollama](https://github.com/jonigl/mcp-client-for-ollama)

---

## Related Documents

- Migration Spec: `.spec-workflow/001-fastmcp-oauth-migration.md`
