# MCP Setup

[English](MCP_SETUP.md) | [日本語](MCP_SETUP.ja.md)

This guide separates values you enter, files a client generates, and results you verify. Follow one recommended path for each client. Alternative project-scoped configuration is in a separate section.

## Prerequisites

- Buckettie is installed and configured on the same Windows machine as the MCP client.
- The `Buckettie` Windows Service is registered.
- At least one repository is present in `buckettie.json`.
- Codex or Claude Code is installed.

Installation and repository configuration are documented in [INSTALLATION.md](INSTALLATION.md) and [CONFIG.md](CONFIG.md).

### Values you enter

You enter only the following two machine-specific values in the preparation commands:

| Variable | What to enter | Example |
| --- | --- | --- |
| `$BuckettieRoot` | The installation directory selected by the MSI or ZIP deployment | `F:\Buckettie` |
| `$RepositoryId` | One case-sensitive ID printed by `repo list` | `my-project` |

The MCP server name and default URL are fixed in the client examples:

| Setting | Value | Change only when |
| --- | --- | --- |
| Server name | `buckettie` | You intentionally need a different client display name. |
| Transport | Streamable HTTP (`http`) | Do not change it. Buckettie does not expose stdio or SSE. |
| URL | `http://127.0.0.1:45450/mcp` | `mcp_port` or `mcp_path` differs in `buckettie.json`. |
| MCP authentication | None | Do not add a token or authorization header. |

`<install-root>`, `<project-root>`, and `<repository-id>` are notation only. Do not paste angle-bracket placeholders into a terminal or configuration file.

## Authentication and Environment

Buckettie does not require MCP client credentials, authorization headers, or environment variables. It accepts MCP traffic only on loopback and restricts an `Origin` header to loopback on the configured port.

Bitbucket authentication is separate. `auth set` encrypts the Bitbucket token with DPAPI LocalMachine and writes it below `$BuckettieRoot\data\secrets`. Never put that token in Codex or Claude Code MCP settings.

## Start the Server

### Step 1: enter your installation directory

Run this in an elevated PowerShell terminal. Change only the quoted path:

```powershell
$BuckettieRoot = 'F:\Buckettie'
```

### Step 2: verify the configuration and find a Repository ID

```powershell
& "$BuckettieRoot\bin\buckettie.exe" config check
& "$BuckettieRoot\bin\buckettie.exe" repo list
```

Expected results:

- `config check` exits with code `0` and reports no configuration error.
- `repo list` prints one case-sensitive Repository ID per line.

### Step 3: enter one printed Repository ID and verify it

Change only the quoted ID:

```powershell
$RepositoryId = 'my-project'
& "$BuckettieRoot\bin\buckettie.exe" repo status $RepositoryId
& "$BuckettieRoot\bin\buckettie.exe" auth test
```

Expected results:

- `repo status` prints the branch, HEAD, and working-tree status.
- `auth test` reports that the configured repository credential is readable without printing the token.

If the token is not registered, run `& "$BuckettieRoot\bin\buckettie.exe" auth set $RepositoryId` first. The token entered at the hidden prompt is stored by Buckettie; it is not copied into an MCP client setting.

### Step 4: start and test the service

```powershell
& "$BuckettieRoot\bin\buckettie.exe" start
& "$BuckettieRoot\bin\buckettie.exe" status
& "$BuckettieRoot\bin\buckettie.exe" mcp test
```

Expected results:

- `status` reports the `Buckettie` service as running.
- `mcp test` prints `[OK] MCP Endpoint (200)`.

For a ZIP deployment, run `& "$BuckettieRoot\bin\buckettie.exe" service install` once before `start`. The MSI registers the service automatically.

## Register Clients

### Codex — recommended manual user configuration

This procedure does not run a generator. You add one TOML table yourself.

1. Open `%USERPROFILE%\.codex\config.toml`.
2. Preserve all existing content.
3. Add the following table once. If `[mcp_servers.buckettie]` already exists, replace only that table.

```toml
[mcp_servers.buckettie]
url = "http://127.0.0.1:45450/mcp"
enabled = true
required = true
default_tools_approval_mode = "writes"
```

What is generated automatically: nothing. Saving `config.toml` is the configuration change.

What each value means:

