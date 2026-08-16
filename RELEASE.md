# Release 1.2.0.1

Release date: 2026-08-17

## Highlights

- Added the public `MCP_SETUP.md` guide for connecting Codex and Claude Code
- Added the synchronized Japanese guide `MCP_SETUP.ja.md`
- Documented complete Streamable HTTP client configurations, authentication boundaries, multi-workspace use, connection verification, and troubleshooting
- Included both MCP setup guides in the MSI and portable ZIP documentation

## Artifacts

| File | SHA-256 |
| --- | --- |
| `Buckettie-1.2.0.1-win-x64.msi` | `F540A0CE51A8B6F05F27FA2FAEEEBB9586F6327D95D606FE349526C2383DE7F9` |
| `Buckettie-1.2.0.1-win-x64.zip` | `1D57C8C77E0D8F414817F549A876A7ECA318A92B36D991F5502DA6AC0CAB465B` |

- Runtime: Windows x64, self-contained
- Display version: `1.2.0.1`
- Windows Installer product version: `1.2.1`
- Tag: `v1.2.0.1`

## Validation

- Automated tests: 112 passed
- Markdown, JSON, TOML, and local-link parsing: passed
- MSI release build: 0 warnings, 0 errors
- MSI and ZIP: version, SHA-256, standard directory layout, and both MCP setup guides verified

Configuration, DPAPI tokens, audit logs, and other machine-specific data are not included in the artifacts.
