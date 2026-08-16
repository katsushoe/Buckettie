# MCPセットアップ

[English](MCP_SETUP.md) | [日本語](MCP_SETUP.ja.md)

このGuideでは、利用者が入力する値、Clientが自動生成する設定、利用者が確認する結果を分離します。Clientごとに推奨手順を1つ示し、Project Scopeの代替設定は別Sectionに分けています。

## 前提条件

- BuckettieとMCP Clientが同じWindows MachineへInstallされ、Buckettieの設定が完了していること
- `Buckettie` Windows Serviceが登録済みであること
- `buckettie.json`にRepositoryが1件以上設定されていること
- CodexまたはClaude CodeがInstallされていること

InstallとRepository設定は[INSTALLATION.md](INSTALLATION.md)および[CONFIG.md](CONFIG.md)を参照してください。

### 利用者が入力する値

準備CommandでMachineごとに入力する値は次の2つだけです。

| 変数 | 入力する内容 | 入力例 |
| --- | --- | --- |
| `$BuckettieRoot` | MSIまたはZIPでBuckettieを配置したDirectory | `F:\Buckettie` |
| `$RepositoryId` | `repo list`が出力した大文字小文字を区別するIDの1つ | `my-project` |

MCP Server名と既定URLはClient設定例で固定しています。

| 設定 | 値 | 変更する条件 |
| --- | --- | --- |
| Server名 | `buckettie` | Client上の表示名を意図的に変える場合だけ変更します。 |
| Transport | Streamable HTTP（`http`） | 変更しません。BuckettieはstdioとSSEを公開しません。 |
| URL | `http://127.0.0.1:45450/mcp` | `buckettie.json`の`mcp_port`または`mcp_path`が異なる場合だけ変更します。 |
| MCP認証 | なし | TokenやAuthorization Headerを追加しません。 |

`<install-root>`、`<project-root>`、`<repository-id>`は説明用の記法です。山括弧付きPlaceholderをTerminalや設定Fileへそのまま入力しないでください。

## 認証と環境

BuckettieはMCP Clientの認証情報、Authorization Header、環境変数を要求しません。MCP通信はLoopbackだけで受け付け、`Origin` Headerがある場合は設定済みPortのLoopback Originに限定します。

Bitbucket認証は別の仕組みです。`auth set`はBitbucket TokenをDPAPI LocalMachineで暗号化し、`$BuckettieRoot\data\secrets`配下へ保存します。このTokenをCodexまたはClaude CodeのMCP設定へ記載しないでください。

## Serverの起動

### 手順1：Install Directoryを入力する

管理者PowerShellで実行します。引用符内のPathだけを実環境に合わせて変更してください。

```powershell
$BuckettieRoot = 'F:\Buckettie'
```

### 手順2：設定を検証してRepository IDを確認する

```powershell
& "$BuckettieRoot\bin\buckettie.exe" config check
& "$BuckettieRoot\bin\buckettie.exe" repo list
```

合格条件：

- `config check`が終了Code `0`で完了し、設定Errorを出力しないこと
- `repo list`が大文字小文字を区別するRepository IDを1行に1件出力すること

### 手順3：出力されたRepository IDを入力して検証する

引用符内のIDだけを変更してください。

```powershell
$RepositoryId = 'my-project'
& "$BuckettieRoot\bin\buckettie.exe" repo status $RepositoryId
& "$BuckettieRoot\bin\buckettie.exe" auth test
```

合格条件：

- `repo status`がBranch、HEAD、Working Tree状態を出力すること
- `auth test`がToken値を表示せず、設定済みRepositoryの認証情報を読めると報告すること

Tokenが未登録の場合は、先に`& "$BuckettieRoot\bin\buckettie.exe" auth set $RepositoryId`を実行します。非表示Promptへ入力したTokenはBuckettieが保存します。MCP Client設定へ転記しません。

### 手順4：Serviceを起動して検証する

```powershell
& "$BuckettieRoot\bin\buckettie.exe" start
& "$BuckettieRoot\bin\buckettie.exe" status
& "$BuckettieRoot\bin\buckettie.exe" mcp test
```

合格条件：

- `status`が`Buckettie` Serviceを実行中と報告すること
- `mcp test`が`[OK] MCP Endpoint (200)`を出力すること

ZIP版では`start`より前に`& "$BuckettieRoot\bin\buckettie.exe" service install`を一度実行します。MSIはServiceを自動登録します。

## Clientの登録

### Codex — 推奨：User設定へ手動追加

この手順は生成Commandを実行しません。利用者がTOML Tableを1つ追加します。

1. `%USERPROFILE%\.codex\config.toml`を開きます。
2. 既存内容をすべて保持します。
3. 次のTableを1回だけ追加します。`[mcp_servers.buckettie]`が既にある場合は、そのTableだけを置き換えます。

```toml
[mcp_servers.buckettie]
url = "http://127.0.0.1:45450/mcp"
enabled = true
required = true
default_tools_approval_mode = "writes"
```

自動生成される内容：ありません。`config.toml`を保存する操作が設定変更です。

各設定値の意味：

