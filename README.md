# Buckettie

Buckettieは、許可したローカルGit RepositoryとBitbucket CloudをMCP経由で安全に操作するWindows向けGatewayです。RepositoryごとのAllowlist、Branch保護、監査ログ、DPAPIで保護したAPI Tokenを組み合わせ、AI Clientへ必要最小限の操作だけを公開します。

Current release: `1.0.0`

## 開発の動機

Claude CodeやCodexからBitbucket Repositoryへ直接pushする場合、外部Siteへの通信や認証情報の利用として、AI ClientのSecurity機構による確認または停止が発生することがあります。Buckettieは、Bitbucketとの通信と認証をlocalhost上の固定Gatewayへ集約し、AI ClientにはAllowlistで制限したMCP Toolだけを公開します。これによりSecurity境界を維持しながら、AI Clientによる直接の外部通信、秘密情報の保持、繰り返し発生する確認を減らすことを目的に開発しました。

## 動作要件

- Windows 10/11またはWindows Server
- Git for Windows
- Bitbucket CloudのAPI Token
- 配布物に対応する.NET Runtime（自己完結型Packageの場合は不要）
- 操作対象RepositoryのローカルClone

## クイックスタート

1. [インストール手順](INSTALLATION.md)に従って配布物を配置します。
2. `buckettie.example.json`を`<install-root>\config\buckettie.json`へコピーし、Repositoryを設定します。
3. 管理者権限のTerminalでTokenを登録し、Serviceを起動します。

```powershell
<install-root>\bin\buckettie.exe config check
<install-root>\bin\buckettie.exe auth set <repository-id>
<install-root>\bin\buckettie.exe service install
<install-root>\bin\buckettie.exe start
<install-root>\bin\buckettie.exe doctor
```

MCP Clientの接続先は既定で `http://127.0.0.1:45450/mcp` です。外部Networkへは公開せず、同一Machine内から利用します。

## 主な機能

- Repository状態確認、fetch、pull、許可Branchへのpush
- Pull Requestの一覧、取得、差分、作成、merge
- Tagの一覧、作成
- Bitbucket APIとGit操作のRepository単位Allowlist
- JSON Lines形式の監査ログ
- Windows Service（LocalSystem）による自動起動

## 文書

- [設定仕様](CONFIG.md)
- [Command一覧](COMMANDS.md)
- [運用手順](OPERATIONS.md)
- [障害対応](TROUBLESHOOTING.md)
- [Security設計](SECURITY.md)
- [Package構成](PACKAGES.md)
- [文書一覧](DOCUMENTS.md)
