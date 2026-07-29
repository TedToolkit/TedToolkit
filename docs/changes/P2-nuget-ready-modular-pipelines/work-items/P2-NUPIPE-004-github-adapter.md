# P2-NUPIPE-004: Implement the Internal GitHub Provider in Combine

## 📌 Status

Completed

## 🚦 Delivery Priority

- Priority: P2
- Rationale: Preserves existing GitHub Actions, PR, Release, and asset behavior while removing it from the general Build package.
- Blocking prerequisites: P2-NUPIPE-002, P2-NUPIPE-003, and ADR-001.

## 🔗 Delivery Context

- Parent change: [P2-nuget-ready-modular-pipelines](../README.md)
- Approved design baseline: `0e01cced32f4d8e0946e54609880c617199a5b4c`
- Prerequisites: P2-NUPIPE-002, P2-NUPIPE-003, ADR-001, and architecture record.
- Applicable principles: None.

## 🧩 Explicit Governance Constraints

Only Combine's internal GitHub provider may reference a GitHub SDK or GitHub Actions variables. Phase A compares candidates in an isolated PoC outside the main solution/default CI, recording TFM support, license, dependency footprint, maintenance/security posture, Enterprise URLs, authentication, injectable HTTP, fake tests, path prefixes, and required API coverage. Performance evidence is needed only if it decides the selection. Phase B starts only after User approves the supplemental ADR. No vendor type enters public APIs. Branch policy and switches are neutral. A logical personal-token reference resolves through `IPipelineSecretResolver`; ActionsToken reads only `GITHUB_TOKEN` through the internal Actions environment reader. Either path runs only for an enabled owning action.

## 🎯 Outcome, Scope, and Non-goals

The outcome is approved client evidence plus an internal GitHub provider inside `TedToolkit.ModularPipelines.Combine` that provides GitHub/GitHub Enterprise context, PR, Release, and Release-asset operations while migrating current GitHub behavior. Consumers reference Combine and select `RepositoryProviderKind.GitHub`.

Non-goals: no GitLab behavior changes; no general build modules inside a GitHub-specific area; no default AI summary.

## 🔍 Current Behavior and Impact Boundary

`TedPipeline.cs` references `ModularPipelines.GitHub.Extensions`. `RunOnGithubActionOnlyAttribute`, version bump, PR, NuGet push, and Release modules directly depend on GitHub environment data and Octokit. Platform coupling must leave Build's public boundary: GitHub tag/history lookup moves to the repository host/provider side, Build retains the pure calendar policy, and the required temporary MSBuild-version stamping is performed only by Build's provider-neutral recoverable file transaction.

## 🧪 Behavior Cases

