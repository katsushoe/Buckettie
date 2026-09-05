# ADR 0015: Controlled latest-commit history rewrite

## Status

Accepted

## Decision

Buckettie exposes separate MCP and CLI operations for previewing a latest-commit identity rewrite,
executing the local rewrite, and updating one remote branch with `--force-with-lease`. The operations
require a full expected SHA, a reason, a clean index and working tree, no unfinished Git operation,
the target branch to be checked out, and the branch to appear in `history_rewrite_branches`. This
permission is empty by default and is independent from ordinary direct-push permission.

The rewritten commit preserves its tree, parents, complete message, author date, and committer date.
Author and committer name/email fields are independently optional. A commit signature cannot be
preserved by `commit-tree`; a signed commit is therefore rejected unless signature removal is
explicitly allowed. Execution creates `refs/buckettie/recovery/<branch>/<old-head>` before moving the
branch and never deletes that reference automatically.

Remote update reads the actual remote branch with `ls-remote`, compares it with the expected full SHA,
uses only `--force-with-lease=<ref>:<sha>`, and reads the actual remote again to verify the new local
HEAD. There is no unconditional force fallback. Local rewrite and remote update remain separate tools.

## Consequences

Normal commit and push behavior is unchanged. Administrators must explicitly approve repository-policy
updates that enable history rewriting. Audit events include actor, reason, target ref, old/new HEAD,
recovery ref, result, and correlation ID. Tests use mocks or disposable repositories; Buckettie's own
history is never rewritten during verification.
