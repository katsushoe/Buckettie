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
| `buckettie repo fetch\|pull\|push <id>` | Run the corresponding policy-checked Git operation through the MCP service. |
| `buckettie repo register\|unregister\|update ...` | Manage the repository allowlist and branch policy through the MCP service. |
| `buckettie branch list <id>` / `branch get <id> <branch>` | List or get Bitbucket branches. |
| `buckettie pr list\|get\|diff\|create\|merge ...` | List, inspect, create, or merge Bitbucket pull requests. |
| `buckettie tag list\|get\|create ...` | List, inspect, or create policy-compliant Bitbucket tags. |
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
