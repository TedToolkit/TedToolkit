# ADR-003: Use Internal HTTP Clients for Repository Providers

- Status: Accepted
- Date: 2026-07-29
- Decision owner: User
- Decision scope: Internal GitHub and GitLab providers in `TedToolkit.ModularPipelines.Combine`.
- Applicable principles: None; the repository does not currently contain approved principle documents.
- Related work packages: P2-NUPIPE-004 and P2-NUPIPE-005.
- Supersedes: None
- Superseded by: None

## Decision

Implement both providers over an internal injectable `HttpClient` transport and each platform's documented REST API. Do not add Octokit, NGitLab, or another vendor SDK.

The transport disables automatic redirects, uses caller-supplied absolute API bases without deriving one URL from another, streams artifact files, scopes credentials to the exact configured API origin, and returns provider-neutral models. Tests replace the transport and exercise request paths, headers, ordering, idempotency, and failure behavior without network access.

## Evidence and Trade-offs

| Candidate | TFM/license/footprint | Enterprise and prefixes | Fakeability/API coverage | Decision |
| --- | --- | --- | --- | --- |
| Octokit | Compatible, MIT, sizable transitive surface | GitHub Enterprise supported | Injectable connection; GitHub-only | Rejected: adds a public dependency for operations covered by a small internal boundary |
| GitLabApiClient/NGitLab | Compatible variants, permissive licenses, additional dependency surface | Custom hosts supported with varying URL assumptions | Fakeable at client level; GitLab-only | Rejected: Generic Package and split web/API URL behavior still need custom handling |
| Built-in `HttpClient` | .NET runtime, no package/license addition | Exact independent bases and reverse-proxy prefixes | Fully injectable; covers required REST operations | Selected |

The cost is maintaining narrowly scoped request/response mappings. Provider contract tests and fail-closed JSON parsing protect that boundary. A future SDK adoption requires a new ADR and must retain the same neutral public surface.

## Security Invariants

- HTTPS is required unless the owning option explicitly permits HTTP.
- User information, query strings, and fragments are forbidden in configured bases.
- Redirects are never followed.
- Dynamic path segments are escaped once.
- Tokens are added only as headers immediately before an enabled request and never enter URLs, models, exceptions, or logs.
- GitHub Actions and GitLab job tokens are read only by their selected internal provider.

## Acceptance

The User approved all implementation decisions and actions on 2026-07-29 before provider implementation began.
