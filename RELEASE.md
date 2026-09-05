# Release 1.3.25.0

Release date: 2026-09-05

## Highlights

- Add typed Provider error classification for deterministic Moyai conversion.
- Add `outcome`, `common_code`, `suggested_action`, and lossless Provider-specific diagnostics.
- Require status verification before retrying a push whose result is unknown.
- Preserve legacy error and repository fields for existing clients.

## Artifacts

| File | SHA-256 |
| --- | --- |
| `Buckettie-1.3.25.0-win-x64.msi` | `AB2D24C1C94D9270E18B3AD6AE901977003B0C02937D43DCB1BC7BFD0B93E57D` |

- Runtime: Windows x64, self-contained
- Display version: `1.3.25.0`
- Windows Installer product version: `1.3.25`
- Tag: `v1.3.25.0`

## Validation

- Automated tests: passed (350 tests)
- MSI build and SHA-256 hash: passed
- Physical-machine upgrade: passed after UAC elevation (service running, CLI version `1.3.25.0`, configuration preserved)

Configuration, DPAPI tokens, audit logs, and other machine-specific data are not included in the artifacts.
