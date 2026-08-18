# Troubleshooting

Start with `buckettie status`, `buckettie doctor`, and `buckettie logs`. Exit codes are `0` for success, `1` for an operation or diagnostic failure, and `2` for invalid input or configuration.

## Service Does Not Start

- Run `service status` as an administrator.
- Resolve every `config check` error.
- Check the layout and access rights for `bin`, `config`, `logs`, and `data`.
- Inspect Service Control Manager errors in the Windows Event Viewer System log.

## Token or Authentication Failure

- Run `auth test` and inspect each Repository ID result.
- Rotate the token with `auth set <repository-id>`.
- Check token permissions, expiry, revocation, and workspace.
- Re-register a token copied from another machine; DPAPI files are machine-bound.
- Never put token values in logs, issues, or screenshots.

## MCP Connection Failure

- Confirm that the service is running.
- Run `mcp status` and `mcp test`.
- Match the client URL to `mcp_port` and `mcp_path`.
- Connect only from the same machine; the endpoint is loopback-only.

## Git Operation Failure

- Run `repo status <repository-id>` and inspect local path, remote URL, branch, and working tree.
- Clean uncommitted changes when `require_clean_working_tree` applies.
- Match the remote URL to the configured workspace and slug.
- Treat push result `nothing_to_push` as success when no commits differ.
- Check branch allowlists and protected branches in [Configuration](CONFIG.md).

## Bitbucket API Failure

- Run `doctor` to verify API connectivity and authentication.
- Check workspace, slug, and Repository ID selection.
- Check pull-request state, source and destination branches, and tag pattern.
- Share only the fixed audit-log error classification; omit secrets.