| ID | Preconditions and input | Action | Expected observable behavior |
| --- | --- | --- | --- |
| BC-004-1 | Simulated GitHub Actions environment | Register provider | Context contains correct branch, repository, and CI state |
| BC-004-2 | Remote switches off | Run Publish | PR/Release client call count is zero |
| BC-004-3 | An open PR already has the same source/target, with equal then changed desired content | Request a change request | Return Reused when equal or update once when changed; never create a duplicate |
| BC-004-4 | Inspect Build and Combine public surfaces | Reflect APIs and dependencies | Build contains no SDK dependency; Combine exposes no Octokit/GitHub type |
| BC-004-5 | Manifest contains publish files and both `CreateRelease` and `PublishArtifacts` are enabled | Publish a release | Create or resolve a draft Release, upload assets, finalize the Release, and return one neutral `ReleasePublicationResult` |
| BC-004-6 | Only `PublishArtifacts` is enabled | Publish against an existing, then a missing, matching Release | Upload to the existing Release; reject the missing-Release case without implicitly creating one |
| BC-004-7 | Only `CreateRelease` is enabled | Publish a release | Create or update the Release without uploading any asset |
| BC-004-8 | Explicit Enterprise URLs/repository identity and conflicting GitHub Actions variables are present | Resolve connection | Explicit values win; InstanceUrl is used for links and ApiUrl for calls |
| BC-004-9 | Explicit fields are absent, then an invalid/insecure URL or incomplete repository identity is supplied | Resolve and validate | Fall back independently to `GITHUB_SERVER_URL`, `GITHUB_API_URL`, and `GITHUB_REPOSITORY`; reject invalid values and HTTP unless explicitly allowed |
| BC-004-10 | Release request has an explicit revision; the tag and Release are absent, independently present, equal, or point elsewhere | Create/resume | Create a draft when the Release is absent, bind an absent/equal tag to the exact revision, reuse matching state, fail a different target without moving it, and never infer the default branch |
| BC-004-11 | URLs contain prefixes/unsafe parts/special segments; timeout expires; API redirects cross-origin | Validate/request | Preserve/encode, reject unsafe/timeout early, disable redirects/TLS bypass, and send bearer only to exact API origin |
| BC-004-12 | Asset upload fails or is uncertain, then reruns against a draft or published Release with missing/matching/mismatching assets | Resume publication | Query first, keep/resume draft, never redraft published state, reuse matches, fail on mismatch, upload only missing files, and finalize only a draft after all succeed |
| BC-004-13 | Supported branch-push, tag-push, manual, and reusable events plus unsupported events expose GitHub variables | Read execution context | Map exact trigger, branch, revision, actor, run/repository/compare URLs; populate Branch only for a branch ref, map a tag push to Push with null Branch so Standard selects None, and resolve unsupported or insufficient context safely without inferring business intent |
| BC-004-14 | An explicit provider-neutral Release-finalization request has an empty artifact list and an absent/matching/conflicting tag or Release | Call `IReleaseFinalizer` with GitHub selected | Resolve only GitHub configuration/credential, create or reuse exactly one Release for the exact existing/absent-matching tag target, return the neutral result, and perform no Build, package, asset, change-request, notification, or tag-cleanup action; conflicting tag/Release or non-empty artifacts fail before mutation |

## 🛠️ Implementation Contract

| Boundary or artifact | Required change | Compatibility or invariant |
| --- | --- | --- |
| Phase A evidence and ADR | Compare candidates in an isolated PoC for TFM, license, footprint, maintenance/security, Enterprise URLs, authentication, fakeability, path prefixes, API coverage, and asset ordering | No main-solution dependency or Phase B implementation before User-approved supplemental ADR |
| Internal GitHub provider | Implement source-control context, change-request, and provider-coordinated Release/asset operations | Platform types never cross the public API |
| Actions context reader | Map `GITHUB_ACTIONS=true`; `push`, `workflow_dispatch`, `workflow_call`; ref/SHA/actor/run/repository variables; and optional `before`/compare data from `GITHUB_EVENT_PATH` | Other events map to Other; all-zero before becomes absent; missing data is not invented |
| `GitHubConnectionOptions` | Define InstanceUrl, ApiUrl, Owner, Repository, authentication mode, logical credential reference, and explicit HTTP opt-in; use Actions variables only as field-level fallbacks | URLs reject query/userinfo/fragment, preserve prefixes, and are not derived; bearer tokens never enter URLs/options/logs |
| Existing modules | Move inside the provider or call neutral models | Current repository workflow remains reproducible |
| Release assets | Map neutral publish requests to draft-first upload with query-before-retry and hash-verified reuse | Disabled means zero calls; mismatch never overwrites; finalization waits for all assets |
| Release-only finalization | Implement Combine's provider-neutral `IReleaseFinalizer` through the internal GitHub provider using an empty-artifact `ReleasePublicationRequest` | Explicit call is required; provider types do not leak; exact tag target is verified; no manifest/package bytes are required; no unrelated action executes |
| Build dependencies | Remove GitHub references | Build has no transitive GitHub SDK |

