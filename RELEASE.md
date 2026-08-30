# Release 1.3.14.0

Release date: 2026-08-30

## Highlights

- Added secure API Token collection to interactive repository registration.
- Kept Tokens out of MCP arguments and responses by using the ACL-restricted one-shot Named Pipe.
- Added rollback of a newly saved Token when repository persistence fails.

## Artifacts

| File | SHA-256 |
| --- | --- |
| `Buckettie-1.3.14.0-win-x64.msi` | `39B8891268ECD15315427010B3E6716C1920D77A4F786C48DD3E30D915B4B63D` |
| `Buckettie-1.3.14.0-win-x64.zip` | Not built for this local installation |

- Runtime: Windows x64, self-contained
- Display version: `1.3.14.0`
- Windows Installer product version: `1.3.14`
- Tag: not created

## Validation

- Automated tests: passed (284 tests)
- MSI build and SHA-256 hash: passed
- Physical-machine upgrade: passed after UAC elevation (service running, version `1.3.14.0`, configuration and 3 Token files preserved)

Configuration, DPAPI tokens, audit logs, and other machine-specific data are not included in the artifacts.
