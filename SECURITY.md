# Security

## Bitbucket API Token

Buckettie stores API Tokens as DPAPI LocalMachine-encrypted files named `data/secrets/<repository-id>.token`.

- Do not put tokens in `buckettie.json`, Git Remote URLs, source files, command-line arguments, or logs.
- Treat `TokenNotFound` as an unconfigured credential, not as an authentication failure.
- The `data/secrets` directory disables inherited ACLs and grants access only to LocalSystem, Administrators, and the creating operator.
- Token files are machine-bound and must not be copied to another computer as backups.

The current design is recorded in [ADR 0010](docs/adr/0010-dpapi-machine-token-store.md). ADR 0001 records the superseded Credential Manager design.

## Git execution

Buckettie exposes typed Git operations only. It does not accept shell commands, executable paths, or arbitrary Git arguments. Repository LocalRoot and HTTPS/SSH Bitbucket Remote URL are validated before every operation, and configured operands are separated from options with `--` or embedded below the fixed `refs/remotes/` namespace. Each process trusts only that validated LocalRoot through a process-local `safe.directory`; no global Git configuration is changed. Repository status uses only local remote-tracking refs and performs no implicit network access. Inherited Git override variables are removed before process start.

The command-boundary design is recorded in [ADR 0002](docs/adr/0002-fixed-git-command-gateway.md).

## Git AskPass

`Buckettie.AskPass` receives only Repository ID and the case-sensitive Bitbucket username through environment variables. It reads and decrypts the repository-scoped DPAPI Token and returns it only through the Git AskPass protocol. The Token must never be copied into an environment variable or temporary script.

## Repository registration approval

`bitbucket_repository_register` adds a new entry to the Repository Allowlist, so it requires approval that an MCP client cannot forge or skip from within the calling chat session. The server resolves the logged-on interactive user's session token (`WTSQueryUserToken`) and launches `Buckettie.ApprovalPrompt` on that user's desktop (`CreateProcessWithTokenW`) rather than relying on a chat-mediated confirmation. The service and the prompt process exchange a fixed, non-secret JSON request and response over a per-request Named Pipe whose ACL grants access only to that user's SID and LocalSystem. No interactive session, a launch failure, a denial, and a response Timeout all fail closed: none of them write to `buckettie.json` or change the in-memory allowlist. Workspace and Slug are always derived from the local repository's actual Git remote, never from caller input, and branch-policy fields are server-defaulted rather than caller-suppliable.

The trust-boundary design is recorded in [ADR 0012](docs/adr/0012-interactive-repository-registration-approval.md).

## Bitbucket REST API

Buckettie resolves workspace and repository slug only from the Repository Allowlist. The REST client uses the fixed `https://api.bitbucket.org/2.0/` base address and typed operations; it does not accept an arbitrary URL, HTTP method, or request body from an MCP client. Basic authentication is generated in memory from the configured Atlassian email and the repository-scoped DPAPI Token. Pull Request creation and merge are restricted to the configured develop-to-main route. Automatic redirects are disabled; diff redirects are followed only inside the matching Bitbucket API repository path.

The REST trust-boundary design is recorded in [ADR 0004](docs/adr/0004-fixed-bitbucket-rest-client.md).

## MCP Streamable HTTP

Buckettie listens only on IPv4 and IPv6 loopback. MCP requests with an `Origin` header are accepted only from HTTP loopback on the configured port. The server exposes exactly the typed Phase 1 tools and never accepts arbitrary shell, Git argument, REST destination, repository coordinates, or Tag target hashes.

The MCP transport boundary is recorded in [ADR 0005](docs/adr/0005-local-streamable-http-mcp-server.md).

MCP Tool failures use fixed error codes and messages. Git stderr, HTTP response bodies, exceptions, local paths, URLs, and credential values are not included. The common error contract is recorded in [ADR 0006](docs/adr/0006-common-mcp-tool-result.md).

## Audit log

Every MCP-backed gateway operation records its tool name, repository ID, applicable ref or Pull Request ID, result, duration, and fixed error code in the daily structured audit log. The standard layout writes these files below `<install-root>\logs`. Tokens, authorization headers, passwords, request/response bodies, PR descriptions, messages, diffs, local paths, URLs, and exception details are excluded by the audit event schema.

The audit boundary and data-minimization decision are recorded in [ADR 0007](docs/adr/0007-structured-audit-log.md).
