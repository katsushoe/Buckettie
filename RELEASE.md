# Release 1.2.0.0

Release date: 2026-08-17

## Highlights

- Windows標準構成として `bin`、`config`、`logs`、`data` をInstall Root直下へ分離
- DPAPI Token保存先を `data/secrets` に統一
- CLI、Server、AskPass、Service管理で共通のPath解決を使用
- MSIのInstall先を `INSTALLROOT` Propertyで指定可能に変更
- MSI／Portable ZIPの双方を標準構成へ対応

## Artifacts

| File | SHA-256 |
| --- | --- |
| `Buckettie-1.2.0.0-win-x64.msi` | `42E93A19EB46468398B86B6FA2B0F410A02A913BB05496417F90E50A9C7FD876` |
| `Buckettie-1.2.0.0-win-x64.zip` | `D7AF188B2DE6DDA47F3987E15C3F048A0BBCC2734D1A4BFC40440B5C6D6E43D7` |

- Runtime: Windows x64、自己完結型
- Tag: `v1.2.0.0`

## Validation

- Release Build: 警告0、エラー0
- 自動Test: 112件合格
- MSI: WiX Database解析、Windows Installer管理Install、必須Directory、Version、SHA-256を確認
- ZIP: 展開、標準Directory構成、必須File、Version、README、SHA-256を確認
- 実Install: `F:\Buckettie` へ標準構成でInstallし、Service起動およびDoctor全項目の合格を確認

設定、DPAPI Token、監査LogはArtifactへ含めません。1.1.0.0以前から移行する場合は、既存TokenをInstall Root直下の `secrets` から `data/secrets` へ移動してください。
