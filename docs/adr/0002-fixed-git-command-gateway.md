# ADR 0002: Fixed Git command gateway

- Status: Accepted

## Context

Buckettie must support repository status, fetch, fast-forward-only pull, and policy-controlled push without exposing arbitrary shell or Git argument execution to an MCP client.

## Decision

Expose one typed method per allowed Git operation. Resolve repositories only by configured Repository ID, validate LocalRoot and Remote URL before every operation, and construct arguments internally with `ProcessStartInfo.ArgumentList`. Use `--` before configured Remote and branch operands. Repository status reads configured remote-tracking refs with fixed `rev-parse` arguments and calculates local HEAD divergence from configured develop with fixed `rev-list --left-right --count`; it performs no implicit network fetch. Disable terminal prompts and force stable English Git diagnostics. Command timeout is supplied to the infrastructure constructor by the executable host.

## Alternatives

- Generic `run_git(args)`: rejected because it exposes unbounded Git behavior and option injection.
- Shell command strings: rejected because quoting, shell expansion, and command chaining expand the trust boundary.
- LibGit2Sharp: not selected because the specification requires system Git and later `GIT_ASKPASS` integration.

## Impact

Adding a Git operation requires a new typed interface method and explicit implementation. Commands cannot prompt interactively. Status remote HEAD and ahead/behind values reflect the most recent local remote-tracking refs and therefore become current after fetch, pull, or push.

## Security conditions

- Validate Allowlist, LocalRoot, `.git`, reparse points, and Remote URL before network operations.
- Never accept executable names, command strings, or arbitrary argument arrays from MCP input.
- Reject direct push to protected branches and dirty-tree push when configured.
- Do not place API Tokens in arguments, environment values that can persist, output, or logs.
- Pass only the validated configured LocalRoot as process-local `safe.directory` so LocalSystem can operate the Allowlisted Repository without changing global Git configuration.

## Operational conditions

The host supplies a positive timeout. Timeout or cancellation terminates the entire Git process tree. `LC_ALL=C` is set so error mapping is deterministic.

## Implementation, tests, and documentation

Application owns policy orchestration and structured results. Infrastructure owns process execution. Unit tests verify policy rejection and exact arguments without contacting a Remote. Credential-assisted integration tests will be added with the temporary credential helper.
