# Release 1.1.0.0

Release date: 2026-08-16

## Highlights

- Windows x64 MSIを標準成果物として追加
- MSIによる自己完結型Binary配置、Windows Service登録、Major Upgrade、Uninstall
- Portable／手動導入向け自己完結型ZIPを継続提供
- READMEにインストーラ配布、バイナリ配布、ソース配布の手順を追加
- 設定、DPAPI Token、監査LogをPackageへ含めず、Upgrade／Uninstall時も保持

## Artifacts

| File | SHA-256 |
| --- | --- |
| `Buckettie-1.1.0.0-win-x64.msi` | `4963D751F9B144890198B0E71A0420FC94A6A7D33CF78514B3ABA25839E1D759` |
| `Buckettie-1.1.0.0-win-x64.zip` | `FE127F416834322CF25775B4B5AE003BBA6B63E95928D3991EA1A5DEB60EBF8A` |

- Runtime: Windows x64、自己完結型
- Tag: `v1.1.0.0`

## Validation

- Release Build: 警告0、エラー0
- 自動Test: 110件合格
- MSI: WiX Database解析、Windows Installer管理Install、必須File、Version、SHA-256を確認
- ZIP: 展開、必須File、Version、README、SHA-256を確認

通常Install／Upgrade／Uninstallは既存のBuckettie Serviceとの競合を避けるため未実施です。実環境の設定、Token、監査LogはArtifactへ含めません。
