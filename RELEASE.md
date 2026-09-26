# Release 1.3.35.0

Release date: 2026-09-27

## Highlights

- **Standalone by default, Moyai integration opt-in.** The server runs standalone unless started with `--moyai`; in standalone mode local loopback MCP clients can use every repository tool under the allowlist, branch policies and audit log. With `--moyai`, change tools require a Moyai Provider Assertion (`Moyai.ProviderAuthentication` 1.0.2) and missing authentication configuration stops startup. `bitbucket_provider_capabilities.data.authentication.integration_mode` reports `standalone` or `moyai`.
- **Moyai connectivity.** MCP discovery traffic (`server/discover`, sent first by MCP C# SDK 2.2+ clients, plus resource listing, notifications and session `GET`/`DELETE`) now passes the authentication boundary. In standalone mode a Moyai assertion on a loopback request is ignored instead of rejected. Disallowed methods return `mcp_method_not_allowed`.
- **Local administration with desktop approval.** Register, update and unregister are accepted from loopback and authorized by the interactive desktop approval dialog; unregister now also requires approval, and the dialog title names the operation. An optional mutually authenticated HTTPS administrator endpoint remains available.
- **Explicit commit author.** Each repository stores a commit author (defaulting to the registering user's Git identity). `bitbucket_repository_commit` passes it through `GIT_AUTHOR_*`/`GIT_COMMITTER_*`, falls back to the repository's Git config, and otherwise fails with `author_identity_missing` before staging. `repo update` can change only the author.
- **Token dialog without a deadline.** `auth set` / `repo register` wait until the token dialog is submitted or cancelled; a dialog that exits before connecting reports `TokenPromptLaunchFailed`.

## Artifacts

| File | SHA-256 |
| --- | --- |
| `Buckettie-1.3.35.0-win-x64.msi` | `96B89E8832AE2F523EF1BFD2BEA370DD1B490EE1E1357C2FCDD913DA1A25C4EF` |

- Runtime: Windows x64, self-contained
- Display version: `1.3.35.0`
- Windows Installer product version: `1.3.35`
- Tag: `v1.3.35.0`

## Upgrade notes

- Existing repository databases gain `commit_author_name` / `commit_author_email` columns automatically. Register an author with `buckettie repo update <id> --commit-author-name <name> --commit-author-email <email>` for repositories whose Git config has no `user.name`/`user.email`.
- An MSI upgrade rewrites the service arguments. If the service runs with `--moyai`, re-apply the option after upgrading.

## Validation

- Automated tests: passed (517 tests)
- MSI build, Windows Installer database inspection, and SHA-256 verification: passed
- Physical-machine upgrade to 1.3.35.0 with configuration and token preservation: passed
- Moyai 1.3.5.0 `repository_status` through Buckettie in standalone mode: passed
- Moyai-delegated commit/push in integration mode: pending (requires `--moyai` and Moyai write scopes)

Configuration, DPAPI tokens, audit logs, and other machine-specific data are not included in the artifacts.
