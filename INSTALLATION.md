# Installation

## MSI（推奨）

`Buckettie-<version>-win-x64.msi`を管理者権限で実行します。既定では`%ProgramFiles%\Buckettie`へ自己完結型Binary、設定Template、文書を配置し、`Buckettie` Windows Serviceを登録します。初回設定とToken登録が終わるまでServiceは起動しません。別のRootへ配置する場合は`INSTALLROOT`を指定します。

```powershell
msiexec.exe /i Buckettie-<version>-win-x64.msi INSTALLROOT="F:\Buckettie"
```

既存の手動配置版から移行する場合は、先に旧Serviceを停止・登録解除してください。実環境の設定、Token、監査LogはBackupしてから移行します。

Install後は`config\buckettie.example.json`を`config\buckettie.json`へコピーして編集し、管理者権限のTerminalでToken登録、Service起動、診断を行います。

## 1. 配置

以下はZIP版の手動配置手順です。

配布Packageを任意の `<install-root>` に展開します。Service登録後も移動しないPathを選んでください。

```text
<install-root>\
  bin\       実行BinaryとRuntime依存File
  config\    buckettie.json
  logs\      監査Log（実行時に作成）
  data\      Application固有Data
    secrets\ DPAPI暗号化Token（登録時に作成）
```

## 2. 設定

`buckettie.example.json`を`<install-root>\config\buckettie.json`へコピーし、[設定仕様](CONFIG.md)に従って編集します。`repositories`のKeyがCommandで使うRepository IDです。`slug`にはBitbucket URLの最後のRepository名を設定します。

```powershell
<install-root>\bin\buckettie.exe config check
<install-root>\bin\buckettie.exe config show
```

標準外の配置では、各Commandへ `--config <path>` を指定します。

## 3. Token登録

Repositoryごとに、管理者権限のTerminalで次を実行します。Token入力は画面に表示されません。

```powershell
<install-root>\bin\buckettie.exe auth set <repository-id>
<install-root>\bin\buckettie.exe auth test
```

TokenはDPAPI LocalMachineで暗号化され、`<install-root>\data\secrets\<repository-id>.token`へ保存されます。設定Fileや環境変数には保存しません。

Version 1.1以前の配置から移行する場合は、Serviceを停止してから`<install-root>\secrets`を`<install-root>\data\secrets`へ移動します。元DirectoryのACLを維持し、移行後に`doctor`でTokenとBitbucket APIを確認します。

## 4. Service登録と確認

管理者権限のTerminalで実行します。

```powershell
<install-root>\bin\buckettie.exe service install
<install-root>\bin\buckettie.exe start
<install-root>\bin\buckettie.exe status
<install-root>\bin\buckettie.exe doctor
```

Service名は `Buckettie`、実行AccountはLocalSystem、起動種類は自動です。既定MCP Endpointは `http://127.0.0.1:45450/mcp` です。

## Uninstall

```powershell
<install-root>\bin\buckettie.exe stop
<install-root>\bin\buckettie.exe service uninstall
```

Service登録だけが削除されます。設定、監査Log、Token Fileは必要性を確認して個別に管理してください。
