# ADR 0017: Safe local branch switching

## Status

Accepted

## Decision

Buckettie exposes `buckettie_branch_switch(repository, branch)` and the symmetric CLI command `buckettie branch switch <repository> <branch>`. The operation is scoped to an allowlisted local repository and requires `repository.branch.write` authorization. It is audited as `buckettie_branch_switch`.

The gateway first applies the same conservative branch-name rule as the other branch tools (rejecting option-like names and revision expressions before any Git process runs), validates the name with `git check-ref-format`, returns a no-op success when the requested branch is already checked out, rejects a dirty working tree, verifies `refs/heads/<branch>` exists locally, then invokes Git with fixed arguments and `--no-guess`. It never fetches, creates a branch, or targets a remote repository. Structured errors are `branch_switch_invalid_ref`, `branch_not_found`, and `working_tree_dirty`. Success data carries `branch_switch` with `previous_branch`, `current_branch`, and `switched` (false for the same-branch no-op).

`bitbucket_provider_capabilities.operations.branch_switch` advertises this local Repository Contract operation. It is additive to Contract version 3 and requires no change to Bitbucket's remote API contract. Moyai consumers can discover support through the existing capabilities endpoint; they must pass the registered repository ID and branch name, preserve the structured error code, and request `repository.branch.write` authorization.

## Consequences

The checkout state of the allowlisted local repository changes. Concurrent external Git operations can still race between status/ref checks and `git switch`; `--no-guess` prevents a missing local ref from silently becoming a remote-tracking branch checkout.
