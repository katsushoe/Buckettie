# Buckettieコマンド

[English](COMMANDS.md) | [日本語](COMMANDS.ja.md)

管理実行ファイルは`buckettie.exe`です。既定設定は実行ファイル基準の`..\config\buckettie.json`です。`--config <path>`で上書きできます。

| Command | 目的 |
| :--- | :--- |
| `buckettie doctor` | 設定、Git、認証情報、ローカルRepository、Bitbucket API、MCP Endpointを診断します。 |
| `buckettie start` | 登録済み`Buckettie` Windows Serviceを起動します。 |
| `buckettie stop` | Windows Serviceを停止します。 |
| `buckettie restart` | Windows Serviceを再起動します。 |
| `buckettie status` | Windows Serviceの状態を表示します。 |
| `buckettie service install` | 固定Release Binaryと設定でServiceを自動起動登録します。 |
| `buckettie service uninstall` | Service登録を削除します。 |
| `buckettie service status` | Service状態を表示します。 |
| `buckettie config check` | 厳密な設定Contractを検証します。 |
| `buckettie config show` | 有効な秘密値以外の設定を表示します。 |
| `buckettie repo list` | 設定済みRepository IDを一覧表示します。 |
| `buckettie repo status <id>` | ローカルRepositoryを検証して状態を表示します。 |
| `buckettie repo diff <id>` | `HEAD`に対する作業ツリー差分を取得します。 |
| `buckettie repo commit <id> <message>` | 作業ツリーの変更をすべてStageし、Policyで許可された現在Branchへlocal commitします。 |
| `buckettie repo fetch\|pull\|push <id>` | MCPサービス経由でポリシー検証付きGit操作を実行します。 |
| `buckettie repo list` / MCP `list_projects` | 操作前に選択する登録済みリポジトリIDを一覧します。MCPクライアントは毎回のpush前に`list_projects`を呼び出します。 |
| `buckettie repo register <id> <local-root> ...` | 最前面・画面中央のDialogでAPI Tokenを入力してからMCPサービス経由でRepositoryを登録します。MCPから直接登録する場合も、未登録TokenをMCP引数へ露出せずホスト上で入力します。`--console-token`では非表示のTerminal入力を使用します。 |
| `buckettie repo unregister\|update ...` | MCPサービス経由で許可リストとブランチポリシーを管理します。 |
| `buckettie branch list\|get\|create\|delete ...` | Bitbucketブランチを一覧・取得・作成・削除します。作成元の明示指定が必須です。develop、main、保護ブランチは削除できません。 |
| `buckettie pr list\|get\|diff\|create\|merge ...` | Bitbucketプルリクエストの一覧・詳細・差分・作成・マージを実行します。 |
| `buckettie tag list\|get\|create\|delete\|push ...` | ポリシー準拠タグの一覧・取得・作成・削除・明示的pushを実行します。 |
| `buckettie provider capabilities` | Repository diff・commitを含むRepository Contract操作の対応可否を表示します。 |
| `buckettie auth test` | 値を表示せず各Repositoryの認証情報を読めるか確認します。 |
| `buckettie auth set <id>` | 非表示でTokenを読み、DPAPI LocalMachineで保護して保存します。 |
| `buckettie auth delete <id>` | Repository単位の暗号化Tokenファイルを削除します。 |
| `buckettie mcp status` | MCP initialize Requestを実行します。 |
| `buckettie mcp tools` | MCP Tool一覧を取得します。 |
| `buckettie mcp test` | MCP initialize Smoke Testを実行します。 |
| `buckettie mcp version` | 稼働中MCPサーバーのバージョンを表示します。 |
| `buckettie logs` | 監査ログDirectoryを表示します。 |
| `buckettie version` | CLI Assembly Versionを表示します。 |

終了Codeは`0`が成功、`1`が処理または診断失敗、`2`が入力または設定不正です。`install`、`uninstall`、`start`、`stop`、`restart`は管理者Terminalから実行します。Service名は`Buckettie`、起動種類は自動、ServerはCLI Binary Directoryの`Buckettie.Server.exe`、設定は`..\config\buckettie.json`です。

ServiceはLocalSystemとして実行し、`..\data\secrets`のMachine保護Tokenを読みます。起動後に`buckettie doctor`を再実行してください。

導入と通常運用は[INSTALLATION.md](INSTALLATION.md)および[OPERATIONS.md](OPERATIONS.md)、障害時は[TROUBLESHOOTING.md](TROUBLESHOOTING.md)を参照してください。

## 作成元の明示指定と参照欠落時の状態取得

`buckettie branch create <repository> <branch> <source>`は、作成元Branch名または完全40桁コミットSHAが必須です。例: `buckettie branch create example develop main`でmainから初回のリモートdevelopを作成します。作成元の暗黙補完、短縮コミットSHAの解決、ローカルcheckout、自動fetchは行いません。MCP `bitbucket_branch_create`も`repository`、`branch`、`source`が必須となり、旧呼び出しは更新が必要です。空白・リビジョン式は`branch_source_invalid`、作成元GETの404はBranchなら`branch_not_found`、Commitなら`branch_source_not_found`です。404は参照またはRepositoryの不可視も含みます。返却には`source`、`source_kind`、`source_hash`と作成Branchの`target_hash`が含まれます。

`repo status`は設定済みremote-tracking参照が欠落しても、ローカルBranch/HEAD/作業ツリー状態を返します。`remote_develop_head`/`remote_main_head`はnullになり得ます。developとの比較不能時の`ahead`/`behind`もnullです。`comparison_reference`、`comparison_unavailable_reason`、`missing_remote_references`で理由を示し、未fetchの可能性もあるためBitbucket上の不存在とは断定しません。他のエラーは失敗として扱います。Provider能力は`contract_version: 2`、`branch_source_required: true`、`repository_status_nullable: true`を返し、Moyaiはsourceの無損失転送とnullの保持が必要です。設計判断は[ADR 0014](docs/adr/0014-explicit-branch-source-and-partial-status.md)を参照してください。
