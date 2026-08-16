# Release 1.2.0.2

Release date: 2026-08-17

## Highlights

- Separated user-entered values, automatically generated settings, and verification results in the MCP setup guides
- Added an explicit value table for the installation root, Repository ID, MCP server name, transport, URL, and authentication
- Defined one recommended path per client: manual user configuration for Codex and automatic user registration for Claude Code
- Moved project-scoped configuration into a clearly labeled alternative procedure
- Added ordered server, tool-discovery, client-registration, read-only tool-call, and complete-diagnosis checks with pass conditions
- Kept the English and Japanese guides synchronized

## Artifacts

| File | SHA-256 |
| --- | --- |
| `Buckettie-1.2.0.2-win-x64.msi` | `DCDEB07B954A3832422DE44006A3760AADF475ABAF7FC821FC67DF6D4CF66843` |
| `Buckettie-1.2.0.2-win-x64.zip` | `5560EEE02AF8DEF0CB40395FD9119079864D376638AC9F39596699238FB30AFC` |

- Runtime: Windows x64, self-contained
- Display version: `1.2.0.2`
- Windows Installer product version: `1.2.2`
- Tag: `v1.2.0.2`

## Validation

- Automated tests: 112 passed
- Markdown, JSON, TOML, local links, and bilingual command parity: passed
- MSI release build: 0 warnings, 0 errors
- MSI and ZIP: version, SHA-256, standard directory layout, and both MCP setup guides verified

Configuration, DPAPI tokens, audit logs, and other machine-specific data are not included in the artifacts.
