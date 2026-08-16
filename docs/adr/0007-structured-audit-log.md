# ADR 0007: Structured audit log

## Context

Buckettie must record every exposed Git and Bitbucket action without persisting credentials or user-supplied content.

## Decision

Wrap both application gateways at the MCP host boundary. Each completed operation writes a structured `ILogger` event containing client, tool, repository, branch, pull-request ID, tag, result, elapsed milliseconds, and fixed error code. A daily file provider writes `buckettie-yyyyMMdd.log` below the directory adjacent to `bin`; the standard release layout therefore writes to `F:\Buckettie\logs`.

## Alternatives

Logging inside every MCP tool was rejected because duplicated instrumentation is easy to omit. Logging HTTP bodies was rejected because requests and responses can contain secrets, descriptions, messages, and diffs.

## Impact

All gateway calls made by MCP tools share one audit boundary. Log files rotate by date; retention and external shipping remain operator responsibilities.

## Security

The audit event type has no Token, authorization header, password, PR title or description, merge message, Tag message, diff, local path, URL, or exception field. Repository IDs and ref names are intentionally recorded.

## Operational considerations

The service account needs create and append permission on the log directory. A log write failure is surfaced rather than silently discarding the audit record.

## Implementation

`AuditedGitGateway` and `AuditedBitbucketRepositoryGateway` measure calls and emit `BuckettieAuditEvent`. `DailyFileLoggerProvider` formats and serializes file writes within the process.
