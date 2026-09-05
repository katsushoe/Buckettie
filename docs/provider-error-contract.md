# Provider error response contract

Buckettie returns typed Provider errors so Moyai can normalize failures without parsing messages. The contract version is `1`.

## Top-level fields

Every tool result contains `schema_version`, `ok`, `operation`, `project`, `data`, and `error`. On failure, `ok` is `false`, `data` is `null`, and `error` is non-null. The existing `repository` field remains as a compatibility alias for `project`.

## Error fields

`error` always includes the Provider `code`, localized `message`, `outcome`, `retryable`, `suggested_action`, `correlation_id`, `provider`, and `common_code`. `provider.name` is `Buckettie`; `provider.code` preserves the original Buckettie code. Sanitized diagnostics may appear in `provider.details`. Existing `category` and `details` fields remain unchanged for compatibility.

`outcome` is one of `not_executed`, `failed`, or `unknown`. A caller must check repository state before repeating a modifying operation whose outcome is `unknown`.

`common_code` is one of `AUTHENTICATION_REQUIRED`, `PERMISSION_DENIED`, `POLICY_REJECTED`, `CONFLICT`, `COMMUNICATION_FAILURE`, `PROVIDER_ERROR`, or `INVALID_STATE`.

`suggested_action` is one of `configure_authentication`, `request_permission`, `review_policy`, `resolve_conflict`, `check_status`, `retry`, `inspect_provider_error`, or `correct_condition`. It is a control value, not localized prose.

For `push`, authentication, permission, and conflict responses report a confirmed `failed` outcome. Local policy rejection reports `not_executed`. Communication failures, timeouts, cancellation, remote verification failure, and unclassified Git failures report `unknown`, `retryable=false`, and `suggested_action=check_status`.

## Compatibility

The legacy `code`, `repository`, `category`, `details`, `summary`, `status`, `retry_after_seconds`, and `project_candidates` fields are retained. Consumers should migrate control flow to `common_code`, `outcome`, `retryable`, and `suggested_action`, while using `provider.code` and diagnostics for display and investigation.
