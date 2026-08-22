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

Repository records live in a SQLite database (`data/repositories.db`), not `buckettie.json`. Register, unregister, and update all work against the *running* service — none of them need `stop`/`restart`:

- **Register**: an MCP client calls `bitbucket_repository_register` (or `buckettie repo register <repository> <local-root>`); approve or deny the Dialog that appears on the server machine's own interactive desktop session. Workspace/Slug are derived from the local Git remote and branch policy is server-defaulted.
- **HTTPS remote required**: Buckettie rejects SSH Git remotes before registration and before every local Git operation. Change an existing remote with `git remote set-url origin https://bitbucket.org/<workspace>/<repository>.git`, then retry.
- **Update**: `bitbucket_repository_update` (or `buckettie repo update <repository> --direct-push-branches ... --pull-branches ... --protected-branches ... --tag-target-branch ... --tag-pattern ...`) changes an existing repository's branch policy and also requires Dialog approval, since it can widen what's allowed. Workspace/Slug/LocalRoot/Remote/DevelopBranch/MainBranch cannot be changed this way — unregister and re-register instead, so the binding is re-validated against the actual Git remote.
- **Unregister**: `bitbucket_repository_unregister` (or `buckettie repo unregister <repository>`) removes a repository immediately, with no Dialog — it only revokes rights.

Fallback: if register/update return `no_interactive_session` (no local console logon, an RDP-only session, or a locked workstation), fall back to the manual flow instead — run `stop`, edit `buckettie.json`'s top-level settings or drop a row via a SQLite client against `data/repositories.db`, then `config check`, `restart`, and `doctor`.

On some hosts the approval Dialog process previously failed to launch even with a valid console logon (`approval_timed_out`, no Dialog ever appears on screen). This was observed on machines running an agent sandbox layer (e.g. a `CodexSandboxUsers`-style setup) whose desktop security descriptor denied interactive attachment to processes created via `WTSQueryUserToken`/`CreateProcessWithTokenW`, regardless of which executable was launched. The Dialog is now launched via a one-shot Task Scheduler task (`/RU <user> /IT`) instead, which is not subject to the same denial on the hosts where this was tested. If a host still shows `approval_timed_out` with no visible Dialog after this change, fall back to the manual flow above.

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
