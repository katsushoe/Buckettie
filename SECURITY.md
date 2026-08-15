# Security

## Bitbucket API Token

Buckettie stores API Tokens as Windows Generic Credentials named `Buckettie/Bitbucket/<repository-id>`.

- Do not put tokens in `buckettie.json`, Git Remote URLs, source files, command-line arguments, or logs.
- Run Buckettie Server and its management CLI as the same Windows user.
- Treat `TokenNotFound` as an unconfigured credential, not as an authentication failure.
- Credential Manager provider error codes may be reported, but credential content must never be reported.

The design rationale and alternatives are recorded in [ADR 0001](docs/adr/0001-windows-credential-manager.md).

## Git execution

Buckettie exposes typed Git operations only. It does not accept shell commands, executable paths, or arbitrary Git arguments. Repository LocalRoot and HTTPS Bitbucket Remote URL are validated before every operation, and configured operands are separated from options with `--`. Inherited Git override variables are removed before process start.

The command-boundary design is recorded in [ADR 0002](docs/adr/0002-fixed-git-command-gateway.md).

## Git AskPass

`Buckettie.AskPass` receives only Repository ID and the case-sensitive Bitbucket username through environment variables. It reads the API Token directly from Windows Credential Manager and returns it only through the Git AskPass protocol. The Token must never be copied into an environment variable or temporary script.

## Bitbucket REST API

Buckettie resolves workspace and repository slug only from the Repository Allowlist. The REST client uses the fixed `https://api.bitbucket.org/2.0/` base address and typed operations; it does not accept an arbitrary URL, HTTP method, or request body from an MCP client. Basic authentication is generated in memory from the configured Atlassian email and the repository-scoped Credential Manager Token.

The REST trust-boundary design is recorded in [ADR 0004](docs/adr/0004-fixed-bitbucket-rest-client.md).
