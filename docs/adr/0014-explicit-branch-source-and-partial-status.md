# ADR 0014: Explicit branch source and partial repository status

- Status: Accepted
- Supersedes ADR 0002 only for missing remote-tracking references in repository status.

## Context

Using the configured develop branch as every new branch's source prevents creating the first develop branch from main. Requiring both remote-tracking references for status also hides otherwise usable local information. A source lookup's 404 was incorrectly classified as repository_not_found.

## Decision

`bitbucket_branch_create(repository, branch, source)` requires an explicit source. The CLI is `buckettie branch create <repository> <branch> <source>`. A 40-character hexadecimal source is a full SHA-1 commit ID; every other valid source is an exact branch name, including all-hex short names (never abbreviated commit IDs). HEAD, revision expressions, whitespace and invalid ref syntax are rejected. No main/develop/HEAD default is provided. Old two-argument calls fail instead of silently creating a different branch.

Resolve branches using the fixed branch GET endpoint and commits using the fixed repository commit GET endpoint. Pin the resolved full hash before POST. Validate the returned branch name and target. Return source, source_kind and source_hash alongside name/target_hash, and record valid source metadata in the audit log. A malformed successful creation response is an error, not permission to retry a mutation blindly. Remote branch creation never checks out or creates a local branch and never implicitly fetches.

`branch_source_invalid` denotes invalid input. A source branch lookup's 404 maps to `branch_not_found`; a source commit lookup's 404 maps to `branch_source_not_found`. A POST 404 also maps to `branch_source_not_found` rather than asserting repository absence. A 404 may also mean inaccessible repository/reference: callers must check access, not infer physical absence. Other authentication, permission, conflict and transient errors are preserved.

Status continues to compare local HEAD with the configured remote-tracking develop reference, without a network fetch. Only the quiet missing-reference result of fixed `rev-parse --verify --quiet` is optional. Missing remote_develop_head/remote_main_head are null; ahead/behind are null only when the develop comparison is unavailable, never fabricated zeroes. comparison_reference identifies the baseline, comparison_unavailable_reason is `remote_tracking_ref_missing_or_not_fetched` when unavailable, and missing_remote_references lists absent local refs. Local branch, HEAD and dirty state remain available. Missing main alone does not disable comparison against an existing develop. Other command failures and malformed divergence remain failures.

## Alternatives

- Implicit fallback to main or HEAD: rejected because it changes caller intent.
- Separate branch/commit optional inputs: rejected in favor of the shared Githubie `source` contract; classification is deterministic.
- Quietly return zero divergence or fetch automatically: rejected because these hide unknown state or introduce network side effects.
- Treat all Git failures as missing refs: rejected because it hides access and repository errors.

## Impact and compatibility

This is a breaking input/nullability change. Provider capabilities advertise contract_version=2, branch_source_required=true and repository_status_nullable=true without changing operation names. MCP output follows the existing snake_case serializer. Moyai must forward source unchanged, preserve nulls and inner errors, and reject old-provider incompatibility without inserting defaults. Runtime installation/release and Moyai implementation are separate from this source change. Existing status fields retain their names; new metadata is additive.

## Security and operational conditions

Keep repository allowlisting, configured workspace/slug, credentials, fixed endpoints and provider permissions unchanged. Never accept URLs, arbitrary Git arguments or revision expressions. No policy widening, real-repository branch changes, implicit retry, installation or release is part of this implementation. The new commit read uses the existing repository read scope.

## Implementation, tests and user documentation

Application validates source and orchestrates resolution; Infrastructure implements fixed commit reads and distinguishes missing local refs; Server publishes required input, errors and audit data; CLI forwards the same source. Regression tests cover main-only bootstrap, arbitrary branches/full commits, invalid/missing source, permission errors, fixed request payloads, schema requiredness, nullable status and non-missing failures. COMMANDS.md and COMMANDS.ja.md document usage and compatibility.

API references: [Bitbucket commits](https://developer.atlassian.com/cloud/bitbucket/rest/api-group-commits/), [Bitbucket branch creation](https://developer.atlassian.com/cloud/bitbucket/rest/api-group-refs/), [Git rev-parse](https://git-scm.com/docs/git-rev-parse).
