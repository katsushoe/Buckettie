# ADR 0005: Local Streamable HTTP MCP server

- Status: Accepted

## Context

Buckettie must expose the fixed Phase 1 repository operations to local AI clients without granting arbitrary Git, shell, REST, repository-coordinate, or commit-target input. Streamable HTTP servers also require protection against unintended network exposure and browser-origin attacks.

## Decision

Use the official `ModelContextProtocol.AspNetCore` 2.2.0 SDK and expose exactly 14 typed `bitbucket_` tools over stateless Streamable HTTP. Kestrel listens only on localhost. The port and endpoint path come from validated Buckettie configuration, with defaults of `45450` and `/mcp`. Requests with an `Origin` header are accepted only when the origin is HTTP loopback on the configured port. Tool methods delegate exclusively to the existing Git and Bitbucket Gateways and return structured content.

## Alternatives

- stdio transport: rejected because the Version 1 specification requires an independently running Streamable HTTP server for multiple clients.
- Generic MCP Git or REST tools: rejected because caller-controlled commands and destinations violate the repository trust boundary.
- Non-loopback listening: rejected because remote Buckettie Server operation is a future phase and requires separate authentication and transport security decisions.
- Stateful HTTP sessions: rejected because the Phase 1 tools do not need server-side conversational state.

## Impact

The server process remains running after configuration validation and owns a localhost HTTP endpoint. MCP clients discover fixed JSON input and output schemas generated from typed methods. Read-only operations are annotated as such; push, Pull Request create/merge, and Tag create are annotated as destructive so clients can apply approval policy.

## Security conditions

- Bind both IPv4 and IPv6 loopback through Kestrel `ListenLocalhost`; never bind wildcard or configured external addresses.
- Reject non-loopback browser origins and origins using a different port or scheme.
- Resolve Repository coordinates, branches, PR routes, Tag targets, Git arguments, and REST paths below the MCP boundary.
- Do not expose arbitrary shell, Git argument, HTTP method, URL, header, request body, or commit hash inputs.
- Keep API Tokens and Authorization headers outside MCP input, output, schemas, and logs.

## Operational conditions

Only one process may listen on the configured port. Configuration changes require server restart. Stateless transport does not expose session-resumption features. Approval remains a client responsibility, while Buckettie policy enforcement is unconditional.

## Implementation, tests, and documentation

The executable host configures Kestrel, Origin validation, the MCP transport, serializer, and fixed Tool set. Reflection tests verify the exact 15 tool names (the 14 `bitbucket_` tools plus the standard `get_version` tool), structured output, and destructive annotations. Configuration and Origin tests cover the localhost boundary. `CONFIG.md`, the example configuration, and `SECURITY.md` describe the externally relevant settings and restrictions.
