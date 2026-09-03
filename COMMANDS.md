# Buckettie commands

[English](COMMANDS.md) | [日本語](COMMANDS.ja.md)

The management executable is `buckettie.exe`. Its default configuration is `..\config\buckettie.json` relative to the executable directory. Override it with `--config <path>`.

| Command | Purpose |
| --- | --- |
| `buckettie doctor` | Diagnose configuration, Git, credentials, local repositories, Bitbucket API, and MCP endpoint. |
| `buckettie start` | Start the registered `Buckettie` Windows Service. |
| `buckettie stop` | Stop the registered Windows Service. |
| `buckettie restart` | Restart the registered Windows Service. |
| `buckettie status` | Show the registered Windows Service state. |
| `buckettie service install` | Register the fixed release binary and configuration for automatic startup. |
| `buckettie service uninstall` | Remove the Windows Service registration. |
| `buckettie service status` | Show the Windows Service state. |
| `buckettie config check` | Validate the strict configuration contract. |
| `buckettie config show` | Print effective non-secret configuration. |
| `buckettie repo list` | List configured Repository IDs. |
| `buckettie repo status <id>` | Validate and show local repository status. |
| `buckettie repo diff <id>` | Return the working-tree diff against `HEAD`. |
| `buckettie repo commit <id> <message>` | Stage all working-tree changes and create a local commit on the current policy-allowed branch. |
| `buckettie repo fetch\|pull\|push <id>` | Run the corresponding policy-checked Git operation through the MCP service. |
| `buckettie repo list` / MCP `list_projects` | List registered repository IDs to select the project name before operations. MCP clients must call `list_projects` before every push. |
| `buckettie repo register <id> <local-root> ...` | Enter the API Token in a topmost centered dialog, then register the repository through the MCP service. Direct MCP registration also collects a missing Token locally without exposing it in MCP arguments. Use `--console-token` to read the Token from the terminal without echo. |
| `buckettie repo unregister\|update ...` | Manage the repository allowlist and branch policy through the MCP service. |
| `buckettie branch list\|get\|create\|delete ...` | List, inspect, create, or delete Bitbucket branches. Creation requires an explicit source; develop, main, and protected branches cannot be deleted. |
| `buckettie pr list\|get\|diff\|create\|merge ...` | List, inspect, create, or merge Bitbucket pull requests. |
| `buckettie tag list\|get\|create\|delete\|push ...` | List, inspect, create, delete, or explicitly push policy-compliant Bitbucket tags. |
| `buckettie provider capabilities` | Print support flags for Repository Contract operations, including repository diff and commit. |
| `buckettie auth test` | Check that each repository credential is readable without printing it. |
| `buckettie auth set <id>` | Read a Token without echo and save it with DPAPI LocalMachine protection. |
| `buckettie auth delete <id>` | Delete the repository-scoped encrypted Token file. |
| `buckettie mcp status` | Perform an MCP initialize request. |
| `buckettie mcp tools` | Request the MCP tool list. |
| `buckettie mcp test` | Perform an MCP initialize smoke test. |
| `buckettie mcp version` | Print the running MCP server version. |
| `buckettie logs` | Print the audit-log directory. |
| `buckettie version` | Print the CLI assembly version. |

Exit code `0` means success, `1` means an operation or diagnostic failed, and `2` means invalid input or configuration. Run install, uninstall, start, stop, and restart from an elevated terminal. The service uses the fixed name `Buckettie`, automatic startup, `Buckettie.Server.exe` in the CLI binary directory, and `..\config\buckettie.json`.

The service runs as LocalSystem and reads machine-protected Tokens from `..\data\secrets`. Re-run `buckettie doctor` after starting the service.

Installation and normal operation are described in [INSTALLATION.md](INSTALLATION.md) and [OPERATIONS.md](OPERATIONS.md). For failures, see [TROUBLESHOOTING.md](TROUBLESHOOTING.md).

## Explicit branch source and partial status

`buckettie branch create <repository> <branch> <source>` requires the source branch name or full 40-character commit SHA. For example, `buckettie branch create example develop main` creates the first remote develop from main. There is no implicit source, abbreviated commit resolution, local checkout or automatic fetch. MCP `bitbucket_branch_create` requires `repository`, `branch`, `source`; existing calls without source must be updated. Whitespace and revision expressions return `branch_source_invalid`. Source lookup 404s return `branch_not_found` (branch) or `branch_source_not_found` (commit); these may also mean the reference/repository is not visible. The response includes `source`, `source_kind`, `source_hash` and the created branch's `target_hash`.

`repo status` still returns local branch/HEAD/working-tree state when configured remote-tracking refs are missing. `remote_develop_head`/`remote_main_head` may be null; `ahead`/`behind` are null when comparison against develop is unavailable. `comparison_reference`, `comparison_unavailable_reason` and `missing_remote_references` explain the result. Missing refs can mean not fetched, not necessarily absent on Bitbucket. Other errors still fail. Provider capabilities report `contract_version: 2`, `branch_source_required: true`, `repository_status_nullable: true`; Moyai callers must pass source unchanged and preserve nulls. See [ADR 0014](docs/adr/0014-explicit-branch-source-and-partial-status.md).
