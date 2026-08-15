# Buckettie commands

The management executable is `buckettie.exe`. Its default configuration is `..\config\buckettie.json` relative to the executable directory. Override it with `--config <path>`.

| Command | Purpose |
| --- | --- |
| `buckettie doctor` | Diagnose configuration, Git, credentials, local repositories, Bitbucket API, and MCP endpoint. |
| `buckettie config check` | Validate the strict configuration contract. |
| `buckettie config show` | Print effective non-secret configuration. |
| `buckettie repo list` | List configured Repository IDs. |
| `buckettie repo status <id>` | Validate and show local repository status. |
| `buckettie auth test` | Check that each repository credential is readable without printing it. |
| `buckettie mcp status` | Perform an MCP initialize request. |
| `buckettie mcp tools` | Request the MCP tool list. |
| `buckettie mcp test` | Perform an MCP initialize smoke test. |
| `buckettie logs` | Print the audit-log directory. |
| `buckettie version` | Print the CLI assembly version. |

Exit code `0` means success, `1` means a diagnostic failed, and `2` means invalid input or configuration. Service lifecycle commands are delivered with Windows Service support.