- `buckettie`：Clientに表示されるServer名
- `url`：Buckettieの設定済みLoopback Endpointと一致させる値
- `enabled = true`：Serverを有効化する設定
- `required = true`：Endpointを利用できない場合にClient起動時の問題として報告する設定
- `default_tools_approval_mode = "writes"`：Read-only以外のToolについてCodex Policyに従って確認する設定
- MCP認証は「なし」のため、Bearer TokenやHeader設定は追加しません。

保存後にChatGPT Desktop App、Codex CLI、またはIDE Extensionを再起動します。これらのLocal Codex Clientは同じHost設定を共有します。

### Claude Code — 推奨：User設定をCommandで自動生成

Buckettieが既定URLを使用している場合は、次のCommandを変更せずにPowerShellで実行します。置換するPlaceholderはありません。

```powershell
claude mcp add --transport http --scope user buckettie http://127.0.0.1:45450/mcp
```

CommandはClaude CodeのUser設定`~/.claude.json`を自動更新します。他の設定を保持したまま、論理的に次のServer Entryを追加します。

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

Command実行後にこのJSONを手作業で貼り付けないでください。この例は、Commandが何を登録したか確認するための表示です。登録後にClaude Codeを再起動または再読込します。

### 代替手順：Project Scope設定

MCP登録をすべてのLocal Projectではなく1つのProjectだけへ適用する場合に限り、この代替手順を使用します。

- Codex：User設定ではなく`<project-root>\.codex\config.toml`へ同じTOML Tableを記載します。
- Claude Code：前掲JSONを記載した`<project-root>\.mcp.json`を作成します。両Scopeが意図的に必要な場合を除き、User Scope Commandを併用しません。

`<project-root>`はClientを起動するSource RepositoryのRoot Directoryです。Claude CodeのProject Scope設定では、Workspaceの信頼とMCP Serverの対話的承認が必要です。

## 複数Workspace

1つのBuckettie Serviceで複数の許可済みBitbucket WorkspaceとRepositoryを扱えます。`buckettie.json`の`repositories`へ一意のRepository IDで追加します。Client登録は1つのまま維持し、各Buckettie Toolへ対象Repository IDを渡します。

Client Scopeは、どのProjectからServer登録を参照できるかだけを制御します。BuckettieのRepository Allowlist、Branch Policy、認証情報は変更しません。

## 接続確認

次の順序で確認し、失敗した時点で後続確認を止めます。

### 確認1：Buckettie Endpoint

```powershell
& "$BuckettieRoot\bin\buckettie.exe" mcp status
```

合格条件：`[OK] MCP Endpoint (200)`。

### 確認2：Buckettie Tool検出

```powershell
& "$BuckettieRoot\bin\buckettie.exe" mcp tools
```

合格条件：`[OK] MCP Tools (200)`が出力され、Responseに`bitbucket_repository_status`が含まれること。

### 確認3：Client登録

- Codex：`/mcp`を開き、`buckettie`が有効かつ接続済みであることを確認します。
- Claude Code：`claude mcp get buckettie`、続いて`claude mcp list`を実行し、接続済みStatusを確認します。

### 確認4：Read-only Tool呼び出し

Clientから`bitbucket_repository_status`を呼び出し、Repository引数へ`$RepositoryId`に保存した正確な値を指定します。

合格条件：設定済みRepositoryのBranch、HEAD、Working Tree状態が返ること。この確認はRepositoryを変更せず、Client登録、MCP Transport、Repository ID選択、Buckettie Policy解決をまとめて検証します。

### 確認5：全体診断

```powershell
& "$BuckettieRoot\bin\buckettie.exe" doctor
```

合格条件：設定、Git、API Token、Repository、Bitbucket API、MCPの全項目が`OK`であること。

## トラブルシューティング

- Commandに`<...>`が残っている：Placeholderを実値へ置換し、山括弧をそのまま入力しません。
- `repo status`が失敗する：`repo list`が出力した大文字小文字を含む正確なIDを使用します。
- 接続拒否：Serviceの実行状態を確認し、「確認1」「確認2」を再実行します。
- HTTP 404または初期化失敗：Client URLを`mcp_port`と`mcp_path`へ一致させます。
- `Origin`付きRequestがHTTP 403：設定済みPortの`127.0.0.1`または`localhost`を使用します。
- Codexに`buckettie`がない：Tableが`%USERPROFILE%\.codex\config.toml`にあることを確認し、Clientを再起動します。
- Claude Codeに`buckettie`がない：`claude mcp add`を再実行し、`claude mcp get buckettie`で確認します。
- Claude Codeが承認待ち：Projectを信頼し、Project Scopeの`.mcp.json` Entryを承認します。
- Bitbucket認証失敗：`auth test`または`auth set`を実行し、TokenをMCP Client設定へ追加しません。

Command詳細と復旧手順は[COMMANDS.md](COMMANDS.md)および[TROUBLESHOOTING.md](TROUBLESHOOTING.md)を参照してください。

Client参考資料：[OpenAI Codex MCP Documentation](https://developers.openai.com/codex/mcp)、[Claude Code MCP Documentation](https://docs.anthropic.com/en/docs/claude-code/mcp)。
