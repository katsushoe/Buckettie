# Release 1.3.5.0

Release date: 2026-08-23

## Highlights

- Applied the configured `ja-JP`, `en-US`, or `auto` language to CLI help, errors, and Windows service messages
- Localized MCP error messages while preserving stable machine-readable error codes
- Continued applying the installer-selected language to interactive approval dialogs
- Added bilingual MCP tool descriptions and localized remaining CLI status labels
- Starts the Windows service automatically after installation and major upgrade
- Localized the remaining MCP endpoint/tool labels and timeout messages
- Added Japanese/English descriptions to every MCP tool argument
- Requires Bitbucket Git remotes to use HTTPS and rejects SSH with migration guidance
- Adds CLI equivalents for Git, branch, pull-request, tag, and running-version MCP tools
- Propagates structured MCP operation failures to the CLI exit code

## Artifacts

| File | SHA-256 |
| --- | --- |
| `Buckettie-1.3.5.0-win-x64.msi` | `D6F7EB8BE528DA61BB353B02EEDEEA8737CCE5470FADD33AEAACCAAA8F03E7FE` |
| `Buckettie-1.3.5.0-win-x64.zip` | Generated during release |

- Runtime: Windows x64, self-contained
- Display version: `1.3.5.0`
- Windows Installer product version: `1.3.5`
- Tag: `v1.3.5.0`

## Validation

- Automated tests: passed (218 tests)
- MSI build and SHA-256 hash: passed
- ZIP release build and SHA-256 hash: pending
- Physical-machine upgrade: passed (service running automatically, version `1.3.5.0`, configuration check passed)
- HTTPS-only enforcement: passed by automated registration and existing-repository operation tests

Configuration, DPAPI tokens, audit logs, and other machine-specific data are not included in the artifacts.
