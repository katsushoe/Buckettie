# ADR 0011: Windows MSI installer

## Status

Accepted

## Context

The ZIP distribution requires users to place binaries, create directories, and register the Windows Service separately. A Windows product needs standard installation, upgrade, and uninstall paths.

## Decision

Generate a per-machine Windows x64 MSI with WiX Toolset 6. The default root is `Buckettie` below `ProgramFiles64Folder`; public directory property `INSTALLROOT` selects another root. The MSI manages self-contained binaries, the configuration template, user documents, `logs` and `data` directories, and LocalSystem registration of the `Buckettie` service. It does not start the service before initial configuration and DPAPI token registration are complete.

Preserve configuration, tokens, and audit logs during upgrades and uninstall. Never include secrets in the MSI.

## Alternatives

- ZIP only: rejected because it does not remove manual layout and service-registration work.
- MSIX: rejected because it adds constraints to the Windows Service and existing per-machine operation.
- Custom setup executable: rejected because it expands maintenance and the security boundary.

## Impact

- Building the MSI requires NuGet restore of the WiX Toolset SDK.
- Windows Installer uses a three-part ProductVersion while the displayed product version uses four parts.
- Before installing over a manual deployment, unregister the old service to avoid a service-name conflict.

## Security conditions

- Do not include API tokens, effective configuration, or audit logs in the MSI.
- Register the service as LocalSystem and keep MCP restricted to localhost.
- `auth set` applies the final ACL to `data/secrets`.

## Operations

The build script runs self-contained publish, MSI generation, and SHA-256 generation together. MSI installation, repair, and uninstall require administrator privileges.

## Implementation and verification

Maintain MSI source, the build script, package documentation, and installation documentation in the same change. Verify build, tests, MSI database inspection, and payload extraction through an administrative installation.
