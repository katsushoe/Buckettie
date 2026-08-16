# MCPセットアップ

[English](MCP_SETUP.md) | [日本語](MCP_SETUP.ja.md)

このGuideでは、CodexまたはClaude CodeをBuckettieのStreamable HTTP MCP Serverへ接続します。BuckettieのInstallとRepository設定は[INSTALLATION.md](INSTALLATION.md)および[CONFIG.md](CONFIG.md)を参照してください。

## 前提条件

- Windows 10/11またはWindows Server
- Installと設定が完了したBuckettie
- 起動中の`Buckettie` Windows Service
- Buckettieと同じWindows Machineで動作するCodexまたはClaude Code
- `buckettie.json`に設定済みのRepository IDが1件以上あること

Clientを登録する前に、管理者Terminalで次を実行します。

```powershell
<install-root>\bin\buckettie.exe config check
<install-root>\bin\buckettie.exe auth test
<install-root>\bin\buckettie.exe start
<install-root>\bin\buckettie.exe doctor
```

## 認証と環境

BuckettieはMCP Clientの認証情報、Authorization Header、環境変数を要求しません。MCP EndpointはLoopbackだけでListenし、`Origin` HeaderがあるRequestはOriginがLoopbackかつ設定済みMCP Portと一致する場合だけ受け付けます。

Bitbucket認証情報はMCP Client認証とは別です。RepositoryごとのTokenは`buckettie auth set <repository-id>`で登録します。BuckettieはTokenをDPAPI LocalMachineで暗号化して`<install-root>\data\secrets`へ保存します。Bitbucket TokenをCodexまたはClaude CodeのMCP設定へ記載しないでください。

既定Endpointは`http://127.0.0.1:45450/mcp`です。`buckettie.json`の`mcp_port`または`mcp_path`を変更した場合は、すべてのClient URLを同じ値へ変更します。`127.0.0.1`をLAN Addressへ置き換えたり、公開Proxy経由でEndpointを公開したりしないでください。

## Serverの起動

MSIは固定名`Buckettie`のWindows Serviceを登録します。管理者Terminalから起動と確認を行います。

```powershell
<install-root>\bin\buckettie.exe start
<install-root>\bin\buckettie.exe status
<install-root>\bin\buckettie.exe mcp test
```

ZIP版では、`start`より前に`buckettie.exe service install`を一度実行します。Service Commandと終了Codeは[COMMANDS.md](COMMANDS.md)を参照してください。

## Clientの登録

Server名は`buckettie`、TransportはStreamable HTTP、URLは設定済みLoopback Endpointを使用します。MCP境界ではClient認証を使わないため、認証Fieldや認証情報を参照する環境変数はありません。

### Codex

CodexのUser設定は`~/.codex/config.toml`です。信頼済みProjectでは`<project-root>/.codex/config.toml`も使用できます。次の完全なServer設定を追加します。

```toml
[mcp_servers.buckettie]
url = "http://127.0.0.1:45450/mcp"
enabled = true
required = true
default_tools_approval_mode = "writes"
```

- `buckettie`はCodexに表示されるServer名です。
- `url`はBuckettieの`mcp_port`と`mcp_path`に一致させます。
- Buckettieは認証なしのLoopback MCP通信だけを受け付けるため、`bearer_token_env_var`、`http_headers`、`env_http_headers`は設定しません。
- `required = true`により、Buckettie Endpointを利用できない場合にClient起動時の問題として報告されます。
- `default_tools_approval_mode = "writes"`により、Codex Policyに従ってRead-only Tool以外の使用時に確認します。

保存後にChatGPT Desktop App、Codex CLI、またはIDE Extensionを再起動します。これらのLocal Codex Clientは同じHost設定を共有します。Codexで`/mcp`を実行し、`buckettie`が接続済みであることを確認します。

### Claude Code

Project共有設定では`<project-root>/.mcp.json`を作成し、次の完全な設定を記載します。

```json
{
  "mcpServers": {
    "buckettie": {
      "type": "http",
      "url": "http://127.0.0.1:45450/mcp"
    }
  }
}
```

- `buckettie`はClaude Codeに表示されるServer名です。
- `type`には`http`を指定します。Claude Codeでは`streamable-http`も使用できます。
- `url`はBuckettieの`mcp_port`と`mcp_path`に一致させます。
- BuckettieはMCP Client認証を使用しないため、`headers`や環境変数は設定しません。

Claude CodeはProject Scopeの`.mcp.json`を初めて使う際に承認を要求します。対象ProjectでClaude Codeを起動し、信頼できるServerとして承認します。非共有のUser Scopeへ同じHTTP登録を追加する場合は次を実行します。

```powershell
claude mcp add --transport http --scope user buckettie http://127.0.0.1:45450/mcp
```

設定変更後にClaude Codeを再起動または再読込します。`claude mcp get buckettie`、`claude mcp list`、または`/mcp`で接続を確認します。

## 複数Workspace

1つのBuckettie Instanceで複数のBitbucket WorkspaceとRepositoryを扱えます。同じ`buckettie.json`の`repositories`へ許可対象を追加し、各Entryに一意のRepository IDを設定します。Client側は1つの`buckettie` MCP Server登録を維持し、各Toolへ対象Repository IDを渡します。

すべてのLocal Projectから同じBuckettie Allowlistを使う場合はCodexまたはClaude CodeのUser Scopeを使用します。選択したProjectだけから利用する場合はProject Scopeを使用します。Project Scopeを使用してもBuckettie側のRepository AllowlistやBranch Policyは変更されません。

## 接続確認

最初にServer側を確認します。

```powershell
<install-root>\bin\buckettie.exe mcp status
<install-root>\bin\buckettie.exe mcp tools
<install-root>\bin\buckettie.exe doctor
```

Tool一覧に`bitbucket_repository_status`が含まれることを確認します。CodexまたはClaude Codeで`buckettie`が接続済みであることを確認し、設定済みRepository IDを指定して`bitbucket_repository_status`を呼び出します。成功時はLocal Branch、HEAD、Working Tree状態が返ります。Tool入力とPolicy Errorは[COMMANDS.md](COMMANDS.md)および[SECURITY.md](SECURITY.md)を参照してください。

## トラブルシューティング

- Clientが接続拒否を報告する場合は、`buckettie status`、`buckettie mcp test`、`buckettie doctor`を実行します。
- HTTPまたは初期化Errorの場合は、Client URLが`mcp_port`と`mcp_path`へ完全に一致することを確認します。
- `Origin`付きRequestがHTTP 403になる場合は、Client OriginとEndpoint Hostに`http://127.0.0.1:<mcp_port>`または`http://localhost:<mcp_port>`を使用します。
- Claude CodeでProject Serverが承認待ちの場合は、Workspaceを信頼して`.mcp.json`を対話的に承認します。
- Repository Errorの場合は、`buckettie repo list`で大文字小文字を含む正確なRepository IDを確認し、`buckettie repo status <repository-id>`を実行します。
- Bitbucket認証に失敗する場合は`buckettie auth test`を実行します。Bitbucket TokenをMCP Client設定へ追加しないでください。
- Client設定を変更した後はClientを再起動または再読込します。

追加の復旧手順は[TROUBLESHOOTING.md](TROUBLESHOOTING.md)を参照してください。

Client設定の参考資料：[OpenAI Codex MCP Documentation](https://developers.openai.com/codex/mcp)、[Claude Code MCP Documentation](https://docs.anthropic.com/en/docs/claude-code/mcp)。
