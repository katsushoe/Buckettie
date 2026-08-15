# Security

## Bitbucket API Token

Buckettie stores API Tokens as Windows Generic Credentials named `Buckettie/Bitbucket/<repository-id>`.

- Do not put tokens in `buckettie.json`, Git Remote URLs, source files, command-line arguments, or logs.
- Run Buckettie Server and its management CLI as the same Windows user.
- Treat `TokenNotFound` as an unconfigured credential, not as an authentication failure.
- Credential Manager provider error codes may be reported, but credential content must never be reported.

The design rationale and alternatives are recorded in [ADR 0001](docs/adr/0001-windows-credential-manager.md).

## Git execution

Buckettie exposes typed Git operations only. It does not accept shell commands, executable paths, or arbitrary Git arguments. Repository LocalRoot and Bitbucket Remote URL are validated before every operation, and configured operands are separated from options with `--`.

The command-boundary design is recorded in [ADR 0002](docs/adr/0002-fixed-git-command-gateway.md).

## Git AskPass

`Buckettie.AskPass` receives only Repository ID and Atlassian email through environment variables. It reads the API Token directly from Windows Credential Manager and returns it only through the Git AskPass protocol. The Token must never be copied into an environment variable or temporary script.
