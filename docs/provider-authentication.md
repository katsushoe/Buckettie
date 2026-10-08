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

## Trust Bundle placement and key rotation

These are operator procedures, not automatic configuration changes. Obtain approval for the exact
production files, key transition and any restart before applying them. Moyai owns the signing key;
Buckettie receives only its public trust entries.

### Placement and reload behavior

- Keep the existing administrator-protected public trust file unless an approved deployment plan
  requires moving it. The filename is not a protocol requirement: `trust_bundle_path` selects the file.
  Use an absolute path in the installation's configuration directory, outside binaries and temporary
  test fixtures. Grant read access to the service account and write access only to authorized administrators.
- Protect `buckettie.json`, the trust file and its parent directory. Keep the persistent replay database
  and its write permissions available to the service account; all consumers in one deployment share it.
  Moving the trust file does not require moving or resetting the replay database.
- Replacing public entries at the same configured path takes effect on the next validation, including
  the check immediately before gateway execution. No restart is needed for a content-only key update.
  Changing `issuer`, `trust_bundle_path`, `replay_database_path` or JSON bindings requires validation and
  a restart. Registry bindings remain subject to their separate live-update rules.
- Prepare a complete UTF-8 JSON array beside the destination, validate its structure and public material,
  then replace the destination atomically on the same volume. Preserve the destination ACL and verify
  its final hash, permissions and service readability. Do not truncate the live file while writing it.
  Missing, malformed or unreadable trust denies asserted requests; it does not enable a fallback route.

### Planned rotation with the same issuer

1. Record the issuer, old/new `kid`, public-key fingerprints, UTC validity windows, affected consumers,
   rollback file and success criteria. Keep private keys, assertions and tokens out of the record.
2. Ask the Moyai owner for a new ES256 public entry with a new `kid`. Verify the issuer and public `x`/`y`,
   validity window and status; do not reuse a `kid` for different key material.
3. Publish a complete bundle that retains the old accepted key and adds the new `active` key to every
   affected consumer before Moyai changes its signer. Keep issuer, bindings and replay history unchanged.
4. Have the Moyai owner switch signing to the new key. Use fresh operation-specific assertions for a
   non-mutating signed read through each consumer; a header-less direct read does not verify this change.
   Record only public operation IDs, error codes and results. Verify that old assertions remain rejected
   as replays and that consumers agree on the new key.
5. After the signer has stopped using the old key, keep its public entry accepted for the maximum
   remaining assertion validity plus allowed clock skew. `retiring` is still an accepting state within
   its validity window; it is not revocation. Then set the old entry to `revoked` or remove it and verify
   rejection of a fresh old-key assertion in an approved isolated test. Never reopen the production old
   signer merely to generate a test assertion.

Changing issuer is a separate coordinated configuration migration and requires a restart; adding a new
issuer entry alone does not change the configured issuer.

### Emergency revocation and recovery

- For a compromised key, revoke it immediately in all affected bundles and have the Moyai owner stop
  using it. Do not wait for the planned overlap period. Restore service with a new approved key.
  `auth_key_revoked` identifies a retained revoked entry; removing an entry can produce a different key
  lookup error. Rejection prevents subsequent validation/gateway entry, not actions already executed.
- If an ordinary rotation fails, keep the replay DB intact and restore only a validated last-known-good
  public bundle whose keys are still trusted and valid. Coordinate the signer state with Moyai. Never
  restore a compromised/revoked key or roll replay history back to make a retry succeed.
- If a path change fails, restore the previously approved configuration/path and valid public bundle,
  then restart and repeat fresh signed-read checks. Do not delete the replay DB or disable assertions.
  A restart does not repair an expired, revoked, malformed or mismatched key.

The procedure is complete only after all affected consumers accept the new signer, the old-key rejection
check passes, protected paths/ACLs are verified and public evidence is recorded. Document preparation
alone does not mean production rotation or deployment has occurred.

## Administrative endpoint and access modes

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
console; ending that process ends standalone mode. Existing Bitbucket API tokens remain in their current
provider-owned store.

`--direct-unrestricted` (valid only together with `--moyai`) implements the Moyai Consumer Contract setting
`direct_connection: unrestricted`. `integration_mode` stays `moyai`, so Moyai keeps delegating to it, and every
request carrying `Authorization` is still fully validated as a Moyai assertion; a failed assertion never falls
back to direct access. Header-less loopback calls on `mcp_port` may then use every repository tool, as in
standalone mode, which means any local process can change repositories without Moyai (accepted by the user on
2026-09-27). `bitbucket_provider_capabilities.data.authentication.direct_connection` reports `read_only`
(`--moyai` only) or `unrestricted` (standalone or `--direct-unrestricted`), and the startup log records it.

The MSI sets the service options through the properties `MOYAI=1` (adds `--moyai`) and `DIRECT_UNRESTRICTED=1`
(adds `--direct-unrestricted`, ignored without `MOYAI=1`). Both values are remembered under
`HKLM\SOFTWARE\Akatsukisoft\Buckettie`, so an upgrade keeps them; pass `MOYAI=0` or `DIRECT_UNRESTRICTED=0`
to turn an option off. For example:
`msiexec /i Buckettie-<version>-win-x64.msi MOYAI=1 DIRECT_UNRESTRICTED=1`.

For local reproduction, restore with the repository NuGet.Config and run the Server and CLI tests.
Repository registration accepts `--moyai-project-id <UUID>`; an existing registration can be bound with
`buckettie repo update <id> --moyai-project-id <UUID>` or cleared with `--remove-moyai-project-id`.
The approval dialog displays the binding change. The canonical repository comes from the validated
Git remote, not a caller-supplied assertion claim. A configured JSON binding takes precedence over a
registry binding; clearing the registry value does not remove a configured binding. Registry changes
take effect without restarting the service. Changes to the JSON authentication settings require a restart
from an elevated terminal. Existing issuer and public trust configuration are still required.

Integration uses a separate loopback port, disposable issuer keys, separate trust/repository DBs, one shared
replay DB, two project UUIDs and distinct Githubie/Buckettie audiences. No production remote writes or
service installation are authorized by the CR. The peer must support the advertised scope set and error
contract before production deployment.
