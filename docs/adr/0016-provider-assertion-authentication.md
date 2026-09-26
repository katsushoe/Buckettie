# Provider Assertion authentication

## Status

Implemented locally; deployment and production integration require separate approval.
The contract owner is Moyai. See its Provider Authentication Consumer Contract (2026-09-07).

## Context

MCP previously checked Origin without authenticating repository operations. Moyai now supplies
one ES256 assertion for each tool invocation. The Bitbucket API credential is a separate secret
owned by Buckettie and must remain unchanged.

## Decision

Use `Moyai.ProviderAuthentication` 1.0.2 as a pinned NuGet dependency, including its validator,
file trust store, and independently initialized SQLite replay cache. The vendored package and
SHA-256 are in `packages/`; NuGet source mapping restricts this package to that source.
Do not compile another repository or copy its JWT implementation.
Version 1.0.1 rechecks time after replay reservation and rejects assertions that expire while the
reservation is in progress, before returning a principal. The consumed reservation is not rolled back.
Version 1.0.2 also provides `IAssertionExecutionValidator.EnsureCurrentAsync`. The production audited
Git and Bitbucket gateways call it immediately before invoking the underlying gateway, using the
authenticated principal from the current HTTP request. It rechecks expiry, current key status and
capability without reserving replay again. A failure returns a stable MCP tool error and makes no
gateway call. The existing approval prompts belong to certificate-authenticated administration;
repository operations currently have no internal approval wait. Any future wait inside a repository
gateway must be followed by another execution check before continuing.

All repository tools require an assertion at the HTTP boundary before dispatch. Missing configuration,
unknown tools, missing bindings, malformed requests and failed authentication deny execution.
Bootstrap allows initialize, tool/prompt discovery, the static guidance prompt, ping, version and
capability; it never includes project enumeration. JSON batches, duplicate property names and tool
notifications are rejected. Requests are buffered in memory with a 1 MiB limit.

Administrators explicitly bind a Moyai Project UUID to a Buckettie repository ID and its normalized
Bitbucket identity in protected configuration. Each request independently resolves the current
repository allowlist and checks the configured identity against its workspace/slug. Neither a JWT
claim nor an input parameter creates a binding. Duplicate UUIDs, repository IDs or identities are invalid.
Existing gateway branch/protection policies remain authoritative after authentication.
Repository requests hold the same gate as register/update/unregister from context resolution through
dispatch. This prevents a concurrent registration change from redirecting an authenticated operation.
The gate is exclusive and fails fast when busy; callers must not automatically retry a change request.

Amendment (2026-09-24): following the Moyai consumer contract update, direct loopback calls on `mcp_port`
without an `Authorization` header may invoke `list_projects` and tools whose scope mapping is exactly
`repository.read`. The request is marked as a direct read and the audited gateways reject any non-read
operation under that mark. Requests carrying an assertion are validated as before and never fall back.

Amendment (2026-09-24, user decision): direct loopback calls on `mcp_port` without an `Authorization` header
may also invoke register/update/unregister. The registration services require interactive desktop approval
(ADR 0012) before writing, and unregistration now requires the same approval; the dialog title names the
operation. This approval is the provider-specific administrator boundary for local use. The mutually
authenticated HTTPS endpoint remains available and is required for callers that are not direct loopback.

Amendment (2026-09-24, user decision): Buckettie is a Moyai sub-tool but must not presuppose Moyai. The
server runs standalone by default and enables Moyai integration only when started with `--moyai`. The mode is
a process start option rather than the presence of `provider_authentication`, so an operator can run
standalone temporarily without editing or removing the authentication configuration, and the mode ends with
the process. In standalone mode, direct loopback calls on `mcp_port` without an `Authorization` header may use
every repository tool; the gateway's allowlist, branch policies, desktop approval for registration changes,
and audit log remain the controls. With `--moyai`, missing authentication configuration fails startup and
change tools require an assertion as above. Capabilities report the mode so Moyai can detect a Buckettie that
is not in integration mode. This supersedes the earlier "no disabled or transition mode" statement.

Amendment (2026-09-27, CR-2026-09-26-accept-moyai-assertion): MCP C# SDK 2.2 clients start with
`server/discover`, which the boundary previously rejected as `auth_scope_denied`, so every Moyai connection
failed before any tool call. Discovery methods, client notifications, and `GET`/`DELETE` session requests now
pass the boundary; other methods return `mcp_method_not_allowed`. In standalone mode a loopback request's
`Authorization` header is removed and ignored instead of rejected, because standalone mode does not use
assertions and a header-less loopback request already has the same access.

Registration/update/removal, and project enumeration outside the direct-read allowance, use a separate loopback HTTPS listener with
client certificate pinning. Certificate private keys stay in Windows certificate stores. The service
loads its server certificate from LocalMachine/My; the CLI loads its client certificate from
CurrentUser/My. An unconfigured administrator endpoint rejects administrative tools. An administrator
certificate does not authorize repository operations or replace their required assertion.

## Alternatives

- Source copies or cross-repository ProjectReference: rejected by the consumer contract and reproducibility requirements.
- A new shared bearer token: adds a long-lived transferable credential; use mutually authenticated TLS for administration.
- Accepting current unauthenticated calls: bypasses the new repository authorization boundary.

## Security and operation conditions

The operator must protect the configuration, public trust bundle and replay DB with ACLs allowing
only the service account and authorized administrators to write. A trust key is reloaded for every
validation. All consumers for the same deployment must use the same replay DB; it must not be deleted
or restored to an older snapshot while assertions can remain valid. A missing trust file or inaccessible
cache denies requests. Do not use temporary fixtures as production keys or configuration.

Certificate pins are explicit trust anchors; there is no automatic CA discovery or revocation lookup.
Remove a compromised client pin and restart the service. The validity period is checked during TLS
and again at the management boundary. Pin changes and binding changes require a service restart.
No service update, certificate installation, ACL changes or public release is performed by this change.

The old MCP Service Token migration is not applicable: the previous MCP endpoint did not validate
such a token. Never delete or migrate the external Bitbucket token for this change.

## Implementation, tests and documentation

`ProviderAuthenticationBoundary` supplies only a validated principal to downstream HTTP dispatch.
`ProviderToolPolicy` defines the advertised minimal scopes, including explicit provider-specific scopes.
The server's policy-enforcing gateways remain in the dispatch path. Authorization headers are removed
before invoking MCP handlers. Authentication errors log a stable code rather than parser or secret text.

Tests cover real package validation, context/scope/time rejection, revoked trust keys, shared replay,
missing credentials, administrator isolation, and MCP read/write/error calls using both client identities.
CONFIG and MCP setup documentation describe configuration and compatibility changes. Production
Moyai/Githubie/Buckettie integration is coordinated separately using isolated services and credentials.
