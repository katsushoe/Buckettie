# ADR 0009: Windows Service hosting and control

## Context

Buckettie must start automatically with Windows and expose fixed lifecycle commands without accepting arbitrary service names, executables, or command lines.

## Decision

Host `Buckettie.Server.exe` through the .NET Windows Service lifetime with the fixed service name `Buckettie`. The management CLI invokes `%SystemRoot%\System32\sc.exe` through a fixed command grammar. Installation always targets the server beside the CLI and the configuration in `..\config\buckettie.json`, with automatic startup.

## Alternatives

A scheduled task was rejected because it does not provide standard service lifecycle semantics. Accepting service names or binary paths as CLI input was rejected because it would create an arbitrary privileged process-registration boundary.

## Impact

Service mutation commands require an elevated terminal. Interactive execution remains supported because the Windows Service lifetime activates only when the process is launched by the Service Control Manager.

## Security

No arbitrary executable, service name, or extra server argument is accepted. Native stdout and stderr are never relayed to callers. The configuration path is not secret and the Token remains DPAPI-encrypted at rest.

## Operational considerations

The service runs as LocalSystem without an Account password and reads DPAPI LocalMachine-protected Tokens through its explicit directory ACL. Installation checks that both fixed release files exist. Service Control Manager exit failures produce a stable nonzero CLI result.

## Implementation

`AddWindowsService` integrates host lifetime and service naming. `WindowsServiceManager` builds fixed `sc.exe` argument lists, while an injected executor allows unit testing without modifying the machine service registry.
