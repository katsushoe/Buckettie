# Release 1.3.4.0

Release date: 2026-08-22

## Highlights

- Applied the configured `ja-JP`, `en-US`, or `auto` language to CLI help, errors, and Windows service messages
- Localized MCP error messages while preserving stable machine-readable error codes
- Continued applying the installer-selected language to interactive approval dialogs
- Added bilingual MCP tool descriptions and localized remaining CLI status labels
- Starts the Windows service automatically after installation and major upgrade
- Localized the remaining MCP endpoint/tool labels and timeout messages
- Added Japanese/English descriptions to every MCP tool argument
- Requires Bitbucket Git remotes to use HTTPS and rejects SSH with migration guidance

## Artifacts

| File | SHA-256 |
| --- | --- |
| `Buckettie-1.3.4.0-win-x64.msi` | `01F11ED8901DDA873433578BFA27D1860157CA2A5547EA1B823F996CD420CC01` |
| `Buckettie-1.3.4.0-win-x64.zip` | `DE68DCC72E6671741FE2F78BA7578A34F46FC9F96990F78018CFA0F1187BE7B8` |

- Runtime: Windows x64, self-contained
- Display version: `1.3.4.0`
- Windows Installer product version: `1.3.4`
- Tag: `v1.3.4.0`

## Validation

- Automated tests: passed (200 tests)
- MSI and ZIP release builds: passed
- MSI and ZIP SHA-256 hashes: verified
- Physical-machine upgrade: passed (service running automatically, version `1.3.4.0`, configuration check passed)
- HTTPS-only enforcement: passed by automated registration and existing-repository operation tests

Configuration, DPAPI tokens, audit logs, and other machine-specific data are not included in the artifacts.
