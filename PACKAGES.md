# Package Contract

Since version 1.1, the standard Windows artifact is `Buckettie-<version>-win-x64.msi` plus its SHA-256 file. A self-contained ZIP may be published for portable or manual installation. Since version 1.2, both use the `bin`, `config`, `logs`, and `data` layout.

| Package path | Content |
| :--- | :--- |
| `bin/buckettie.exe` | Management CLI |
| `bin/Buckettie.Server.exe` | MCP Windows Service |
| `bin/Buckettie.AskPass.exe` | Git authentication helper process |
| `bin/*.dll`, `*.deps.json`, `*.runtimeconfig.json` | Runtime dependencies |
| `config/buckettie.example.json` | Configuration template without secrets |
| `docs/*.md` | README, configuration, commands, operations, troubleshooting, and security documents |

Packages exclude environment-specific data:

- `config/buckettie.json`
- `data/` and DPAPI token files
- `logs/` and audit logs
- `.local/`, test results, and developer-machine data
- symbols and intermediate build output unless separately required

The release is a self-contained Windows x64 package. Record SHA-256 hashes and run `buckettie version`, `config check`, and `doctor` after deployment.

The MSI manages installation below `%ProgramFiles%\Buckettie` or `INSTALLROOT`, directory creation, Windows Service registration, major upgrades, and uninstall. It neither packages nor removes the effective configuration, application data, tokens, or audit logs.
