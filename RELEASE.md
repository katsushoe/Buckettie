# Release 1.3.23.0

Release date: 2026-09-05

## Highlights

- Return an explicit `error.category` and sanitized `error.details` for Git failures so AI clients can explain the cause to users.
- Redact URL-like values, email addresses, absolute paths, and credential-like assignments from Git diagnostics, normalize whitespace, and limit details to 1024 characters.
- Parse MCP Server-Sent Event responses in CLI Tool calls so `buckettie mcp version` reports the running version correctly.

## Artifacts

| File | SHA-256 |
| --- | --- |
| `Buckettie-1.3.23.0-win-x64.msi` | `3CEA539E9CC7657CA6424E6B4FE82CDDB357ACB8597F4167CD9B884EFCA0C2D4` |
| `Buckettie-1.3.23.0-win-x64.zip` | `7F44644F65189A88444BC05CB307BC3289121FD8EFD7315ED6A83724ECE4A594` |

- Runtime: Windows x64, self-contained
- Display version: `1.3.23.0`
- Windows Installer product version: `1.3.23`
- Tag: `v1.3.23.0`

## Validation

- Automated tests: passed (338 tests)
- MSI build and SHA-256 hash: passed
- ZIP release build and SHA-256 hash: passed
- Physical-machine reinstall: passed after UAC elevation (service running automatically, MCP and CLI version `1.3.23.0`, `buckettie mcp version` passed, configuration preserved)

Configuration, DPAPI tokens, audit logs, and other machine-specific data are not included in the artifacts.
