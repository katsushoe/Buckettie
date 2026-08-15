# ADR 0003: Credential Manager-backed Git AskPass

- Status: Accepted

## Context

Git HTTPS needs a username and API Token without embedding the Token in Remote URLs, arguments, configuration, logs, or plaintext temporary files.

## Decision

Use a dedicated `Buckettie.AskPass` executable. The Git parent process supplies only Repository ID and Atlassian email as environment variables. The helper reads the Token directly from Windows Credential Manager and writes the requested username or password to standard output according to the AskPass protocol.

## Alternatives

- Token environment variable: rejected because child process environments are an unnecessary secret exposure surface.
- Temporary script containing the Token: rejected because plaintext can remain on disk or be captured before deletion.
- Token in Remote URL: rejected because Git configuration, process diagnostics, and logs may persist it.

## Impact

Deployment must keep the AskPass executable beside the host or provide its trusted absolute path. Git command integration must set `GIT_ASKPASS_REQUIRE=force`, disable terminal prompts, clear inherited Git override variables, and use HTTPS Remotes only.

## Security conditions

- Pass no Token through environment variables, files, arguments, or logs.
- Accept only Username and Password prompts under stable `LC_ALL=C` Git output.
- Run AskPass as the same Windows user that owns the Credential Manager entry.
- Do not log AskPass standard output.

## Operational conditions

Atlassian email is non-secret configuration. A missing credential makes AskPass exit nonzero. The helper is noninteractive and writes no diagnostic detail containing credentials.

## Implementation, tests, and documentation

Application owns prompt interpretation and protocol constants. The executable is a composition root using `WindowsCredentialManagerTokenStore`. Unit tests use a fake Token Store; live Git integration follows when the environment is connected to the Git process runner.
