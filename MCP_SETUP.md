# MCP Setup

[English](MCP_SETUP.md) | [日本語](MCP_SETUP.ja.md)

This guide connects Codex or Claude Code to the Buckettie Streamable HTTP MCP server. For Buckettie installation and repository configuration, see [INSTALLATION.md](INSTALLATION.md) and [CONFIG.md](CONFIG.md).

## Prerequisites

- Windows 10/11 or Windows Server
- An installed and configured Buckettie instance
- A running `Buckettie` Windows Service
- Codex or Claude Code on the same Windows machine as Buckettie
- At least one Repository ID configured in `buckettie.json`

Run the following commands from an elevated terminal before registering a client:

```powershell
<install-root>\bin\buckettie.exe config check
<install-root>\bin\buckettie.exe auth test
<install-root>\bin\buckettie.exe start
<install-root>\bin\buckettie.exe doctor
```

## Authentication and Environment

Buckettie does not require MCP client credentials, authorization headers, or environment variables. The MCP endpoint is bound to loopback, and requests with an `Origin` header are accepted only when the origin uses loopback and the configured MCP port.

Bitbucket credentials are separate from MCP client authentication. Register each repository token with `buckettie auth set <repository-id>`. Buckettie encrypts it with DPAPI LocalMachine and stores it under `<install-root>\data\secrets`; never put a Bitbucket token in a Codex or Claude Code MCP configuration.

The default endpoint is `http://127.0.0.1:45450/mcp`. If `mcp_port` or `mcp_path` changes in `buckettie.json`, update every client URL to the same value. Do not replace `127.0.0.1` with a LAN address or expose the endpoint through a public proxy.

## Start the Server

The MSI registers the fixed `Buckettie` Windows Service. Start and verify it from an elevated terminal:

```powershell
<install-root>\bin\buckettie.exe start
<install-root>\bin\buckettie.exe status
<install-root>\bin\buckettie.exe mcp test
```

For a ZIP installation, run `buckettie.exe service install` once before `start`. Service commands and exit codes are defined in [COMMANDS.md](COMMANDS.md).

## Register Clients

Use the server name `buckettie`, the Streamable HTTP transport, and the configured loopback URL. No authentication fields or credential environment variables are present because the MCP boundary does not use client authentication.

### Codex

Codex reads the user configuration from `~/.codex/config.toml`. A trusted project may instead use `<project-root>/.codex/config.toml`. Add this complete server entry:

```toml
[mcp_servers.buckettie]
url = "http://127.0.0.1:45450/mcp"
enabled = true
required = true
default_tools_approval_mode = "writes"
```

- `buckettie` is the server name shown by Codex.
- `url` must match Buckettie's `mcp_port` and `mcp_path`.
- No `bearer_token_env_var`, `http_headers`, or `env_http_headers` is configured because Buckettie accepts unauthenticated loopback MCP traffic only.
- `required = true` makes client startup report an unavailable Buckettie endpoint.
- `default_tools_approval_mode = "writes"` prompts for tools not marked read-only while allowing read-only discovery according to Codex policy.

Save the file, then restart the ChatGPT desktop app, Codex CLI, or IDE extension. These local Codex clients share the same host configuration. In Codex, use `/mcp` to confirm that `buckettie` is connected.

### Claude Code

For a project-shared configuration, create `<project-root>/.mcp.json` with this complete entry:

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

- `buckettie` is the server name shown by Claude Code.
- `type` must be `http`; `streamable-http` is also accepted by Claude Code.
- `url` must match Buckettie's `mcp_port` and `mcp_path`.
- No `headers` or environment variables are configured because Buckettie does not use MCP client credentials.

Claude Code requires approval before using a project-scoped `.mcp.json` server. Start Claude Code in the project and approve the trusted server when prompted. As a private user-scoped alternative, this command writes the equivalent HTTP registration to Claude Code's user configuration:

```powershell
claude mcp add --transport http --scope user buckettie http://127.0.0.1:45450/mcp
```

Restart or reload Claude Code after changing the configuration. Run `claude mcp get buckettie`, `claude mcp list`, or `/mcp` to inspect the connection.

## Multiple Workspaces

One Buckettie instance can serve multiple Bitbucket workspaces and repositories. Add every allowed repository under `repositories` in the same `buckettie.json`, using a unique Repository ID for each entry. Clients keep one `buckettie` MCP server registration and pass the intended Repository ID to each tool.

Use a user-scoped Codex or Claude Code registration when all local projects may access the same Buckettie allowlist. Use project-scoped configuration when access should be visible only from selected projects. Project scope does not override Buckettie's repository allowlist or branch policies.

## Verify the Connection

Verify the server before the clients:

```powershell
<install-root>\bin\buckettie.exe mcp status
<install-root>\bin\buckettie.exe mcp tools
<install-root>\bin\buckettie.exe doctor
```

The tool list must include `bitbucket_repository_status`. In Codex or Claude Code, confirm `buckettie` is connected, then call `bitbucket_repository_status` with a configured Repository ID. A successful result returns the local branch, HEAD, and working-tree status. Tool inputs and policy errors are defined in [COMMANDS.md](COMMANDS.md) and [SECURITY.md](SECURITY.md).

## Troubleshooting

- If the client reports connection refused, run `buckettie status`, `buckettie mcp test`, and `buckettie doctor`.
- If the client reports an HTTP or initialization error, confirm that its URL exactly matches `mcp_port` and `mcp_path`.
- If an `Origin` request receives HTTP 403, use `http://127.0.0.1:<mcp_port>` or `http://localhost:<mcp_port>` as the client origin and endpoint host.
- If Claude Code shows a project server as pending, trust the workspace and approve `.mcp.json` interactively.
- If a tool returns a repository error, confirm the exact case-sensitive Repository ID with `buckettie repo list` and run `buckettie repo status <repository-id>`.
- If Bitbucket authentication fails, run `buckettie auth test`; do not add the Bitbucket token to the MCP client configuration.
- After any client configuration change, restart or reload that client.

For additional recovery steps, see [TROUBLESHOOTING.md](TROUBLESHOOTING.md).

Client configuration references: [OpenAI Codex MCP documentation](https://developers.openai.com/codex/mcp) and [Claude Code MCP documentation](https://docs.anthropic.com/en/docs/claude-code/mcp).
