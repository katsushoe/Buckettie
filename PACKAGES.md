# Package contract

Version 1 Release Packageは次の構成を正本とします。実際のVersion番号とRelease TagはRelease確定時に設定します。

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
- `secrets/`とDPAPI Token File
- `logs/`と監査Log
- `.local/`、Test結果、開発者Machine固有情報
- Symbolや中間Build生成物（Release用途で別途必要な場合を除く）

配布時はPackageのHashを記録し、展開後に `buckettie version`、`config check`、`doctor`を実行します。Framework依存Packageの場合は対象.NET Runtimeを要件へ明記します。

