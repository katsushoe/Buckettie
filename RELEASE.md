# Release 1.3.0.0

Release date: 2026-08-21

## Highlights

- Added Japanese/English language selection to the MSI installer
- Persisted the selected UI language in `buckettie.json` with `ja-JP`, `en-US`, and `auto` validation
- Applied the configured language to interactive approval dialogs
- Centered approval dialogs on the primary desktop and kept them in the foreground
- Preserved existing configuration and machine-specific data during upgrades

## Artifacts

| File | SHA-256 |
| --- | --- |
| `Buckettie-1.3.0.0-win-x64.msi` | `73D937584B67C8B6C3057E16D218B5059DBE066212536D3C359F65833E2B3011` |
| `Buckettie-1.3.0.0-win-x64.zip` | `C27EFEE6E6430756261D3ED287A46D179D3BCD960F91E771F47D54D10FA7C20A` |

- Runtime: Windows x64, self-contained
- Display version: `1.3.0.0`
- Windows Installer product version: `1.3.0`
- Tag: `v1.3.0.0`

## Validation

- Automated tests: 191 passed
- MSI release build: 0 warnings, 0 errors
- MSI and ZIP SHA-256 hashes: verified
- MSI install and service start on a physical Windows machine: passed
- Running MCP version after installation: `1.3.0.0`
- Japanese approval dialog labels, buttons, countdown, centering, and foreground display: passed

Configuration, DPAPI tokens, audit logs, and other machine-specific data are not included in the artifacts.
