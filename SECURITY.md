# Security

## Bitbucket API Token

Buckettie stores API Tokens as Windows Generic Credentials named `Buckettie/Bitbucket/<repository-id>`.

- Do not put tokens in `buckettie.json`, Git Remote URLs, source files, command-line arguments, or logs.
- Run Buckettie Server and its management CLI as the same Windows user.
- Treat `TokenNotFound` as an unconfigured credential, not as an authentication failure.
- Credential Manager provider error codes may be reported, but credential content must never be reported.

The design rationale and alternatives are recorded in [ADR 0001](docs/adr/0001-windows-credential-manager.md).

## Git execution

Buckettie exposes typed Git operations only. It does not accept shell commands, executable paths, or arbitrary Git arguments. Repository LocalRoot and HTTPS Bitbucket Remote URL are validated before every operation, and configured operands are separated from options with `--` or embedded below the fixed `refs/remotes/` namespace. Repository status uses only local remote-tracking refs and performs no implicit network access. Inherited Git override variables are removed before process start.

The command-boundary design is recorded in [ADR 0002](docs/adr/0002-fixed-git-command-gateway.md).

## Git AskPass

`Buckettie.AskPass` receives only Repository ID and the case-sensitive Bitbucket username through environment variables. It reads the API Token directly from Windows Credential Manager and returns it only through the Git AskPass protocol. The Token must never be copied into an environment variable or temporary script.

## Bitbucket REST API

Buckettie resolves workspace and repository slug only from the Repository Allowlist. The REST client uses the fixed `https://api.bitbucket.org/2.0/` base address and typed operations; it does not accept an arbitrary URL, HTTP method, or request body from an MCP client. Basic authentication is generated in memory from the configured Atlassian email and the repository-scoped Credential Manager Token. Pull Request creation and merge are restricted to the configured develop-to-main route. Automatic redirects are disabled; diff redirects are followed only inside the matching Bitbucket API repository path.

The REST trust-boundary design is recorded in [ADR 0004](docs/adr/0004-fixed-bitbucket-rest-client.md).

## MCP Streamable HTTP

Buckettie listens only on IPv4 and IPv6 loopback. MCP requests with an `Origin` header are accepted only from HTTP loopback on the configured port. The server exposes exactly the typed Phase 1 tools and never accepts arbitrary shell, Git argument, REST destination, repository coordinates, or Tag target hashes.

The MCP transport boundary is recorded in [ADR 0005](docs/adr/0005-local-streamable-http-mcp-server.md).

MCP Tool failures use fixed error codes and messages. Git stderr, HTTP response bodies, exceptions, local paths, URLs, and credential values are not included. The common error contract is recorded in [ADR 0006](docs/adr/0006-common-mcp-tool-result.md).

## Audit log

Every MCP-backed gateway operation records its tool name, repository ID, applicable ref or Pull Request ID, result, duration, and fixed error code in the daily structured audit log. The release layout writes these files under `F:\Buckettie\logs`. Tokens, authorization headers, passwords, request/response bodies, PR descriptions, messages, diffs, local paths, URLs, and exception details are excluded by the audit event schema.

The audit boundary and data-minimization decision are recorded in [ADR 0007](docs/adr/0007-structured-audit-log.md).
