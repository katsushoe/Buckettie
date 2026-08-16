# ADR 0010: DPAPI LocalMachine Token Store

## Context

Windows Credential Manager isolates credentials by user. A LocalSystem Windows Service therefore cannot read Tokens registered by the interactive operator.

## Decision

Use one DPAPI LocalMachine-encrypted file per Repository ID under the release `data/secrets` directory. Disable ACL inheritance and grant full control only to LocalSystem, Administrators, and the operator that creates the directory. Runtime code uses only `DpapiFileTokenStore`.

## Alternatives

Running the service as an interactive user was rejected because SCM requires a reusable Account password. Plain files, environment variables, and command-line Token arguments were rejected because they expose the secret. Credential Manager was superseded because its user boundary conflicts with LocalSystem operation.

## Impact

Encrypted files are bound to the Windows machine, not a user profile. Moving the files to another machine does not constitute a usable backup. Local administrators remain trusted, as they are for the service installation and binaries.

## Security

Token input is non-echoing. Clear and protected temporary byte arrays are zeroed. Writes use a temporary file followed by atomic replacement. Repository IDs are validated before path construction. Token values, blobs, authorization headers, and paths are never logged.

## Operational considerations

Use `buckettie auth set <repository>` from an elevated terminal to create or replace a Token and `auth delete` to remove it. LocalSystem can decrypt the file; other ordinary users cannot read it.

## Implementation

`DpapiFileTokenStore` owns validation, DPAPI calls, atomic files, and ACL application behind testable boundaries. Server composition and Git AskPass resolve `..\data\secrets` relative to the binary directory.
