# ADR 0018: Unborn Branch Support

- Status: Accepted
- Date: 2026-10-10

## Context

A newly initialized repository has a symbolic current branch but no commit object. Git commands that require `HEAD` to resolve can fail, and the current status mapper can report that state as an unrelated reference error. This prevents a provider caller from inspecting the repository, creating its first commit, or pushing that commit to an empty remote.

## Decision

- Resolve the current branch from `symbolic-ref --short HEAD` when `rev-parse --abbrev-ref HEAD` cannot resolve it (or reports detached `HEAD`). Preserve detached-HEAD behavior when the symbolic fallback also fails.
- Treat a missing `HEAD` as a successful empty local-head value. Status still reports the symbolic branch and porcelain worktree state; ahead/behind stays null with `comparison_unavailable_reason=local_head_missing`.
- For `repo diff`, use `git diff --cached` until a commit exists, then keep comparing against `HEAD`.
- Keep commit staging and commit identity resolution unchanged. A first commit is an ordinary root commit on the current branch, subject to the existing branch allowlist and author checks.
- Keep push policy checks unchanged. Push the current branch using the configured remote and the existing fixed argument path; a newly created remote branch is allowed only when the provider accepts the normal push.
- Apply the behavior in the shared Git client/Gateway so CLI and MCP return the same state and errors.

## Alternatives Considered

- Reject status/diff/commit/push until a first commit exists: rejected because it leaves a newly registered repository unusable through the provider.
- Create a commit, branch, or remote reference implicitly during status/diff: rejected because read operations must not mutate repository state.
- Weaken push or branch policies for the first push: rejected because unborn state does not justify bypassing established authorization and branch restrictions.

## Consequences and Security

- `local_head` is the empty string only when no commit exists; callers must not parse it as a hash. Remote heads may still be null, and ahead/behind is unavailable until both local and comparison references exist.
- The symbolic branch name still passes through existing branch policy checks before commit and push. No fetch, branch creation, force push, or shell command construction is introduced.
- Local temporary-repository tests cover unborn and ordinary committed repositories, including initial push to an empty bare remote. Gateway tests cover structured status and ensure divergence is not queried without a local commit.

## Implementation and Documentation

Update `GitCommandClient`, `GitGateway`, unit and real-Git integration tests, and the English/Japanese `COMMANDS.md` files. CLI and MCP share the same Gateway and therefore the same status output, commit/push policy, and error mapping.
