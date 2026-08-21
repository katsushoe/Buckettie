# Release 1.3.3.0

Release date: 2026-08-21

## Highlights

- Applied the configured `ja-JP`, `en-US`, or `auto` language to CLI help, errors, and Windows service messages
- Localized MCP error messages while preserving stable machine-readable error codes
- Continued applying the installer-selected language to interactive approval dialogs
- Added bilingual MCP tool descriptions and localized remaining CLI status labels
- Starts the Windows service automatically after installation and major upgrade
- Localized the remaining MCP endpoint/tool labels and timeout messages
- Added Japanese/English descriptions to every MCP tool argument

## Artifacts

| File | SHA-256 |
| --- | --- |
| `Buckettie-1.3.3.0-win-x64.msi` | `4EB58ED5C49FA7A0DF36EE9BA984E4494AF599F9E168CDDF327DADA9CB06E740` |
| `Buckettie-1.3.3.0-win-x64.zip` | `04E0C58811F91D552B40A62322FBA098836C1CD0DE71C8986556288782990329` |

- Runtime: Windows x64, self-contained
- Display version: `1.3.3.0`
- Windows Installer product version: `1.3.3`
- Tag: `v1.3.3.0`

## Validation

- Automated tests: 198 passed
- MSI release build: 0 warnings, 0 errors
- MSI and ZIP SHA-256 hashes: verified
- Physical-machine uninstall with configuration/data/log retention: passed
- English fresh install with automatic service start and English CLI output: passed
- Original Japanese configuration SHA-256 restoration and final install: passed

Configuration, DPAPI tokens, audit logs, and other machine-specific data are not included in the artifacts.
