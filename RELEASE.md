# Release 1.3.9.0

Release date: 2026-08-26

## Highlights

- Changed the default MSI installation root to `C:\Buckettie`
- Added the installed `bin` directory to the system `PATH`
- Preserved support for custom roots through the public `INSTALLROOT` property

## Artifacts

| File | SHA-256 |
| --- | --- |
| `Buckettie-1.3.9.0-win-x64.msi` | `1FD650EB131A89FBBFC3C02565030CEF507F85D62FA86D419DDFF8B6B466103A` |
| `Buckettie-1.3.9.0-win-x64.zip` | `B968D9AC760480FDC1606A25741F6FCA4E2D81070D1AD4C1ED11C51956451658` |

- Runtime: Windows x64, self-contained
- Display version: `1.3.9.0`
- Windows Installer product version: `1.3.9`
- Tag: `v1.3.9.0`

## Validation

- Automated tests: passed (233 tests)
- MSI build and SHA-256 hash: passed
- ZIP release build and SHA-256 hash: passed
- Physical-machine upgrade: passed (service running, version `1.3.9.0`, configuration preserved)

Configuration, DPAPI tokens, audit logs, and other machine-specific data are not included in the artifacts.
