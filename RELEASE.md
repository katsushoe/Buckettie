# Release 1.3.27.0

Release date: 2026-09-06

## Highlights

- Require an explicit source branch or full 40-character commit SHA when creating a Bitbucket tag.
- Resolve and verify the source through Bitbucket before creating the tag, then return `source`, `source_kind`, and `source_hash` for target verification.
- Return typed `tag_source_invalid` and `tag_source_not_found` errors without falling back to a default branch or local HEAD.
- Preserve omission of the optional Bitbucket tag message from the API request when no message is supplied.

## Artifacts

| File | SHA-256 |
| --- | --- |
| `Buckettie-1.3.27.0-win-x64.msi` | `75C0156DA8B6177FC90CB758F7F2D1548CB216B24BEDC2C3271BF4134007A993` |

- Runtime: Windows x64, self-contained
- Display version: `1.3.27.0`
- Windows Installer product version: `1.3.27`
- Tag: `v1.3.27.0`

## Validation

- Automated tests: passed (363 tests)
- MSI build, Windows Installer database inspection, and SHA-256 verification: passed
- Physical-machine upgrade and running MCP version verification: pending
- Moyai end-to-end tag creation, lookup, deletion, and post-deletion absence verification: pending

Configuration, DPAPI tokens, audit logs, and other machine-specific data are not included in the artifacts.
