# Release 1.3.26.0

Release date: 2026-09-06

## Highlights

- Show the requesting project name and target repository URL explicitly in the token registration dialog.
- Omit the optional Bitbucket tag message from the API request when no message is supplied.
- Preserve the existing token registration and tag creation contracts for current MCP clients.

## Artifacts

| File | SHA-256 |
| --- | --- |
| `Buckettie-1.3.26.0-win-x64.msi` | `D46AABECF9C76F4EB99959C9EAFABC3F55DB0CCAE6ACE5D8773DBA33F11B0219` |

- Runtime: Windows x64, self-contained
- Display version: `1.3.26.0`
- Windows Installer product version: `1.3.26`
- Tag: `v1.3.26.0`

## Validation

- Automated tests: passed (351 tests)
- MSI build, Windows Installer database inspection, and SHA-256 verification: passed
- Physical-machine upgrade and live `tag_create` verification: pending

Configuration, DPAPI tokens, audit logs, and other machine-specific data are not included in the artifacts.
