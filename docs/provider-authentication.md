# Provider authentication configuration

See [ADR 0016](adr/0016-provider-assertion-authentication.md) for the security and trust decisions.

The pinned authentication package is 1.0.2. Immediately before invoking each repository gateway,
the server rechecks expiry, current key status and capability without reserving replay a second time.
Initial authentication failures return HTTP 401. Failures after MCP dispatch return a tool result
with `isError: true` and a stable authentication error code in the text; no repository gateway is invoked.

`buckettie.json` accepts `provider_authentication` with these required fields:

- `issuer`: the configured Moyai instance ID (`moyai:...`).
- `trust_bundle_path`: an absolute path to the administrator-protected public trust JSON array.
- `replay_database_path`: an absolute path to the persistent SQLite replay DB shared by all consumers.
- `bindings`: an array of objects with `repository` (existing Buckettie ID), `project_id` (existing Moyai UUID),
  and `normalized_repository` (`bitbucket.org/<registered-workspace>/<registered-slug>`).

Obtain the UUID from Moyai's project query; do not generate a new UUID in Buckettie or copy it from a JWT.
Create/update/remove bindings in protected configuration and restart after validation. Register the
repository through the authenticated administrative endpoint before adding its binding. Removing a
repository denies its bound operations immediately; remove the obsolete binding from configuration too.
Re-registration with a different workspace/slug cannot reuse the old binding.

Trust JSON is an array of public entries with `issuer`, `kid`, `algorithm` (`ES256`), `x`, `y`,
`not_before_utc`, `not_after_utc`, and `status` (`active`/`retiring` or a non-accepting key state).
Use the public material supplied by Moyai. Never include private JWK components.

An optional `administrator_endpoint` object enables management via mutually authenticated HTTPS:

- `port`: a different loopback port from `mcp_port`.
- `server_certificate_thumbprint`: the exact 40-character thumbprint in LocalMachine/My with its private key.
- `client_certificate_thumbprints`: explicit allowed client certificate thumbprints.
- `cli_certificate_thumbprint`: the client certificate in CurrentUser/My for CLI management commands.

Configure certificate private-key ACLs separately for the service account and administrator. Certificates
must be within their validity periods. The server and CLI pin configured peers; a caller-selected
thumbprint or a certificate on the ordinary HTTP port cannot grant administration.
The CLI routes list/register/update/unregister to this endpoint automatically when a CLI certificate is configured and never sends the private key. Otherwise it uses the ordinary loopback port: `list_projects` is a direct read, and register/update/unregister are accepted from loopback without an `Authorization` header only because each change requires interactive desktop approval before it is written. Requests carrying an assertion or arriving from a non-loopback address still need the administrator endpoint.
Standard MCP clients may use the HTTPS management endpoint if configured to supply a matching client
certificate. A failed certificate or assertion never falls back to the loopback approval path.

Direct read access: a `tools/call` without an `Authorization` header that arrives from a loopback address on
`mcp_port` may invoke `list_projects` and any tool whose scope mapping is exactly `repository.read`
(status, diff, branch/tag/pull-request/release reads, history rewrite preview). This follows the Moyai
consumer contract section "既存Tokenと直接接続" (2026-09-24) and works even when `provider_authentication` is
not configured. A request that presents an assertion is always fully validated and never falls back to this
direct-read allowance. The gateway also refuses any non-read operation during a direct-read request.

In Moyai integration mode, all other repository calls, including `bitbucket_fetch`, `bitbucket_pull` and
every change tool, need an operation-specific Moyai assertion even from direct Codex/Claude/CLI connections. These clients must use Moyai
for such operations; there is no CLI token argument or copied assertion workflow. Administrative certificates
cannot authorize push, commit or release tools.

Moyai sends the assertion only in `Authorization: Bearer ...` on `tools/call`, together with the public
`X-Moyai-Operation-Id` (1–200 ASCII letters/digits, `-`, `_`, `.`). It must use the exact scope set advertised
in `bitbucket_provider_capabilities.data.authentication.tool_scopes`. JWT claims, signatures and tokens
are never MCP arguments or output fields. Successful authenticated calls echo the public operation ID
header. Authentication failures use HTTP 401 with JSON-RPC error `-32001`, a stable error identifier in
`error.data.code`, and `retryable: false`; no alternative route or automatic retry is used.

Moyai integration is optional: Buckettie is a Moyai sub-tool but never presupposes Moyai. The server runs in
standalone mode by default. In standalone mode, `provider_authentication` is ignored even when present, and
direct loopback calls on `mcp_port` without an `Authorization` header may use every repository tool, including
fetch, pull and change tools, under the allowlist, branch policies and audit log. In standalone mode an
`Authorization` header on a loopback request (for example a Moyai assertion) is removed and ignored: it can
grant nothing beyond a header-less loopback call, and Moyai itself refuses to delegate state-changing
operations unless `integration_mode` is `moyai`. Non-loopback requests and requests on another port are still
rejected.

MCP traffic that runs no tool passes the boundary in both modes: `initialize`, `server/discover` (sent first by
MCP C# SDK 2.2+ clients such as Moyai), `ping`, `tools/list`, `prompts/list`, `prompts/get`, `resources/list`,
`resources/templates/list`, client `notifications/*`, and Streamable HTTP `GET`/`DELETE` session requests. Any
other JSON-RPC method is rejected with HTTP 401 and `mcp_method_not_allowed`.

Starting the server with `--moyai` (`Buckettie.Server.exe <config> --moyai`) enables Moyai integration mode,
where the direct-read and assertion rules above apply. In this mode a missing or invalid
`provider_authentication` stops startup instead of falling back to standalone mode. The startup log records
`integration_mode`, and `bitbucket_provider_capabilities.data.authentication.integration_mode` reports
`standalone` or `moyai` so Moyai can refuse a Buckettie that is not in integration mode. To run standalone
temporarily, stop the service and start `Buckettie.Server.exe` without `--moyai` from an administrator
console; ending that process ends standalone mode. For a Moyai-managed service, add `--moyai` to the service
arguments (for example with `sc.exe config Buckettie binPath= ...`); an MSI upgrade currently rewrites the
service arguments, so re-apply the option after upgrading. Existing Bitbucket API tokens remain in their
current provider-owned store.

For local reproduction, restore with the repository NuGet.Config and run the Server and CLI tests.
Integration uses a separate loopback port, disposable issuer keys, separate trust/repository DBs, one shared
replay DB, two project UUIDs and distinct Githubie/Buckettie audiences. No production remote writes or
service installation are authorized by the CR. The peer must support the advertised scope set and error
contract before production deployment.
