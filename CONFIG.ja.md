# Buckettie設定

Buckettieは既定で単体動作します。loopbackのMCPクライアントは、Allowlist・Branch Policy・監査ログの範囲で
すべてのRepository操作を利用できます。Serverを`--moyai`付きで起動した場合だけMoyai連携モードになり、
[認証設定](docs/provider-authentication.md)が必須、変更操作にMoyai Provider Assertionが必要になります。
`--moyai`なしで起動した場合、`provider_authentication`は保持したまま使用しません。
Bitbucket外部API Tokenは変更しません。

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

`bitbucket_repository_unregister` MCP Tool（またはCLIの`buckettie repo unregister`）は、登録・修正と同じ対話Desktop承認Dialogで承認された場合だけRepositoryを削除します。管理者用Endpointを構成していない場合、登録・修正・登録解除はloopbackの通常MCP Portから受け付け、この承認を操作の認可とします。

ServiceはLocalSystemで動作し、そのGit作成者は通常未設定のため、Repositoryごとにcommit作成者（`commit_author_name`、`commit_author_email`）を保持します。`buckettie repo register`は実行者のGit設定の`user.name`／`user.email`を既定値とし、`--commit-author-name`／`--commit-author-email`（またはregister/updateの同名MCP引数）で明示指定できます。名前とメールアドレスは両方必要で、制御文字や山括弧は使えません（`commit_author_invalid`）。`bitbucket_repository_commit`は登録済みの作成者を使い、なければRepository自身のGit設定を使います。どちらもない場合はStage前に`author_identity_missing`で失敗します。作成者は`GIT_AUTHOR_*`／`GIT_COMMITTER_*`で渡し、サービス実行アカウントの設定には依存しません。

これら3操作はいずれも`stop`/`restart`を必要としません。これらのToolが対応しない形の登録・修正が必要な場合は、引き続き手動編集フロー（対象がSQLite Databaseへ変わった点を除き[Repository保存先](#repository保存先)参照）を使用します。

## Gitリモートの解決

BuckettieはMoyai Repository Provider Contract（`remote_resolution` version 1、mode `repository_url`）に従い、対応を`bitbucket_provider_capabilities.data.remote_resolution`で表明します。ローカルGit操作の直前に、次の順でリモートを決めます。

1. Git系Tool（`repository_status`、`repository_diff`、`repository_commit`、`fetch`、`pull`、`push`、`tag_push`、`history_rewrite_*`、`force_push_with_lease`）の任意引数`remote`。Moyaiは`gitRemoteName`をここへ渡します。
2. 登録時に指定して保存した`remote`。
3. どちらもなければ自動解決します。対象RepositoryのリモートのうちHTTPS URLが登録済みの`bitbucket.org/<workspace>/<slug>`を指すもの（`.git`と末尾`/`は無視、パスの大文字小文字は区別）を候補とします。SSHリモートと、資格情報・query・fragmentを含むURLは除外します。候補が1つならそれを使い、複数なら命名規則`<ホスト>-origin-<接続方式>`（例: `bitbucket-origin-https`）に合う名前が1つだけの場合にそれを使います。

`origin`への暗黙の退避は行いません。失敗時は共通エラーコード`provider_remote_not_found`（一致するリモートがない、または指定名のリモートがない）、`provider_remote_ambiguous`（複数一致し命名規則でも1つに決まらない）、`provider_remote_mismatch`（指定名のリモートが別Repositoryを指す）を返します。指定名のリモートがSSHの場合は、従来どおり`ssh_remote_not_supported`です。

登録時の`remote`は省略できます。省略時は、ローカルRepositoryのHTTPS形式Bitbucketリモート（すべて同じRepositoryを指す必要があります）から`workspace`／`slug`を導出し、リモート名は保存せず操作ごとに自動解決します。既存の登録は保存済みのリモート名を引き続き使います。

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