## ✅ Verification Plan

| Behavior case | Test level and location | Setup and action | Observable assertion | Command |
| --- | --- | --- | --- | --- |
| BC-004-1, BC-004-8, BC-004-9 | GitHub provider unit tests | Inject fake options and environment values | Context, precedence, Enterprise URL separation, validation, and safe authentication behavior are correct | `dotnet run --project tests/TedToolkit.ModularPipelines.Combine.GitHub.Tests -c Release` |
| BC-004-2 | GitHub provider unit tests | Use a fake API client with remote switches off | No HTTP/API calls | Same command |
| BC-004-3 | GitHub provider unit tests | Return equal and differing existing PRs from the fake client | Create count is zero; update/disposition matches content equality | Same command |
| BC-004-4 | Build/Combine contract tests | Read Build references and Combine public APIs | No platform SDK leaks | Same command |
| BC-004-5–7 | GitHub provider unit tests | Use a recording fake Release/asset client and manifest file | Draft-create/find, upload, finalize, existing-only, release-only, hash, returned URL, and zero implicit creation are correct | Same command |
| BC-004-10 | GitHub provider unit tests | Use a validated request with a recording tag client | The exact request revision becomes the tag target; no default-branch lookup supplies it | Same command |
| BC-004-11 | URI/request-construction tests | Exercise prefixed bases and unsafe/dynamic values | Prefixes and encoding are exact; unsafe URLs fail before auth/client creation | Same command |
| BC-004-12 | Failure/retry tests | Simulate uncertain/missing/matching/mismatching asset states | Query/reuse/upload/fail/finalize sequence is idempotent and hash-safe | Same command |
| BC-004-13 | Environment-reader tests | Supply supported branch/tag push and manual/reusable shapes plus unsupported event payload variables | Neutral context is complete only for supported shapes; tag names never become branches, tag push safely maps to None at the composition boundary, and business intent is never over-inferred | Same command |
| BC-004-14 | GitHub provider contract and dependency test | Invoke `IReleaseFinalizer` with recording provider/client boundaries across absent/matching/conflicting state and inspect the host/public API graph | Exact query/create/reuse/fail sequence and zero unrelated calls hold; host and public contracts contain no GitHub SDK/API type | Same command |

## ⏱️ Workload Estimate

- Planning range: 0.30–0.50 person-months.
- Confidence: Low–Medium.
- Assumptions: At least one candidate supports Enterprise URLs, a fakeable HTTP/client boundary, the required draft-Release ordering, and idempotent Release-only lookup/create without package bytes; otherwise this work package returns to design review.
- Exclusions: Integration tests against a real GitHub repository and production secret configuration.
- Actual effort: Completed on 2026-07-29 in implementation commit `f14ed04a2c7ba8f123bbe3b7e65e813d91ee4a8c`.
- Variance: ADR-003 selected a dependency-free internal HTTP client rather than adding a hosting SDK.

## 📋 Completion Evidence

ADR-003 and 7 provider tests cover explicit Enterprise URLs, event-file context, PR reuse, recursive annotated-tag peeling, Release-only finalization, and streaming public GitHub asset upload. The side-effect-free GitHub CI dry run remains owned by P2-NUPIPE-006.

## 🔄 Migration and Rollback

Verify the GitHub provider through the package-only GitHub consumer and side-effect-free release configuration. P2-NUPIPE-003 owns the repository workflow shape, marker policy, and fake-boundary host tests; its gated provider-owned GitHub Release path must remain disabled until this work package passes. If a provider-owned remote operation is incorrect, keep `NUGET_RELEASE_ENABLED=false`, disable the corresponding consumer switch, and stop later publication.

## ⚠️ Risks and Open Questions

| Item | Impact | Owner or next decision |
| --- | --- | --- |
| GitHub environment variables vary by event type | Required context fields may be absent | Define the supported event set in the environment reader |
