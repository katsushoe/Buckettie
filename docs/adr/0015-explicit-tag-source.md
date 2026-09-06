# ADR 0015: Explicit tag source

## Status

Accepted

## Context

`bitbucket_tag_create` previously resolved the configured tag target branch internally. A caller-supplied source was therefore lost, and a requested commit could differ from the remote tag target.

## Decision

Tag creation requires `source`, using the same branch-name or full 40-character commit SHA syntax as branch creation. Branch sources are resolved through Bitbucket, commit sources are verified through Bitbucket, and only the resolved SHA is sent as the remote tag target. Branch names remain subject to the configured tag target branch policy. No configured branch, local HEAD, checkout, or fetched reference is used as a fallback.

Successful creation returns `source`, `source_kind`, `source_hash`, and `target_hash`. Buckettie rejects a response whose name or target differs from the request. Invalid input returns `tag_source_invalid`; absent or inaccessible sources return `tag_source_not_found`. The optional message remains absent from the Bitbucket JSON request when omitted.

Provider capabilities advertise `contract_version=3` and `tag_source_required=true` so callers can reject incompatible providers without inserting defaults.
