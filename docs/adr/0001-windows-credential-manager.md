# ADR 0001: Windows Credential Manager for API tokens

- Status: Accepted

## Context

Buckettie needs a Bitbucket API Token for Git HTTPS and REST calls. The token must not be stored in `buckettie.json`, a Git Remote URL, command-line arguments, or logs. Phase 1 targets Windows.

## Decision

Store one Generic Credential per Repository ID in Windows Credential Manager under `Buckettie/Bitbucket/<repository-id>`. Persist it for the current Windows user on the local machine. Application code accesses it only through `IApiTokenStore`.

## Alternatives

- DPAPI-encrypted file: not selected because it also needs a configured storage path, ACL policy, atomic updates, and concurrent access handling.
- Environment variable: not selected as the primary store because inheritance and process inspection increase accidental exposure risk.
- Plain JSON: rejected because it stores the token as plaintext.

## Impact

The default implementation is Windows-only. Other platforms require another `IApiTokenStore` implementation. Repository IDs are limited to ASCII letters, numbers, `.`, `_`, and `-` so Credential targets remain unambiguous.

## Security conditions

- Never log the token, credential blob, Authorization header, or secret-bearing command.
- Never put the token in `buckettie.json`, Remote URLs, or command-line arguments.
- Clear temporary managed and unmanaged byte buffers after use.
- Return provider error codes without embedding secret material.

## Operational conditions

Credential creation and removal will be exposed by the management CLI. Deleting a missing credential is idempotent. Service execution must use the same Windows account that owns the credential.

## Implementation, tests, and documentation

Infrastructure wraps the native Credential Manager API. Unit tests use a fake native boundary and never alter the developer's Credential Manager. `SECURITY.md` documents the operator-facing rules; CLI help will reference it when the CLI is implemented.
