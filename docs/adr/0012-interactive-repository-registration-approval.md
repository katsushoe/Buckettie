# ADR 0012: Interactive repository registration approval

## Status

Accepted

## Context

Registering a repository in `buckettie.json` grants push, Pull Request, and Tag rights. Today this requires stopping the service, hand-editing the configuration from an elevated terminal, and restarting. An MCP client asked for a `bitbucket_repository_register` tool to remove this friction.

A chat-mediated confirmation ("do you approve this?") is not a sufficient safeguard: it lives in the same trust boundary as the tool call itself, so a compromised or over-eager MCP client could forge or skip it. The allowlist must remain gated by something an MCP client cannot script around.

## Decision

Add `bitbucket_repository_register`, which requires a human physically at the machine's interactive desktop session to approve or deny a native Dialog before the repository is written to `buckettie.json`.

`Buckettie.Server` runs as a LocalSystem Windows Service in Session 0 and cannot show UI on the interactive desktop directly. On each registration request the service uses `WTSGetActiveConsoleSessionId` and `WTSQueryUserToken` to resolve the logged-on user's session token and account name, then launches a small dedicated executable, `Buckettie.ApprovalPrompt`, into that user's session by registering and immediately running a one-shot Task Scheduler task (`schtasks /Create ... /RU <account> /IT /RL LIMITED`, `/Run`, then `/Delete`) rather than calling `CreateProcessWithTokenW` directly. Some EDR/sandbox software treats a raw `WTSQueryUserToken` + `CreateProcessWithTokenW` cross-session launch as a token-impersonation lateral-movement signature and denies the resulting process interactive-desktop attachment outright — confirmed on one such host, where even `notepad.exe` launched this way failed with `STATUS_DLL_INIT_FAILED`. Task Scheduler's own interactive-token session-crossing is not subject to the same denial. The service and the prompt process exchange a fixed, length-prefixed JSON request/response over a per-request Named Pipe (a fresh GUID name, ACL-restricted to the resolved user's SID and LocalSystem); the pipe is created before the task is launched and torn down after the exchange. The Dialog fails closed: no response within the approval Timeout is treated as Denied.

Workspace and Slug are always derived from the local repository's actual Git remote (`git remote get-url`), never from caller-supplied values, so a client cannot claim a URL that does not match the local repository it is pointing at. Branch-policy fields (`direct_push_branches`, `pull_branches`, `protected_branches`, `tag_target_branch`, `tag_pattern`, `require_clean_working_tree`) are entirely server-defaulted from the supplied develop/main branch names; the caller cannot request a more permissive policy than the default. A registration that needs different policy still uses the existing manual edit flow.

`RepositoryAllowlist` becomes a copy-on-write structure guarded by a lock for the single `Register` writer; existing lock-free readers (`TryGet`) are unaffected. On approval the service writes the updated configuration to a temporary file and moves it over `buckettie.json` (the same pattern `DpapiFileTokenStore` already uses for its own file writes) before mutating the in-memory allowlist, so a disk failure never leaves memory ahead of what a restart would reload. A `SemaphoreSlim(1,1)` rejects a second concurrent registration request outright rather than stacking a second Dialog on the same desktop.

## Alternatives

- Chat-mediated confirmation only: rejected as described above — it does not add an independent trust boundary.
- An always-running tray/startup companion process listening on a well-known channel: rejected for this release. It needs autostart registration and an idle background process for a rarely used operation; an on-demand launch needs neither.
- A loopback HTTP page opened in the browser: rejected because an MCP client with browser-automation tools could interact with the page itself, undermining the "cannot be reached from the calling session" property that motivated this design.

## Impact

- New project `Buckettie.ApprovalPrompt` targets `net9.0-windows` for WinForms; it cannot be a `ProjectReference` from the plain-`net9.0` `Buckettie.Server`/`Buckettie.Cli` projects (NuGet rejects `net9.0` referencing `net9.0-windows7.0` as incompatible). It is published and packaged as an independent sibling executable, the same way `Buckettie.AskPass` already is in `Build-Msi.ps1`'s project list.
- `Program.cs`'s configuration `FileStream` is now scoped to composition only and disposed before the service starts serving, instead of being held open for the service's lifetime; this was also the direct cause of the hand-edit failure that motivated re-examining the manual flow.

## Security conditions

- The approval Dialog process receives only the fixed, non-secret fields needed to display the request (Repository ID, Workspace, Slug, LocalRoot, RemoteUrl) — no Token, no Bitbucket credentials.
- The Named Pipe's ACL grants access only to the resolved interactive user's SID and LocalSystem; it is not reachable by other local users or processes.
- No interactive session, a launch failure, a denial, and a Timeout all fail closed: none of them mutate the in-memory allowlist or `buckettie.json`.
- The audit log records the fixed `bitbucket_repository_register` tool name, Repository ID, result, duration, and error code, following ADR 0007's existing schema; Workspace, Slug, and LocalRoot are not added as logged fields.

## Operational considerations

`no_interactive_session` (no local console logon, RDP-only session, locked workstation edge cases) falls back to the existing manual `stop` → edit → `config check` → `restart` → `doctor` flow documented in OPERATIONS.md. The interactive-session and process-launch code paths cannot run in CI; they require manual verification on a real Windows workstation with an active console logon.

## Implementation

`Buckettie.Application.Interactive` defines the `IInteractiveApprovalPrompt` seam and the wire DTOs shared with `Buckettie.ApprovalPrompt`. `Buckettie.Infrastructure.Interactive` composes `ISessionTokenProvider`, `IApprovalProcessLauncher`, and `IApprovalPipeTransport` into `WindowsInteractiveApprovalPrompt`. `RepositoryRegistrationService` in `Buckettie.Server` orchestrates validation, approval, and persistence; `AuditedRepositoryRegistrationService` wraps it following the same decorator pattern as `AuditedGitGateway`/`AuditedBitbucketRepositoryGateway`.
