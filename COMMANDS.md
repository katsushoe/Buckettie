# Buckettie commands

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
| `buckettie auth test` | Check that each repository credential is readable without printing it. |
| `buckettie mcp status` | Perform an MCP initialize request. |
| `buckettie mcp tools` | Request the MCP tool list. |
| `buckettie mcp test` | Perform an MCP initialize smoke test. |
| `buckettie logs` | Print the audit-log directory. |
| `buckettie version` | Print the CLI assembly version. |

Exit code `0` means success, `1` means an operation or diagnostic failed, and `2` means invalid input or configuration. Run install, uninstall, start, stop, and restart from an elevated terminal. The service uses the fixed name `Buckettie`, automatic startup, `Buckettie.Server.exe` in the CLI binary directory, and `..\config\buckettie.json`.

After installation, open the service properties and set **Log On** to the Windows account that owns the `Buckettie/Bitbucket/<repository-id>` credentials before starting it. The CLI deliberately does not accept an account password because passing it through process arguments would expose the secret. Re-run `buckettie doctor` after starting the service.
