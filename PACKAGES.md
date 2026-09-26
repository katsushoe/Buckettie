# Package Contract

Provider authentication depends on the vendored `Moyai.ProviderAuthentication` 1.0.2 NuGet package
in `packages/` (SHA-256 recorded alongside it). Restore through `NuGet.Config`; its runtime assemblies
must accompany the server. See [authentication setup](docs/provider-authentication.md) before deployment.

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

The build scripts publish each application into an isolated directory, then merge their files with the CLI last so its server dependencies are not overwritten by older framework assemblies from the GUI or helper. They run the published CLI's `version` command and verify the requested display version before creating an artifact.

The MSI manages installation below `C:\Buckettie` or `INSTALLROOT`, system `PATH` registration for the installed `bin` directory, directory creation, Windows Service registration, major upgrades, and uninstall. It neither packages nor removes the effective configuration, application data, tokens, or audit logs.
