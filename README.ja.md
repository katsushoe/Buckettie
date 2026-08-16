# Buckettie

[English](README.md) | [日本語](README.ja.md)

Buckettieは、許可したローカルGitリポジトリとBitbucket CloudをMCPクライアントから操作するWindows向けゲートウェイです。リポジトリAllowlist、ブランチ保護、監査ログ、DPAPIで保護したAPI Tokenにより、AIクライアントへ必要な操作だけを公開します。

現在のリリース：`1.2.0.2`

## はじめに

1. MSIをインストールするか、バイナリアーカイブを展開します。
2. `buckettie.example.json`を`<install-root>\config\buckettie.json`へコピーし、リポジトリを設定します。
3. API Tokenを登録し、サービスを起動して診断します。
4. MCPクライアントへ`http://127.0.0.1:45450/mcp`を登録します。

CodexとClaude Codeの完全な登録手順は[MCPセットアップ](MCP_SETUP.ja.md)を参照してください。

## インストール

### インストーラ配布（推奨）

リリースから`Buckettie-<version>-win-x64.msi`とSHA-256ファイルを取得し、ハッシュを検証してから管理者権限で実行します。既定のインストール先は`%ProgramFiles%\Buckettie`です。

```powershell
msiexec.exe /i Buckettie-<version>-win-x64.msi INSTALLROOT="F:\Buckettie"
```

### バイナリ配布

`Buckettie-<version>-win-x64.zip`を取得して検証し、移動しない`<install-root>`へ展開します。アーカイブは自己完結型です。設定後に`service install`を実行します。

### ソース配布

.NET 9 SDKとGit for Windowsを導入し、次を実行します。

```powershell
git clone https://github.com/katsushoe/Buckettie.git
Set-Location Buckettie
dotnet test Buckettie.slnx -c Release
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Build-Msi.ps1
```

MSIは`.local\installer\output`配下へ出力されます。詳細は[インストール](INSTALLATION.md)と[パッケージ構成](PACKAGES.md)を参照してください。

## 設定

標準構成は`<install-root>\bin`、`config`、`logs`、`data`です。DPAPIで暗号化したTokenは`<install-root>\data\secrets`配下へ保存されます。

```powershell
<install-root>\bin\buckettie.exe config check
<install-root>\bin\buckettie.exe auth set <repository-id>
<install-root>\bin\buckettie.exe start
<install-root>\bin\buckettie.exe doctor
```

ZIP版では`start`より前に`service install`を一度実行します。全設定項目と制約は[設定](CONFIG.ja.md)を参照してください。

## 使い方

- リポジトリ状態の確認と`fetch`、`pull`、許可された`push`
- Pull Requestの一覧、取得、作成、merge
- Tagの一覧と作成
- リポジトリおよびブランチ単位のGit／Bitbucket API操作制限

CLIとMCP Toolの仕様は[コマンド](COMMANDS.ja.md)を参照してください。

## 開発の動機

Claude CodeやCodexからBitbucketへ直接アクセスすると、外部通信や認証情報の利用としてAIクライアントのセキュリティ確認が発生することがあります。BuckettieはBitbucket通信と認証をlocalhost上の固定ゲートウェイへ集約し、Allowlistで制限したMCP Toolだけを公開します。これによりセキュリティ境界を維持しながら、クライアントによる直接の外部通信、秘密情報の保持、繰り返し発生する確認を減らします。

## ドキュメント

- [MCP Setup](MCP_SETUP.md) / [MCPセットアップ](MCP_SETUP.ja.md)
- [Configuration](CONFIG.md) / [設定](CONFIG.ja.md)
- [Commands](COMMANDS.md) / [コマンド](COMMANDS.ja.md)
- [インストール](INSTALLATION.md)
- [運用](OPERATIONS.md)
- [トラブルシューティング](TROUBLESHOOTING.md)
- [セキュリティ](SECURITY.md)
- [パッケージ構成](PACKAGES.md)
- [Document Index](DOCUMENTS.md) / [文書一覧](DOCUMENTS.ja.md)

## セキュリティ

MCP EndpointはLoopbackだけで待ち受けます。API Tokenを設定ファイルやMCPクライアント設定へ保存しないでください。信頼境界と脆弱性報告は[セキュリティ](SECURITY.md)を参照してください。

## ライセンス

Buckettieは[MIT License](LICENSE)で提供します。
