# Troubleshooting

最初に `buckettie status`、`buckettie doctor`、`buckettie logs` を実行します。Commandの終了Codeは、`0`が成功、`1`が処理または診断失敗、`2`が入力または設定不正です。

## Serviceが起動しない

- 管理者権限で `service status` を確認します。
- `config check`で設定Errorを解消します。
- `bin`、`config`、`logs`、`data`の配置とAccess権を確認します。
- Windows Event ViewerのSystem LogでService Control ManagerのErrorを確認します。

## Tokenを取得できない、認証に失敗する

- `auth test`でRepository IDごとの読取結果を確認します。
- `auth set <repository-id>`でTokenを再登録します。更新時も同じCommandです。
- TokenのBitbucket権限、失効、有効期限、対象Workspaceを確認します。
- Token Fileを別MachineからCopyした場合は削除し、利用Machine上で再登録します。
- Token値をLog、Issue、画面Captureへ掲載しないでください。

## MCPへ接続できない

- ServiceがRunningであることを確認します。
- `mcp status`または`mcp test`を実行します。
- Client URLが設定の `mcp_port` と `mcp_path` に一致することを確認します。
- EndpointはLoopback専用です。別Machineから直接接続できません。

## Git操作が失敗する

- `repo status <repository-id>`でLocal Path、Remote URL、Branch、Working Treeを確認します。
- `require_clean_working_tree`が有効な操作では未Commit変更を解消します。
- `remote`のURLが設定したBitbucket WorkspaceとSlugに一致することを確認します。
- pushの `nothing_to_push` は差分がない場合の正常結果です。
- Branch AllowlistとProtected Branch規則は[設定仕様](CONFIG.md)を確認します。

## Bitbucket API操作が失敗する

- `doctor`でAPI疎通と認証を確認します。
- Workspace、Slug、Repository IDの取り違えを確認します。
- Pull Requestの状態、Source/Destination Branch、Tag名Patternを確認します。
- 詳細は監査LogのError分類を確認し、秘密値を除いた情報だけを共有します。
