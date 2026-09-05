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
- Physical-machine upgrade: passed after UAC elevation (service running, MCP version `1.3.26.0`, existing Bitbucket credentials usable)
- Direct Buckettie live `tag_create`, lookup, deletion, and post-deletion absence check: passed against `buckettieselftest`
- Moyai end-to-end tag mutation verification: pending because Moyai fails before invoking the Provider

Configuration, DPAPI tokens, audit logs, and other machine-specific data are not included in the artifacts.
