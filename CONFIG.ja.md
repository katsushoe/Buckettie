# Buckettie設定

[English](CONFIG.md) | [日本語](CONFIG.ja.md)

## 設定ファイル

既定の設定ファイルは実行ファイル基準の`..\config\buckettie.json`です。別のファイルは`--config <path>`で指定します。UTF-8の厳密なJSONであり、プロパティ名は大文字小文字を区別する`snake_case`です。未知のプロパティとコメントは拒否されます。

完全な例は[`buckettie.example.json`](buckettie.example.json)を参照してください。

## 設定項目

| 項目 | 必須 | 型 | 既定値 | 制約と意味 |
| :--- | :--- | :--- | :--- | :--- |
| `mcp_port` | 任意 | integer | `45450` | `1`～`65535`。Loopback MCP Port。 |
| `mcp_path` | 任意 | string | `/mcp` | `/`で始まる128文字以下。制御文字、`?`、`#`は禁止。 |
| `atlassian_email` | 必須 | string | なし | Bitbucket REST認証に使う有効なメールアドレス。秘密値ではありません。 |
| `bitbucket_username` | 必須 | string | なし | Git HTTPS認証に使う、大文字小文字を区別するBitbucket Cloud Username。 |
| `repositories` | 必須 | object | なし | 後方互換のためだけに残る旧項目。[Repository保存先](#repository保存先)を参照。稼働中のインストールでは常に`{}`で、実際のRepository情報はSQLiteに保存されます。 |

Repository IDは大文字小文字を区別し、一意でなければなりません。使用可能文字はASCII英数字、`.`、`_`、`-`で、最大128文字です。`protected_branches`は`direct_push_branches`より優先されます。

## Repository保存先

Repository情報（`workspace`、`slug`、`local_root`、`remote`、`develop_branch`、`main_branch`、`direct_push_branches`、`pull_branches`、`protected_branches`、`tag_target_branch`、`tag_pattern`、`require_clean_working_tree`）は、`buckettie.json`ではなくBinary Directory基準の`..\data\repositories.db`（SQLite Database）に保存されます。

`repositories`にRepository情報を保存していた旧Versionからのアップグレード後、初回起動時にServiceが`repositories`配下の全項目をDatabaseへ一度だけ移行し、その後`buckettie.json`の`repositories`を`{}`へ書き換えます。以降はDatabaseが唯一の正本となり、`buckettie.json`の`repositories`は再び読み込まれません（移行済みFileが引き続き検証を通るよう、JSON Schema上の項目としてのみ残ります）。

## 読み込みと秘密情報

設定の上書き階層はありません。CLIおよびServiceは、既定Pathまたは`--config`で選択された1ファイルを読み込みます。API Tokenはこのファイルへ保存しません。

TokenはBinary Directory基準の`..\data\secrets`へDPAPI LocalMachine暗号化ファイルとして保存されます。管理者Terminalで`buckettie auth set <repository-id>`を実行し、暗号化ファイルを手動編集しないでください。

## Repository登録・修正・登録解除

`bitbucket_repository_register` MCP Tool（またはCLIの`buckettie repo register`）は、以下の手動フローを使わずにRepositoryを1件追加します。受け付けるのは`repository`（新規Repository ID）、`local_root`、および任意の`remote`／`develop_branch`／`main_branch`だけです。`workspace`と`slug`は常に対象Local RepositoryのGit Remoteから導出され、呼び出し元は指定できません。`direct_push_branches`、`pull_branches`、`protected_branches`、`tag_target_branch`、`tag_pattern`、`require_clean_working_tree`は、指定されたBranch名から上記の例と同じ保守的な形でServer側が既定値を設定します。

`bitbucket_repository_update` MCP Tool（またはCLIの`buckettie repo update`）は、登録済みRepositoryの`direct_push_branches`、`pull_branches`、`protected_branches`、`tag_target_branch`、`tag_pattern`、`require_clean_working_tree`を変更します。`workspace`／`slug`／`local_root`／`remote`／`develop_branch`／`main_branch`はここでは変更できません。これらは登録時にGit Remoteに対して検証済みの値として固定されるため、指し示すRepositoryを変える場合は登録解除してから再登録します。

`register`と`update`はいずれも、Serverマシンの対話Desktop SessionでNative Dialogへの人間による承認が必須です。呼び出し元のMCP Clientから承認することはできません。信頼境界の詳細は[SECURITY.md](SECURITY.md#repository-registration-approval)、設計は[ADR 0012](docs/adr/0012-interactive-repository-registration-approval.md)・[ADR 0013](docs/adr/0013-repository-store-and-live-lifecycle.md)を参照してください。

`bitbucket_repository_unregister` MCP Tool（またはCLIの`buckettie repo unregister`）はDialogなしで即座にRepositoryを削除します。権限を削減するだけの操作であり、侵害されたClientや誤操作によって得をする余地がないためです。

これら3操作はいずれも`stop`/`restart`を必要としません。これらのToolが対応しない形の登録・修正が必要な場合は、引き続き手動編集フロー（対象がSQLite Databaseへ変わった点を除き[Repository保存先](#repository保存先)参照）を使用します。

## 検証エラー

| Code | 意味 |
| :--- | :--- |
| `InvalidJson` | JSON構文または厳密なContractが不正です。 |
| `InvalidAtlassianEmail` | `atlassian_email`が有効な単一メールアドレスではありません。 |
| `InvalidBitbucketUsername` | `bitbucket_username`が有効なUsernameではありません。 |
| `DuplicateRepositoryId` | `repositories`に同一IDが複数あります。 |
| `InvalidRepositoryId` | Repository IDの文字または長さが不正です。 |
| `RequiredValueMissing` | 必須値が欠落、null、空、空白です。 |
| `InvalidTagPattern` | `tag_pattern`が有効な正規表現ではありません。 |
| `InvalidMcpPort` | `mcp_port`が範囲外です。 |
| `InvalidMcpPath` | `mcp_path`が安全な絶対HTTP Pathではありません。 |

Filesystemの存在、`.git`、Symlink／Junction、Git Remoteは、読み込み後のRepository境界検証で確認します。
