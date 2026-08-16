# Operations

## 日常確認

```powershell
<install-root>\bin\buckettie.exe status
<install-root>\bin\buckettie.exe doctor
```

`doctor`は設定、Git、Token、ローカルRepository、Bitbucket API、MCP Endpointを一括確認します。終了Code `0` を正常とします。

## Service操作

起動、停止、再起動は管理者権限のTerminalで実行します。

```powershell
<install-root>\bin\buckettie.exe start
<install-root>\bin\buckettie.exe stop
<install-root>\bin\buckettie.exe restart
```

設定変更後は `config check` を通し、Serviceを再起動してから `doctor` を実行します。

## Tokenの登録・更新・削除

登録と更新は同じCommandです。管理者権限のTerminalで実行し、入力Promptへ新しいTokenを貼り付けます。

```powershell
<install-root>\bin\buckettie.exe auth set <repository-id>
<install-root>\bin\buckettie.exe auth test
<install-root>\bin\buckettie.exe restart
```

不要になったTokenは `auth delete <repository-id>` で削除できます。Tokenを削除すると該当RepositoryのBitbucket APIおよび認証が必要なGit操作は失敗します。

## Logと監査

`buckettie logs`で監査Log Directoryを確認できます。LogはJSON Lines形式です。秘密値を出力せず、呼出元、Tool、対象Repository、結果、Error分類を記録します。容量と保管期間は運用環境側で監視し、必要に応じて退避・削除してください。

## Upgrade

1. `stop`でServiceを停止します。
2. `config`、`logs`、`secrets`を保持し、`bin`の配布Fileを更新します。
3. 必要に応じて `service install` を再実行します。
4. `start`、`doctor`の順に確認します。

DPAPI Token Fileは作成Machineに束縛されるため、別MachineへCopyしても利用できません。移行先でTokenを再登録してください。