- `buckettie`: client-visible server name.
- `url`: must equal Buckettie's configured loopback endpoint.
- `enabled = true`: enables the server.
- `required = true`: reports an unavailable endpoint as a client startup problem.
- `default_tools_approval_mode = "writes"`: prompts according to Codex policy for tools not marked read-only.
- No bearer-token or header setting is present because MCP authentication is `None`.

Restart the ChatGPT desktop app, Codex CLI, or IDE extension after saving. These local Codex clients share the host configuration.

### Claude Code — recommended automatic user registration

Run exactly this command in PowerShell. There are no placeholders to replace when Buckettie uses the default URL:

```powershell
claude mcp add --transport http --scope user buckettie http://127.0.0.1:45450/mcp
```

The command automatically updates Claude Code's user configuration at `~/.claude.json`. It adds the logical server entry below while preserving other settings:

```json
{
  "mcpServers": {
    "buckettie": {
      "type": "http",
      "url": "http://127.0.0.1:45450/mcp"
    }
  }
}
```

Do not paste this JSON after running the command. It shows what the command registers so that you can verify the result. Restart or reload Claude Code after registration.

### Alternative: project-scoped configuration

Use this alternative only when the MCP registration should apply to one project rather than all local projects.

- Codex: put the same TOML table in `<project-root>\.codex\config.toml` instead of the user file.
- Claude Code: create `<project-root>\.mcp.json` containing the JSON example above. Do not also run the user-scope command unless both scopes are intentionally required.

`<project-root>` means the root directory of the source repository in which the client runs. Project-scoped Claude Code configuration requires interactive workspace trust and MCP server approval.

## Multiple Workspaces

One Buckettie service can expose multiple allowed Bitbucket workspaces and repositories. Add them to `repositories` in `buckettie.json` with unique Repository IDs. Keep one client registration and pass the intended Repository ID to each Buckettie tool.

Client scope controls where the client can see the server registration. It does not change Buckettie's repository allowlist, branch policy, or credentials.

## Verify the Connection

Perform these checks in order. Stop at the first failure.

### Check 1: Buckettie endpoint

```powershell
& "$BuckettieRoot\bin\buckettie.exe" mcp status
```

Pass condition: `[OK] MCP Endpoint (200)`.

### Check 2: Buckettie tool discovery

```powershell
& "$BuckettieRoot\bin\buckettie.exe" mcp tools
```

Pass conditions: `[OK] MCP Tools (200)` and the response contains `bitbucket_repository_status`.

### Check 3: client registration

- Codex: open `/mcp` and confirm that `buckettie` is enabled and connected.
- Claude Code: run `claude mcp get buckettie`, then `claude mcp list`; confirm a connected status.

### Check 4: read-only tool call

In the client, call `bitbucket_repository_status` with the exact value stored in `$RepositoryId`.

Pass condition: the result contains the configured repository's branch, HEAD, and working-tree status. This confirms client registration, MCP transport, Repository ID selection, and Buckettie policy resolution without modifying the repository.

### Check 5: complete diagnosis

```powershell
& "$BuckettieRoot\bin\buckettie.exe" doctor
```

Pass condition: configuration, Git, API token, repository, Bitbucket API, and MCP checks are all `OK`.

## Troubleshooting

- Command contains `<...>`: replace the placeholder; never paste angle brackets literally.
- `repo status` fails: use the exact case-sensitive ID printed by `repo list`.
- Connection refused: confirm the service is running, then repeat Checks 1 and 2.
- HTTP 404 or initialization failure: make the client URL match `mcp_port` and `mcp_path`.
- HTTP 403 with `Origin`: use `127.0.0.1` or `localhost` with the configured port.
- Codex has no `buckettie` entry: confirm the table is in `%USERPROFILE%\.codex\config.toml` and restart the client.
- Claude Code has no `buckettie` entry: rerun `claude mcp add`, then inspect it with `claude mcp get buckettie`.
- Claude Code shows pending approval: trust the project and approve its project-scoped `.mcp.json` entry.
- Bitbucket authentication fails: run `auth test` or `auth set`; do not add the token to MCP client settings.

For command details and recovery procedures, see [COMMANDS.md](COMMANDS.md) and [TROUBLESHOOTING.md](TROUBLESHOOTING.md).

Client references: [OpenAI Codex MCP documentation](https://developers.openai.com/codex/mcp) and [Claude Code MCP documentation](https://docs.anthropic.com/en/docs/claude-code/mcp).
