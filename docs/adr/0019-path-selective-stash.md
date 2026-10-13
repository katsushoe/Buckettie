# ADR 0019: Path-selective local Git stash

## Status

Accepted

## Context

Users need to separate local changes for review and commits without moving or discarding unrelated work. An arbitrary Git command or free-form pathspec would bypass the fixed Git command boundary.

## Decision

Expose typed stash push/list/apply/drop operations through the allowlisted local Git gateway and MCP/CLI surfaces. Push accepts repository-relative paths and uses literal pathspecs after `--`; omitted paths intentionally means all changes. Include selected untracked files. Reject rooted paths, traversal, `.git` segments, and pathspec namespace syntax. Return full stash object IDs, and require those IDs for apply/drop. Apply restores the staged index state. Audit records the operation and repository without logging selected path names.

Selection is path-granular. It does not split separate hunks within one file; callers must keep a file's changes together or use a separate working tree for hunk-level separation.

## Consequences

The implementation remains within the fixed Git process gateway and its repository allowlist. Provider capabilities advertise each operation; assertion scopes are read for listing and `repository.stash.write` for mutations. Tests must verify selected tracked/untracked paths are stashed while unselected paths remain unchanged.
