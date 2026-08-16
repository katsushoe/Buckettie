# Installation

## MSI (Recommended)

Run `Buckettie-<version>-win-x64.msi` as an administrator. It installs the self-contained binaries, configuration template, and documentation below `%ProgramFiles%\Buckettie` by default and registers the `Buckettie` Windows Service. The service remains stopped until configuration and token registration are complete. Set `INSTALLROOT` for another root.

```powershell
msiexec.exe /i Buckettie-<version>-win-x64.msi INSTALLROOT="F:\Buckettie"
```

Before migrating a manual installation, stop and unregister its service, then back up configuration, tokens, and audit logs.

## Binary Archive Layout

Extract the archive to a permanent `<install-root>`:

```text
<install-root>\
  bin\       executables and runtime dependencies
  config\    buckettie.json
  logs\      audit logs created at runtime
  data\      application data
    secrets\ DPAPI-encrypted tokens
```

## Configuration

Copy `buckettie.example.json` to `<install-root>\config\buckettie.json` and edit it according to [Configuration](CONFIG.md). The `repositories` key is the Repository ID used by commands. `slug` is the repository name at the end of its Bitbucket URL.

```powershell
<install-root>\bin\buckettie.exe config check
<install-root>\bin\buckettie.exe config show
```

Use `--config <path>` for a nonstandard location.

## Token Registration

Run from an elevated terminal for each repository. Input is hidden.

```powershell
<install-root>\bin\buckettie.exe auth set <repository-id>
<install-root>\bin\buckettie.exe auth test
```

The token is DPAPI LocalMachine-encrypted at `<install-root>\data\secrets\<repository-id>.token`; it is never stored in configuration or environment variables. When migrating from version 1.1 or earlier, stop the service, preserve ACLs while moving `<install-root>\secrets` to `<install-root>\data\secrets`, and run `doctor`.

## Service Registration and Verification

For an MSI deployment, omit `service install` because the MSI registers the service.

```powershell
<install-root>\bin\buckettie.exe service install
<install-root>\bin\buckettie.exe start
<install-root>\bin\buckettie.exe status
<install-root>\bin\buckettie.exe doctor
```

The service name is `Buckettie`, its account is LocalSystem, and its startup type is automatic. The default MCP endpoint is `http://127.0.0.1:45450/mcp`.

## Uninstall

```powershell
<install-root>\bin\buckettie.exe stop
<install-root>\bin\buckettie.exe service uninstall
```

This removes only service registration. Manage configuration, audit logs, and token files separately after confirming whether they are still needed.
