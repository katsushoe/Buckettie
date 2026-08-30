# Release 1.3.13.0

Release date: 2026-08-30

## Highlights

- Added the read-only MCP `list_projects` tool for discovering registered project IDs.
- Added registered project candidates to unregistered-project push errors.
- Instructed MCP clients to resolve the project ID with `list_projects` before every push.

## Artifacts

| File | SHA-256 |
| --- | --- |
| `Buckettie-1.3.13.0-win-x64.msi` | `CDAD122671EA816B1D384B34A443D3301BA4FBB89A3AE635F7122DE070431D67` |
| `Buckettie-1.3.13.0-win-x64.zip` | `E7CDA9E527E3BFE45F20E388042531E0334E9AAC7D54773EC9C740A260A9FD52` |

- Runtime: Windows x64, self-contained
- Display version: `1.3.13.0`
- Windows Installer product version: `1.3.13`
- Tag: `v1.3.13.0`

## Validation

- Automated tests: passed (283 tests)
- MSI build and SHA-256 hash: passed
- ZIP release build and SHA-256 hash: passed

Configuration, DPAPI tokens, audit logs, and other machine-specific data are not included in the artifacts.
