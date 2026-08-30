# Release 1.3.15.0

Release date: 2026-08-30

## Highlights

- Integrated API Token input into the lower section of the repository registration approval dialog.
- Disabled approval until a required Token is entered.
- Removed the separate Token dialog transition from interactive registration.

## Artifacts

| File | SHA-256 |
| --- | --- |
| `Buckettie-1.3.15.0-win-x64.msi` | `54861DCCECDC7425387444946AE87658383D22E0A1647EA258F47FEE6920192E` |
| `Buckettie-1.3.15.0-win-x64.zip` | Not built for this local installation |

- Runtime: Windows x64, self-contained
- Display version: `1.3.15.0`
- Windows Installer product version: `1.3.15`
- Tag: not created

## Validation

- Automated tests: passed (284 tests)
- MSI build and SHA-256 hash: passed
- Physical-machine upgrade: passed after UAC elevation (service running, version `1.3.15.0`, configuration and 3 Token files preserved)

Configuration, DPAPI tokens, audit logs, and other machine-specific data are not included in the artifacts.
