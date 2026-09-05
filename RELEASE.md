# Release 1.3.24.0

Release date: 2026-09-05

## Highlights

- Preview and execute latest-commit Author/Committer corrections while preserving tree, parents, message, and dates.
- Create a persistent recovery reference before rewriting and reject signed-commit rewrites unless signature removal is explicitly approved.
- Add separately authorized, single-branch `force-with-lease` push with actual-remote comparison and post-push verification.
- Expose symmetric MCP/CLI operations, structured errors, provider capabilities, and detailed audit fields.

## Artifacts

| File | SHA-256 |
| --- | --- |
| `Buckettie-1.3.24.0-win-x64.msi` | `0D466C7A844EA7F4746952A68C89BA58DA079092A2B85EA11FB2747C8E92A0B1` |

- Runtime: Windows x64, self-contained
- Display version: `1.3.24.0`
- Windows Installer product version: `1.3.24`
- Tag: `v1.3.24.0`

## Validation

- Automated tests: passed (343 tests)
- MSI build and SHA-256 hash: passed
- Physical-machine upgrade: passed after UAC elevation (service running, CLI and MCP version `1.3.24.0`, configuration preserved)

Configuration, DPAPI tokens, audit logs, and other machine-specific data are not included in the artifacts.
