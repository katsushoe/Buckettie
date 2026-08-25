# Release 1.3.6.0

Release date: 2026-08-26

## Highlights

- Added MCP Server Instructions that explain Buckettie's purpose, security boundary, and safe operation policy
- Added the `buckettie_usage` MCP Prompt with bilingual usage guidance
- Directs MCP clients to use registered repository IDs and inspect state before mutations
- Clarifies that protected-branch and repository policies must not be bypassed

## Artifacts

| File | SHA-256 |
| --- | --- |
| `Buckettie-1.3.6.0-win-x64.msi` | `B9AC2B253E18CC41BE631C33B4165B5D8AC61226EC65DFBAB4FAD39E46B84B92` |
| `Buckettie-1.3.6.0-win-x64.zip` | Generated during release packaging |

- Runtime: Windows x64, self-contained
- Display version: `1.3.6.0`
- Windows Installer product version: `1.3.6`
- Tag: `v1.3.6.0`

## Validation

- Automated tests: passed (233 tests)
- MSI build and SHA-256 hash: passed
- ZIP release build and SHA-256 hash: pending
- Physical-machine upgrade: pending

Configuration, DPAPI tokens, audit logs, and other machine-specific data are not included in the artifacts.
