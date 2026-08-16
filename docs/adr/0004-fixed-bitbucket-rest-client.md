# ADR 0004: Fixed Bitbucket REST client

- Status: Accepted

## Context

Buckettie must read Bitbucket Cloud repository and branch information without exposing arbitrary REST access, destinations, or credentials to an MCP client. Branch lists are paginated, API responses are external input, and API Tokens must remain outside configuration and logs.

## Decision

Expose typed repository, branch, Pull Request, and Tag methods behind an Application Gateway. Resolve workspace and repository slug from the Repository Allowlist, use the fixed `https://api.bitbucket.org/2.0/` base address, and build endpoint paths internally. Authenticate each request with the configured Atlassian email and the repository-scoped API Token read from the DPAPI Token Store. Limit collection pagination to 100 pages and reject malformed successful responses. Pull Request list source and destination filters are validated and applied locally using ordinal exact branch matching rather than accepting a caller-supplied REST query. Create Pull Requests only from configured develop to main, and fetch and validate the current Pull Request state and route immediately before merge. Create Tags only at the current HEAD of the configured Tag target branch after validating the configured Tag pattern; callers cannot supply a commit hash.

## Alternatives

- Generic `bitbucket_request(method, url, body)`: rejected because it permits arbitrary network access and REST operations.
- Agent-supplied workspace and repository slug: rejected because it bypasses the Repository Allowlist.
- Following the server-provided `next` URL directly: rejected because a compromised response could expand the destination trust boundary.
- Persisting an Authorization header: rejected because it increases secret lifetime and accidental disclosure risk.

## Impact

Every supported REST operation requires a typed interface method, fixed path construction, response model, and error mapping. Repository, branch, and Tag operations require their corresponding repository read/write scopes; Pull Request reads and writes require their corresponding pull-request scopes. Collections larger than 10,000 entries fail as an invalid response instead of paging without a bound. Diff content is limited to 5,000,000 characters.

## Security conditions

- Accept only Repository ID and operation-specific values from the caller.
- Never accept an arbitrary URL, HTTP method, header, or request body.
- URL-encode configured path components and branch names.
- Disable automatic redirects and follow Pull Request diff redirects only when the HTTPS destination remains under the configured Bitbucket API repository diff path.
- Reject merge unless the Pull Request is OPEN and follows the configured develop-to-main route.
- Resolve the Tag target hash from the configured branch immediately before creation; never accept an arbitrary target hash from the caller-facing Gateway.
- Never return, log, or persist the API Token or Authorization header.
- Map authentication, permission, rate-limit, network, timeout, and malformed-response failures to structured errors.

## Operational conditions

The executable host configures `HttpClient` with the fixed HTTPS base address and a 30-second timeout. The host must have ACL access to the DPAPI Token Store. Branch paging uses `pagelen=100`, increments a locally generated page number, and stops after 100 pages.

## Implementation, tests, and documentation

Application owns Allowlist resolution and typed results. Infrastructure owns HTTP authentication, fixed request construction, pagination, deserialization, and status mapping. Unit tests verify configured coordinates, exact paths, Basic authentication, pagination, malformed JSON, missing Token, and HTTP error mapping without contacting Bitbucket Cloud. Security documentation references this ADR.
