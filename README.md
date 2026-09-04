# Buckettie

[English](README.md) | [日本語](README.ja.md)

Buckettie is a Windows gateway that lets MCP clients operate explicitly allowed local Git repositories and Bitbucket Cloud repositories. Repository allowlists, branch protection, audit logs, and DPAPI-protected API tokens expose only the operations an AI client needs.

Current release: `1.3.23.0`

Bitbucket Release lifecycle semantics are documented in [docs/bitbucket-release-provider.md](docs/bitbucket-release-provider.md).

## Getting Started

1. Install the MSI package or extract the binary archive.
2. For an MSI installation, edit the generated `<install-root>\config\buckettie.json`; for ZIP, copy `buckettie.example.json` to that path first.
3. Register its API token, start the service, and run diagnostics.
4. Register `http://127.0.0.1:45450/mcp` in the MCP client.

See [MCP Setup](MCP_SETUP.md) for complete Codex and Claude Code registration instructions.

## Installation

### Installer distribution (recommended)

Download `Buckettie-<version>-win-x64.msi` and its SHA-256 file from the release. Verify the hash, then run the MSI as an administrator. The default installation root is `C:\Buckettie`, and the installer adds `C:\Buckettie\bin` to the system `PATH`.

```powershell
msiexec.exe /i Buckettie-<version>-win-x64.msi INSTALLROOT="D:\Buckettie"
```

### Binary archive

Download and verify `Buckettie-<version>-win-x64.zip`, then extract it to a permanent `<install-root>`. The archive is self-contained. Run `service install` after configuration.

### Source build

Install the .NET 9 SDK and Git for Windows, then run:

```powershell
git clone https://github.com/katsushoe/Buckettie.git
Set-Location Buckettie
dotnet test Buckettie.slnx -c Release
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Build-Msi.ps1
```

The MSI is written below `.local\installer\output`. See [Installation](INSTALLATION.md) and [Package Contract](PACKAGES.md).

## Configuration

The standard layout is `<install-root>\bin`, `config`, `logs`, and `data`. DPAPI-encrypted tokens are stored below `<install-root>\data\secrets`.

```powershell
<install-root>\bin\buckettie.exe config check
<install-root>\bin\buckettie.exe auth set <repository-id>
<install-root>\bin\buckettie.exe start
<install-root>\bin\buckettie.exe doctor
```

For a ZIP deployment, run `service install` once before `start`. See [Configuration](CONFIG.md) for every setting and its constraints.

`repo register` opens a topmost centered Token dialog by default. Use `--console-token` only when terminal input is required.
Repository IDs must match the Itoguruma Project Inbox ID rule `^[a-z][a-z0-9]*$`. Repository lookup is case-insensitive.

## Usage

- Inspect repository state and run `fetch`, `pull`, or an allowed `push`.
- List, inspect, create, and merge pull requests.
- Query Repository Contract capabilities; inspect working-tree diffs; create local commits; create or delete branches.
- List, create, delete, and explicitly push tags.
- Restrict Git and Bitbucket API operations by repository and branch.

See [Commands](COMMANDS.md) for the CLI and MCP tool contracts.

## Development Motivation

Direct Bitbucket access from Claude Code or Codex can trigger an AI client's security checks for external communication and credential use. Buckettie centralizes Bitbucket communication and authentication in a fixed localhost gateway and exposes only allowlisted MCP tools. This preserves the security boundary while reducing direct external access, secret handling by clients, and repeated confirmations.

## Documentation

- [MCP Setup](MCP_SETUP.md) / [MCPセットアップ](MCP_SETUP.ja.md)
- [Configuration](CONFIG.md) / [設定](CONFIG.ja.md)
- [Commands](COMMANDS.md) / [コマンド](COMMANDS.ja.md)
- [Installation](INSTALLATION.md)
- [Operations](OPERATIONS.md)
- [Troubleshooting](TROUBLESHOOTING.md)
- [Security](SECURITY.md)
- [Package Contract](PACKAGES.md)
- [Document Index](DOCUMENTS.md) / [文書一覧](DOCUMENTS.ja.md)

## Security

The MCP endpoint listens only on loopback. Do not store API tokens in configuration files or MCP client settings. See [Security](SECURITY.md) for the trust boundaries and vulnerability-reporting guidance.

## License

Buckettie is licensed under the [MIT License](LICENSE).
