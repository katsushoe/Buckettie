# ADR 0011: Windows MSI installer

## Status

Accepted

## Context

ZIP配布ではBinary配置、Directory作成、Windows Service登録を利用者が個別に実行する必要がある。Windows製品として標準的なInstall、Upgrade、Uninstall経路が必要である。

## Decision

WiX Toolset 6でWindows x64・per-machine MSIを生成する。既定配置はWindowsの`ProgramFiles64Folder`配下の`Buckettie`とし、公開Directory Property `INSTALLROOT`で別Rootを指定できる。MSIは自己完結型Binary、設定Template、利用者文書、`logs`・`data` Directory、LocalSystemの`Buckettie` Service登録を管理する。初回設定とDPAPI Token登録が完了するまでServiceは起動しない。

設定、Token、監査LogはUpgradeおよびUninstallで保持する。秘密値はMSIへ含めない。

## Alternatives

- ZIPのみ: 単純だが、配置とService登録の手作業を解消できないため不採用。
- MSIX: Windows Serviceと既存のper-machine運用に追加制約があるため不採用。
- 独自Setup EXE: 保守対象とSecurity境界が増えるため不採用。

## Impact

- MSI BuildにWiX Toolset SDKのNuGet復元が必要になる。
- Windows InstallerのProductVersionは3-part、製品表示Versionは4-partで管理する。
- 既存の手動配置環境へ導入する場合は、Service名の競合を避けるため旧Serviceを先に解除する。

## Security conditions

- API Token、実環境設定、監査LogをMSIへ含めない。
- ServiceはLocalSystemで登録し、MCPは既存どおりlocalhost限定とする。
- `data/secrets`の最終ACLは`auth set`が設定する。

## Operations

Build Scriptは自己完結型publish、MSI生成、SHA-256生成を一括実行する。MSIのInstall・Repair・Uninstallは管理者権限を要求する。

## Implementation and verification

MSI Source、Build Script、Package文書、Install文書を同一変更で管理する。Build、Test、MSI Database検査、管理InstallによるPayload展開を検証する。
