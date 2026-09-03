# Release 1.3.20.0

Release date: 2026-09-04

## Highlights

- Require every branch creation request to provide an explicit source branch or full 40-character commit SHA.
- Support creating the initial `develop` branch from an explicitly selected source such as `main` without an implicit fallback.
- Return partial repository status when configured remote-tracking references are missing, with nullable comparison data and an explicit reason.
- Distinguish invalid and missing branch sources in MCP, CLI, and audit results.

## Artifacts

| File | SHA-256 |
| --- | --- |
| `Buckettie-1.3.20.0-win-x64.msi` | `5C18307652736F4A88D1F8B138974599F152E0B3523BE29EF583AA70B7AE8217` |
| `Buckettie-1.3.20.0-win-x64.zip` | `66795B4FD93EF038993952BF11FDC8D9FA56143113F29034EED16A37AB35834E` |

- Runtime: Windows x64, self-contained
- Display version: `1.3.20.0`
- Windows Installer product version: `1.3.20`
- Tag: `v1.3.20.0`

## Validation

- Automated tests: passed (332 tests)
- MSI build and SHA-256 hash: passed
- ZIP release build and SHA-256 hash: passed
- Physical-machine upgrade: passed after UAC elevation (service running automatically, MCP version `1.3.20.0`, configuration and six registered repositories preserved)

Configuration, DPAPI tokens, audit logs, and other machine-specific data are not included in the artifacts.
