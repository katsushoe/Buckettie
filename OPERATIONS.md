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

## Repository Registration

Normal path: an MCP client calls `bitbucket_repository_register`; approve or deny the Dialog that appears on the server machine's own interactive desktop session. Approval writes `buckettie.json` and updates the running service's allowlist immediately — no `stop`/`restart` is needed for this path.

Fallback: if the tool returns `no_interactive_session` (no local console logon, an RDP-only session, or a locked workstation), use the manual flow instead — run `stop`, edit `buckettie.json`, then `config check`, `restart`, and `doctor` as described above.

On some hosts the approval Dialog process can fail to launch even with a valid console logon: the request times out with `approval_timed_out` and no Dialog ever appears on screen. This has been observed on machines running an agent sandbox layer (e.g. a `CodexSandboxUsers`-style setup) whose desktop security descriptor denies interactive attachment to processes created via `WTSQueryUserToken`/`CreateProcessWithTokenW`, regardless of which executable is launched. On such a host, use the manual flow above as the standard registration path rather than retrying the Dialog.

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
