# ADR 0008: Management CLI and Doctor

## Context

Operators need repeatable diagnostics for configuration, credentials, repositories, Bitbucket, and MCP without inspecting secret-bearing stores or internal exceptions.

## Decision

Provide `buckettie.exe` as a separate management process. Commands reuse the production composition root and typed gateways. `doctor` reports one `[OK]` or `[NG]` line per bounded check and returns a machine-usable exit code.

## Alternatives

Embedding commands in the server executable was rejected because management invocations must not accidentally start the host. Shell scripts were rejected because they would duplicate validation and credential rules.

## Impact

The CLI and server share configuration, policies, and adapters. Service lifecycle commands remain isolated until Windows Service support is implemented.

## Security

Diagnostics print fixed error enums and operational identifiers only. Token values, provider data, Git stderr, HTTP bodies, URLs, local paths, and exceptions are not emitted. `config show` is safe because the configuration contract prohibits secrets.

## Operational considerations

Run the CLI as the same Windows user as the server so it reads the same Credential Manager entries. Bitbucket and MCP checks require network or local server availability and return exit code `1` when unavailable.

## Implementation

`CliApplication` parses a fixed command grammar and builds services through `BuckettieCompositionRoot`. Unit tests cover help, valid configuration, and secret-safe invalid JSON reporting.
