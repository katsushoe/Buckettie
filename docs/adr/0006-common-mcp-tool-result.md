# ADR 0006: Common MCP tool result

- Status: Accepted

## Context

Git and Bitbucket REST Gateways use operation-specific result and error enums. Exposing those internal shapes directly gives MCP clients inconsistent success fields, error locations, enum serialization, and not-found meanings. Tool output must remain stable and must not disclose Git stderr, HTTP bodies, exceptions, credentials, or other untrusted details.

## Decision

Every Buckettie MCP Tool returns the common structured shape `{ ok, operation, repository, data, error }`. Successful results place operation-specific typed content in `data` and set `error` to null. Failures set `data` to null. The existing `code` and `message` fields remain stable, and Git failures additionally return `summary`, `suggestedAction`, `retryable`, and `correlationId`. The summary identifies the failed Git operation without including stderr. A Server-boundary mapper converts every Git and Bitbucket REST error to a fixed snake_case code and fixed non-secret guidance. Context-dependent HTTP 404 results are mapped by operation to repository, branch, Pull Request, or Tag not-found codes.

Git command failures are classified, when recognizable, as `authentication_failed`, `network_error`, `permission_denied`, `conflict`, `non_fast_forward`, `working_tree_dirty`, or `remote_mismatch`. `git_failed` is reserved for unclassified failures. Every Git failure receives an opaque correlation ID which is returned to the client and written to the audit event. CLI repository commands call the same MCP tools and therefore expose the same structured contract.

## Alternatives

- Expose Gateway results unchanged: rejected because Git and REST output contracts differ and leak internal enum organization into MCP schemas.
- Return exception or upstream error text: rejected because it is unstable, may contain sensitive data, and is unsuitable for automated decisions.
- Replace all internal result types with one generic type: rejected because Git and REST layers have different responsibilities and typed errors remain useful below the MCP boundary.

## Impact

All MCP output schemas share the same envelope while retaining typed success data. Existing clients can continue to evaluate `ok`, `error.code`, and `error.message`; new clients can use the additional Git diagnostic fields. Adding an internal Gateway error requires an explicit mapper decision or it safely falls back to `git_failed` or `bitbucket_api_error`.

## Security conditions

- Error messages, summaries, and suggested actions are fixed literals and never include command output, HTTP response bodies, exceptions, paths, URLs, Tokens, credentials, or caller-provided text.
- Git stderr is used only for internal classification and is discarded before the Gateway result is created. It is not written to Tool responses, CLI output, or audit logs.
- Only stable snake_case codes are exposed for automated handling.
- Policy failures remain normal structured Tool failures and do not bypass Gateway enforcement.

## Operational conditions

Transport- or schema-level MCP failures remain protocol errors because no Tool operation result exists. Cancellation that reaches a Gateway is returned as `cancelled`; server shutdown and transport cancellation may terminate the protocol request.

## Implementation, tests, and documentation

The Server project owns the common contract and mapper because it is the external MCP boundary. Tests verify the common generic envelope, Git stderr classification, correlation IDs, fixed guidance, and absence of raw diagnostics from returned results. This ADR is the authoritative contract for MCP Tool result formatting.
