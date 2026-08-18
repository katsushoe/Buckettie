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
| `repositories` | 必須 | object | なし | Repository IDをKey、Repository設定をValueとする1件以上のDictionary。 |
| `repositories.<id>.workspace` | 必須 | string | なし | Bitbucket Workspace Slug。 |
| `repositories.<id>.slug` | 必須 | string | なし | Bitbucket Repository URL末尾のRepository Slug。 |
| `repositories.<id>.local_root` | 必須 | string | なし | 許可する既存Local Git Repositoryの絶対Path。 |
| `repositories.<id>.remote` | 必須 | string | なし | 検証および通信に使うGit Remote名。通常は`origin`。 |
| `repositories.<id>.develop_branch` | 必須 | string | なし | 開発Branch名。 |
| `repositories.<id>.main_branch` | 必須 | string | なし | Main Branch名。 |
| `repositories.<id>.direct_push_branches` | 必須 | string array | なし | 直接Pushを許可するBranchの完全一致一覧。 |
| `repositories.<id>.pull_branches` | 必須 | string array | なし | Pullを許可するBranchの完全一致一覧。 |
| `repositories.<id>.protected_branches` | 必須 | string array | なし | 直接Pushを拒否する保護Branch一覧。 |
| `repositories.<id>.tag_target_branch` | 必須 | string | なし | Tag作成を許可する対象Branch。 |
| `repositories.<id>.tag_pattern` | 必須 | string | なし | 許可するTag名の有効な.NET正規表現。 |
| `repositories.<id>.require_clean_working_tree` | 任意 | boolean | `true` | 対象操作でClean Working Treeを要求するか。 |

Repository IDは大文字小文字を区別し、一意でなければなりません。使用可能文字はASCII英数字、`.`、`_`、`-`で、最大128文字です。`protected_branches`は`direct_push_branches`より優先されます。

## 読み込みと秘密情報

設定の上書き階層はありません。CLIおよびServiceは、既定Pathまたは`--config`で選択された1ファイルを読み込みます。API Tokenはこのファイルへ保存しません。

TokenはBinary Directory基準の`..\data\secrets`へDPAPI LocalMachine暗号化ファイルとして保存されます。管理者Terminalで`buckettie auth set <repository-id>`を実行し、暗号化ファイルを手動編集しないでください。

## Repository登録

`bitbucket_repository_register` MCP Toolは、以下の手動フローを使わずに`repositories`へ1件追加します。受け付けるのは`repository`（新規Repository ID）、`local_root`、および任意の`remote`／`develop_branch`／`main_branch`だけです。`workspace`と`slug`は常に対象Local RepositoryのGit Remoteから導出され、呼び出し元は指定できません。`direct_push_branches`、`pull_branches`、`protected_branches`、`tag_target_branch`、`tag_pattern`、`require_clean_working_tree`は、指定されたBranch名から上記の例と同じ保守的な形でServer側が既定値を設定します。異なるPolicyが必要な登録は、引き続き手動編集フローを使用します。

呼び出しごとに、Serverマシンの対話Desktop SessionでNative Dialogへの人間による承認が必須です。呼び出し元のMCP Clientから承認することはできません。信頼境界の詳細は[SECURITY.md](SECURITY.md#repository-registration-approval)、設計は[ADR 0012](docs/adr/0012-interactive-repository-registration-approval.md)を参照してください。

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
