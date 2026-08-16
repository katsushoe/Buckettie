# Package contract

Version 1.1以降のWindows標準成果物は `Buckettie-<version>-win-x64.msi` とSHA-256 Fileです。自己完結型ZIPはPortable／手動導入用の補助成果物として併記できます。Version 1.2以降の標準構成は`bin`、`config`、`logs`、`data`です。

Version 1 Release Packageは `Buckettie-1.0.0-win-x64.zip` とし、次の構成を正本とします。

| Package内Path | 内容 |
| --- | --- |
| `bin/buckettie.exe` | Management CLI |
| `bin/Buckettie.Server.exe` | MCP Windows Service |
| `bin/Buckettie.AskPass.exe` | Git認証補助Process |
| `bin/*.dll`、`*.deps.json`、`*.runtimeconfig.json` | 実行依存File |
| `config/buckettie.example.json` | 秘密値を含まない設定Template |
| `docs/*.md` | README、設定、Command、運用、障害対応、Security文書 |

次の実環境DataはPackageへ含めません。

- `config/buckettie.json`
- `data/`とDPAPI Token File
- `logs/`と監査Log
- `.local/`、Test結果、開発者Machine固有情報
- Symbolや中間Build生成物（Release用途で別途必要な場合を除く）

Version 1はWindows x64向け自己完結型Packageとして発行します。配布時はSHA-256 Hashを記録し、展開後に `buckettie version`、`config check`、`doctor`を実行します。

MSIは既定の`%ProgramFiles%\Buckettie`または`INSTALLROOT`で指定したRootへの配置、Directory作成、Windows Service登録、Major Upgrade、Uninstallを管理します。実環境の`buckettie.json`、`data`、DPAPI Token、監査LogはMSIへ含めず、Upgrade／Uninstallでも保持します。
