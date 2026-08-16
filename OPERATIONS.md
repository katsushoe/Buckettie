# Operations

## Routine Checks

```powershell
<install-root>\bin\buckettie.exe status
<install-root>\bin\buckettie.exe doctor
```

`doctor` checks configuration, Git, tokens, local repositories, Bitbucket API access, and the MCP endpoint. Exit code `0` is a pass.

## Service Control

Run service control from an elevated terminal.

```powershell
<install-root>\bin\buckettie.exe start
<install-root>\bin\buckettie.exe stop
<install-root>\bin\buckettie.exe restart
```

After changing configuration, run `config check`, restart the service, and run `doctor`.

## Token Lifecycle

Registration and rotation use the same command and hidden prompt.

```powershell
<install-root>\bin\buckettie.exe auth set <repository-id>
<install-root>\bin\buckettie.exe auth test
<install-root>\bin\buckettie.exe restart
```

Use `auth delete <repository-id>` to remove a token. Authenticated Git and Bitbucket API operations for that repository then fail until another token is registered.

## Logs and Audit

`buckettie logs` prints the audit-log directory. Logs use JSON Lines and contain caller, tool, repository, result, and fixed error classification without secret values. Monitor retention and capacity in the operating environment.

## Upgrade

1. Run `stop`.
2. Preserve `config`, `logs`, and `data`; replace the distribution files in `bin`.
3. Run `service install` again if required.
4. Run `start` and then `doctor`.

DPAPI token files are machine-bound. Register tokens again after moving to another machine.
