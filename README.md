# Buckettie

Buckettieは、許可したローカルGit RepositoryとBitbucket CloudをMCP経由で安全に操作するWindows向けGatewayです。RepositoryごとのAllowlist、Branch保護、監査ログ、DPAPIで保護したAPI Tokenを組み合わせ、AI Clientへ必要最小限の操作だけを公開します。

Current release: `1.2.0.0`

## 開発の動機

Claude CodeやCodexからBitbucket Repositoryへ直接pushする場合、外部Siteへの通信や認証情報の利用として、AI ClientのSecurity機構による確認または停止が発生することがあります。Buckettieは、Bitbucketとの通信と認証をlocalhost上の固定Gatewayへ集約し、AI ClientにはAllowlistで制限したMCP Toolだけを公開します。これによりSecurity境界を維持しながら、AI Clientによる直接の外部通信、秘密情報の保持、繰り返し発生する確認を減らすことを目的に開発しました。

## 動作要件

- Windows 10/11またはWindows Server
- Git for Windows
- Bitbucket CloudのAPI Token
- 配布物に対応する.NET Runtime（自己完結型Packageの場合は不要）
- 操作対象RepositoryのローカルClone

## インストール方法

### インストーラ配布（推奨）

Releaseの`Buckettie-<version>-win-x64.msi`とSHA-256 Fileを取得し、
Hashを照合してからMSIを管理者権限で実行します。自己完結型Binaryは既定で
`%ProgramFiles%\Buckettie`へ配置され、Windows Serviceも登録されます。
別のRootへ配置する場合は、管理者Terminalから`INSTALLROOT`を指定します。

```powershell
msiexec.exe /i Buckettie-<version>-win-x64.msi INSTALLROOT="F:\Buckettie"
```

設定とToken登録が終わるまでServiceは起動しません。

### バイナリ配布

Releaseの`Buckettie-<version>-win-x64.zip`とSHA-256 Fileを取得し、
Hashを照合してから、Service登録後も移動しない任意の`<install-root>`へ
展開します。ZIPは自己完結型のため.NET Runtimeの別途導入は不要です。
設定後、管理者権限のTerminalから`service install`を実行します。

### ソース配布

.NET 9 SDKとGit for Windowsを導入し、RepositoryをCloneしてテスト後にMSIを生成します。

```powershell
git clone https://github.com/katsushoe/Buckettie.git
Set-Location Buckettie
dotnet test Buckettie.slnx -c Release
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Build-Msi.ps1
```

生成物は`.local\installer\output`に出力されます。生成したMSIを管理者権限で
実行します。詳細は[インストール手順](INSTALLATION.md)と
[Package構成](PACKAGES.md)を参照してください。

### 初期設定（全配布方式共通）

標準構成は`<install-root>\bin`、`config`、`logs`、`data`です。DPAPIで
暗号化したTokenは`<install-root>\data\secrets`へ保存されます。

1. `buckettie.example.json`を`<install-root>\config\buckettie.json`へコピーし、Repositoryを設定します。
2. 管理者権限のTerminalでTokenを登録し、Serviceを起動します。
   インストーラ配布ではService登録済みのため`service install`は不要です。

```powershell
<install-root>\bin\buckettie.exe config check
<install-root>\bin\buckettie.exe auth set <repository-id>
# バイナリ配布のみ実行
<install-root>\bin\buckettie.exe service install
<install-root>\bin\buckettie.exe start
<install-root>\bin\buckettie.exe doctor
```

MCP Clientの接続先は既定で `http://127.0.0.1:45450/mcp` です。外部Networkへは公開せず、同一Machine内から利用します。CodexとClaude Codeの完全な登録例は[MCPセットアップ](MCP_SETUP.ja.md)を参照してください。

## 主な機能

- Repository状態確認、fetch、pull、許可Branchへのpush
- Pull Requestの一覧、取得、差分、作成、merge
- Tagの一覧、作成
- Bitbucket APIとGit操作のRepository単位Allowlist
- JSON Lines形式の監査ログ
- Windows Service（LocalSystem）による自動起動

## 文書

- [MCP Setup (English)](MCP_SETUP.md) / [MCPセットアップ（日本語）](MCP_SETUP.ja.md)
- [設定仕様](CONFIG.md)
- [Command一覧](COMMANDS.md)
- [運用手順](OPERATIONS.md)
- [障害対応](TROUBLESHOOTING.md)
- [Security設計](SECURITY.md)
- [Package構成](PACKAGES.md)
- [文書一覧](DOCUMENTS.md)
