# Release 1.3.2.0

Release date: 2026-08-21

## Highlights

- Applied the configured `ja-JP`, `en-US`, or `auto` language to CLI help, errors, and Windows service messages
- Localized MCP error messages while preserving stable machine-readable error codes
- Continued applying the installer-selected language to interactive approval dialogs
- Added bilingual MCP tool descriptions and localized remaining CLI status labels
- Starts the Windows service automatically after installation and major upgrade

## Artifacts

| File | SHA-256 |
| --- | --- |
| `Buckettie-1.3.2.0-win-x64.msi` | `5C38E0274A86F40128529B176DABCA12A751BED9AD22BECDE555DC0380495684` |
| `Buckettie-1.3.2.0-win-x64.zip` | `2648F4E404C890FF1DEC22CECC1D2D5CC226F2CF5F9CA4F7D88457F35C4C85AD` |

- Runtime: Windows x64, self-contained
- Display version: `1.3.2.0`
- Windows Installer product version: `1.3.2`
- Tag: `v1.3.2.0`

## Validation

- Automated tests: 196 passed
- MSI release build: 0 warnings, 0 errors
- MSI and ZIP SHA-256 hashes: verified
- Physical-machine upgrade from 1.3.1.0 and automatic service restart: passed
- Installed MCP version and retained `ja-JP` configuration: verified

Configuration, DPAPI tokens, audit logs, and other machine-specific data are not included in the artifacts.
