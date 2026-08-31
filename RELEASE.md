# Release 1.3.17.0

Release date: 2026-08-31

## Highlights

- Added the read-only MCP `list_projects` tool for selecting registered repository IDs before push operations.
- Added registered-project candidates to unregistered repository push errors and MCP guidance to call `list_projects` before every push.
- Added the source project name and target repository URL to the Token registration dialog.

## Artifacts

| File | SHA-256 |
| --- | --- |
| `Buckettie-1.3.17.0-win-x64.msi` | `0E5D3FA7F6239760A7807B8BEB865935FFC15F6F14154EC8A01E4BBFE68964EE` |
| `Buckettie-1.3.17.0-win-x64.zip` | `0AF7874C5FE75C67CF8C3F5FE81734D5397F73C0E744ECE283B96AF3340F5EDC` |

- Runtime: Windows x64, self-contained
- Display version: `1.3.17.0`
- Windows Installer product version: `1.3.17`
- Tag: `v1.3.17.0`

## Validation

- Automated tests: passed (284 tests)
- MSI build and SHA-256 hash: passed
- ZIP release build and SHA-256 hash: passed
- Physical-machine upgrade: passed after UAC elevation (service running, version `1.3.17.0`, configuration and 3 Token files preserved)

Configuration, DPAPI tokens, audit logs, and other machine-specific data are not included in the artifacts.
