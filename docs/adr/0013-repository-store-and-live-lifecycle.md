# ADR 0013: SQLite repository store and live register/unregister/update

## Status

Accepted

## Context

`buckettie.json` held both top-level settings (`atlassian_email`, `bitbucket_username`, `mcp_*`) and the entire `repositories` allowlist. Only *adding* a repository could happen while the service ran: `bitbucket_repository_register` (ADR 0012) wrote the JSON atomically and updated the in-memory `RepositoryAllowlist` in the same call. There was no way to remove or edit a registered repository except stopping the service, hand-editing `buckettie.json`, and restarting — the manual fallback ADR 0012 itself documents for `no_interactive_session`. That fallback was used in practice for ordinary repository lifecycle changes, not just as a degraded-mode fallback, which defeats the purpose of running Buckettie as a long-lived service.

Separately, while verifying the Dialog on a host running an agent sandbox layer, it was confirmed that `CreateProcessWithTokenW`-based cross-session process creation (the mechanism ADR 0012 originally specified) is denied outright on such hosts — even `notepad.exe` launched the same way fails with `STATUS_DLL_INIT_FAILED`. This ADR also records the fix: the Dialog is now launched via a one-shot Task Scheduler task (`schtasks /Create ... /RU <account> /IT /RL LIMITED`, `/Run`, then `/Delete`) instead, which is not subject to the same denial.

## Decision

**Storage.** Repository records move out of `buckettie.json` into a SQLite database (`data/repositories.db`), accessed through a new `IRepositoryStore` interface (`LoadAllAsync`/`InsertAsync`/`UpdateAsync`/`DeleteAsync`), implemented by `SqliteRepositoryStore` using `Microsoft.Data.Sqlite`. `RepositoryRegistrationService` now persists through `IRepositoryStore.InsertAsync` instead of rewriting the whole configuration file. `buckettie.json`'s `repositories` field and its existing validation are left in place unchanged, but only as a one-time migration source: on startup, if the store is empty and the JSON's `repositories` is non-empty, every entry is inserted into the store and `buckettie.json` is rewritten with `repositories: {}`. From then on `BuckettieOptions.Repositories` is always populated by re-reading the store on every startup, never from the JSON.

**Live unregister and update.** `bitbucket_repository_unregister` and `bitbucket_repository_update` are added alongside `register`, both operating on the running service with no `stop`/`restart`, following the same validate → [approve] → persist → mutate-allowlist shape as `RepositoryRegistrationService`. A single shared `RepositoryMutationGate` (one `SemaphoreSlim(1,1)`) serializes all three operations against the same store and allowlist, replacing each service's previously-independent gate.

**Approval gating differs by operation.** `register` and `update` both require the same interactive Dialog approval as before: `update` can widen branch policy (e.g. add `main` to `direct_push_branches`), so it carries the same risk shape as granting rights in the first place. `unregister` does not require approval — it only removes rights, so there is nothing for a compromised or over-eager MCP client to gain by calling it, and gating it would just add friction to a safe operation. All three remain fully audited.

**Update is scoped to branch policy only.** `update` can change `direct_push_branches`, `pull_branches`, `protected_branches`, `tag_target_branch`, `tag_pattern`, and `require_clean_working_tree`. It cannot change `workspace`, `slug`, `local_root`, `remote`, `develop_branch`, or `main_branch` — those are fixed at registration time specifically because they were validated against the actual Git remote (ADR 0012's "never from caller input" principle). Allowing `update` to silently retarget which Bitbucket repository or local path an ID points to would bypass that validation. Changing those fields means unregistering and registering again, which re-validates.

**CLI commands call the running service, they don't duplicate its logic.** `buckettie repo register/unregister/update` are added, implemented as MCP `tools/call` HTTP requests against the service's own local endpoint (`http://127.0.0.1:<mcp_port><mcp_path>`), the same pattern `buckettie mcp status/tools/test` already use. This keeps exactly one code path that can mutate the store and allowlist — the running service — rather than letting the CLI process (which builds its own separate composition root for `repo list`/`config show`/etc.) write to the SQLite file directly and risk the in-memory allowlist going stale until the next restart.

**Project discovery.** The read-only MCP `list_projects` tool exposes the same registered repository IDs as CLI `repo list`, using the live allowlist snapshot. Server instructions require clients to call it before every push and choose the intended ID from its candidates. If a push uses an unregistered ID, the structured `repository_not_allowed` error includes `project_candidates`; it never guesses or silently substitutes the target.

## Alternatives

- Keep repositories in `buckettie.json`, extend the same atomic-write pattern to unregister/update: rejected because the user explicitly asked for SQLite storage, and a single-table SQLite store is a smaller, more direct fit for row-level insert/update/delete than rewriting a whole JSON document per mutation.
- Let the CLI write directly to `data/repositories.db` for register/unregister/update: rejected — it would leave the running service's in-memory `RepositoryAllowlist` stale until restart, exactly the problem this ADR sets out to remove, and would duplicate the validation/approval/audit logic that already lives server-side.
- Require Dialog approval for `unregister` too, for uniformity: rejected as unnecessary friction — see the approval-gating rationale above.
- Let `update` also change identity fields (`workspace`/`slug`/`local_root`/`remote`/branch names): rejected — see the update-scope rationale above.

## Impact

- `Buckettie.Infrastructure.csproj` gains a `Microsoft.Data.Sqlite` package reference.
- `RepositoryRegistrationService`'s constructor signature changes (`IRepositoryStore`/`RepositoryMutationGate` instead of `BuckettieOptions`/`IBuckettieOptionsLoader`/a configuration path); `BuckettieCompositionRoot` and its tests are updated accordingly.
- `docs/adr/0012-interactive-repository-registration-approval.md` was amended in place (not superseded) to describe the Task-Scheduler-based Dialog launch, since that fix landed as part of the same still-unreleased interactive-approval feature.

## Operational considerations

An old `buckettie.json` with a populated `repositories` migrates automatically and silently the first time an upgraded service starts; no separate migration command is needed. `data/repositories.db` should be included in the same backup/preservation steps `OPERATIONS.md` already documents for the `data` directory during upgrades.
