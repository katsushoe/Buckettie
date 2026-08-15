# Security

## Bitbucket API Token

Buckettie stores API Tokens as Windows Generic Credentials named `Buckettie/Bitbucket/<repository-id>`.

- Do not put tokens in `buckettie.json`, Git Remote URLs, source files, command-line arguments, or logs.
- Run Buckettie Server and its management CLI as the same Windows user.
- Treat `TokenNotFound` as an unconfigured credential, not as an authentication failure.
- Credential Manager provider error codes may be reported, but credential content must never be reported.

The design rationale and alternatives are recorded in [ADR 0001](docs/adr/0001-windows-credential-manager.md).
