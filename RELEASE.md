# Release 1.3.8.0

Release date: 2026-08-26

## Highlights

- Added diagnostic Git failure classifications, retry guidance, and audit correlation IDs
- Added provider-neutral Pull Request mergeability states and retry contracts
- Added bounded polling for asynchronous Bitbucket merge tasks
- Preserved existing Pull Request response fields while adding `mergeability_status`

## Artifacts

| File | SHA-256 |
| --- | --- |
| `Buckettie-1.3.8.0-win-x64.msi` | `5D2F31DFEB2A8A87AE1D833B93A0A384318137F32F3BD8AA0B94511DB4FABE87` |
| `Buckettie-1.3.8.0-win-x64.zip` | `E8502DEC2AA1C7F23BD761CE995F766208D257861F813F064591A64764C01167` |

- Runtime: Windows x64, self-contained
- Display version: `1.3.8.0`
- Windows Installer product version: `1.3.8`
- Tag: `v1.3.8.0`

## Validation

- Automated tests: passed (233 tests)
- MSI build and SHA-256 hash: passed
- ZIP release build and SHA-256 hash: passed
- Physical-machine upgrade: passed (service running, version `1.3.8.0`, configuration preserved)

Configuration, DPAPI tokens, audit logs, and other machine-specific data are not included in the artifacts.
