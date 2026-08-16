# Release 1.0.0

Release date: 2026-08-16

## Highlights

- localhost限定Streamable HTTP MCP ServerとPhase 1の14 Tool
- Repository・Branch AllowlistおよびLocal Path・Remote URL境界
- Git status、fetch、pull、push
- Bitbucket Pull RequestとTag操作
- DPAPI LocalMachine Token StoreとLocalSystem Windows Service
- JSON Lines監査Log、Management CLI、Doctor

## Artifact

- File: `Buckettie-1.0.0-win-x64.zip`
- Runtime: Windows x64、自己完結型
- Tag: `v1.0.0`
- SHA-256: `208691DB12DDD894606F2AF53ADAB5D04A8FA87CB53E619434396256500517B9`

## Release手順

1. `dotnet test Buckettie.slnx -c Release`を成功させます。
2. CLI、Server、AskPassを `win-x64`、自己完結型でpublishします。
3. [Package構成](PACKAGES.md)に従ってZIPを生成し、SHA-256を記録します。
4. 展開したPackageで `version`、`config check`、Service起動、`doctor`を確認します。
5. `develop`のRelease Commitへ署名なし注釈Tag `v1.0.0`を作成し、CommitとTagをpushします。

実環境の設定、Token、監査LogはArtifactへ含めません。
