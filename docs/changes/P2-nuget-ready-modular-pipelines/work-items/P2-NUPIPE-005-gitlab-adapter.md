# P2-NUPIPE-005: Implement the Internal GitLab Provider and Custom Instance URLs

## 📌 Status

Approved

## 🚦 Delivery Priority

- Priority: P2
- Rationale: Satisfies the core requirement for GitHub/GitLab support and arbitrary self-hosted GitLab URLs.
- Blocking prerequisites: P2-NUPIPE-002, P2-NUPIPE-003, and ADR-001.

## 🔗 Delivery Context

- Parent change: [P2-nuget-ready-modular-pipelines](../README.md)
- Approved design baseline: `0e01cced32f4d8e0946e54609880c617199a5b4c`
- Prerequisites: P2-NUPIPE-002, P2-NUPIPE-003, ADR-001, and architecture record.
- Applicable principles: None.

## 🧩 Explicit Governance Constraints

Only Combine's internal GitLab provider may reference a GitLab SDK or GitLab CI variables. Phase A compares candidates in an isolated PoC outside the main solution/default CI, recording TFM support, license, dependency footprint, maintenance/security posture, explicit URLs/path prefixes, Generic Package operations, injectable HTTP/fake tests, authentication, and required API coverage. Performance evidence is needed only if it decides the selection. Phase B starts only after User approves the supplemental ADR.

Consumers explicitly configure `InstanceUrl` and `ApiUrl` for web and API use. Do not hard-code GitLab.com, a company instance, or `/api/v4`. URLs may use different hosts and retain path prefixes. They must be absolute and contain no user information, query, or fragment. HTTPS is the default; HTTP requires explicit `AllowInsecureHttp=true`. Fall back to `CI_SERVER_URL` and `CI_API_V4_URL` only inside GitLab CI and only for missing values. Missing authentication is an error only when an enabled GitLab action needs it. JobToken reads only `CI_JOB_TOKEN` through the internal GitLab CI reader; private/OAuth logical references use `IPipelineSecretResolver`. Tokens never enter URLs or logs.

## 🎯 Outcome, Scope, and Non-goals

The outcome is approved client evidence plus an internal GitLab provider inside Combine that provides source-control context, MR, Generic Package, Release, URL, and authentication behavior. Consumers reference Combine and select `RepositoryProviderKind.GitLab`. Scope includes reusable behavior extracted from EverythingButTheSink plus isolated tests; the external repository is not modified.

Non-goals: no GitLab.com requirement; no GitLab dependency in Build; no branch-policy inference from GitLab CI; no Feishu or Gemini integration.

## 🔍 Current Behavior and Impact Boundary

The reference `GitLabCi.cs` reads environment variables directly, and `ServiceCollectionHelpers.AddGitlab` constructs services from `GitLabCi.ServerUrl` and `ProjectId`. `SharedHelpers.Type` selects a flow from fixed `main`, `development`, and GitLab CI state. This proves the capability but is not a configurable public contract.

## 🧪 Behavior Cases

| ID | Preconditions and input | Action | Expected observable behavior |
| --- | --- | --- | --- |
| BC-005-1 | Explicit `InstanceUrl=https://gitlab.example.com`, `ApiUrl=https://gitlab.example.com/api/v4` | Register provider | API client uses ApiUrl and web links use InstanceUrl |
| BC-005-2 | GitLab CI variables exist and options omit URLs | Register provider | Use `CI_SERVER_URL` and `CI_API_V4_URL` independently |
| BC-005-3 | Explicit URLs and CI variables both exist | Register provider | Explicit options win |
| BC-005-4 | An enabled GitLab action needs an API URL, which is absent outside GitLab CI | Validate the owning action | Return an error naming the configuration key before client creation; do not request GitLab.com |
| BC-005-5 | HTTP/unsafe URL, missing auth, timeout, or API redirect is encountered | Validate/execute | Explicit HTTP only; reject unsafe/timeouts/redirects safely; no TLS bypass or credential forwarding |
| BC-005-6 | Job, private, or OAuth token | Run MR/Release service | Use `JOB-TOKEN`, `PRIVATE-TOKEN`, or bearer authentication exactly and never log the secret |
| BC-005-7 | Release tag or source/target MR already exists with equal then changed desired content | Request mutation | Reuse equal state or update once when changed; never create a duplicate |
| BC-005-8 | Instance and API URLs use different HTTPS hosts | Validate and create links/requests | Accept configuration and use each URL only for its declared purpose |
| BC-005-9 | Manifest contains publish archives with distinct logical names for multiple RIDs and both publication switches are enabled | Publish a release | Upload each Generic Package under its artifact logical name first, then create/update the Release with neutral links and return `ReleasePublicationResult` |
| BC-005-10 | Enable only `PublishArtifacts`, then only `CreateRelease` | Execute both runs | The first uploads Generic Packages without a Release; the second creates or updates a Release without package links |
| BC-005-11 | Release request has an explicit revision and tag/ref is absent, equal, or points elsewhere | Create/update | Use exact revision, reuse equal ref, fail different target without moving it, and never infer default branch |
| BC-005-12 | Generic Package or Release call fails/has uncertain completion, then reruns against missing/matching/mismatching remote files | Resume publication | Query first, reuse hash-matching packages, fail on mismatch without overwrite, upload only missing files, then create/update Release idempotently |
| BC-005-13 | Prefixed API URL and dynamic package/version/filename characters are used | Construct requests | Preserve prefix and percent-encode every dynamic segment exactly once |
| BC-005-14 | Supported push/manual/pipeline-source values and unsupported GitLab CI contexts are supplied | Read execution context | Map exact neutral trigger/context fields; unsupported or insufficient context does not infer business intent |
| BC-005-15 | An explicit provider-neutral Release-finalization request has an empty artifact list and an absent/matching/conflicting tag or Release | Call `IReleaseFinalizer` with GitLab selected | Resolve only GitLab configuration/credential, create or reuse exactly one Release for the exact existing/absent-matching tag target, return the neutral result, and perform no Build, package, artifact, change-request, notification, or tag-cleanup action; conflicting tag/Release or non-empty artifacts fail before mutation |

## 🛠️ Implementation Contract

| Boundary or artifact | Required change | Compatibility or invariant |
| --- | --- | --- |
| Phase A evidence and ADR | Compare candidates in an isolated PoC for TFM, license, footprint, maintenance/security, explicit URLs/prefixes, auth, Generic Packages, fakeability, and API coverage | No main-solution dependency or Phase B implementation before User-approved supplemental ADR |
| `GitLabConnectionOptions` | Define `InstanceUrl`, `ApiUrl`, `ProjectId`, authentication mode, logical credential reference, and `AllowInsecureHttp` | URLs are `Uri`; web/API semantics remain separate; HTTP is explicit opt-in; public options contain no token |
| URL resolver | Implement options > GitLab CI variables > configuration error | Never fall back to a fixed official URL |
| CI context reader | Map `GITLAB_CI=true`; `CI_PIPELINE_SOURCE=push` to Push, `web` to Manual, `pipeline`/`parent_pipeline` to ReusableCall, and others to Other; read branch/SHA/`CI_COMMIT_BEFORE_SHA`, user, pipeline, and project variables | All-zero before becomes absent; missing/unsupported data is not invented; generic CI alone is insufficient |
| Service registration | Register the internal GitLab provider | Build references no GitLab SDK and Combine public APIs expose no GitLab type |
| MR/Release | Migrate reference behavior to shared overridable branch conventions | Default main/development; duplicate requests are idempotent |
| Generic Package | Map each PublishArchive logical name, version, and file from `ArtifactPublicationRequest` to the Generic Package API under explicit ApiUrl | Query-before-retry; hash-match reuse; no silent overwrite; encode segments; expose no NGitLab type |
| Release-only finalization | Implement Combine's provider-neutral `IReleaseFinalizer` through the internal GitLab provider using an empty-artifact `ReleasePublicationRequest` | Explicit call is required; provider types do not leak; exact tag target is verified; no manifest/package bytes are required; no unrelated action executes |
| Security | Resolve secrets by mode and validate/redact URIs/logs | Correct job/private/OAuth headers; query/userinfo/fragment rejected; HTTPS by default |

## ✅ Verification Plan

| Behavior case | Test level and location | Setup and action | Observable assertion | Command |
| --- | --- | --- | --- | --- |
| BC-005-1–5, 8 | GitLab provider unit tests | Use fake environment variables and options validator | URL source, precedence, separate-host semantics, HTTP opt-in, and rejection reasons are correct | `dotnet run --project tests/TedToolkit.ModularPipelines.Combine.GitLab.Tests -c Release` |
| BC-005-6 | GitLab provider unit tests | Use fake HTTP/SDK client and logger | Authentication headers/calls are correct and logs contain no token | Same command |
| BC-005-7 | GitLab provider unit tests | Return equal/differing existing MR/Release state | No duplicate create; update and dispositions match content equality | Same command |
| BC-005-9 | GitLab provider unit tests | Use fake HTTP, two RID zip files, and a Release request | Upload URL, authentication, links, and Release neutral assets are correct | Same command |
| BC-005-10 | GitLab provider unit tests | Toggle the two publication switches independently | Package-only and Release-only call sequences contain no implicit extra operation | Same command |
| BC-005-11 | GitLab provider unit tests | Use a validated request with a recording Release client | The exact request revision becomes the Release ref; no default-branch lookup supplies it | Same command |
| BC-005-12 | Failure/retry tests | Simulate uncertain/missing/matching/mismatching package and failed Release states | Verified files are reused, mismatches fail, missing uploads resume, and Release mutation is idempotent | Same command |
| BC-005-13 | URI/request-construction tests | Exercise prefixed base and special dynamic values | Prefix and single encoding are exact | Same command |
| BC-005-14 | Environment-reader tests | Supply supported/unsupported GitLab pipeline sources and variables | Neutral context mapping is exact and safe | Same command |
| BC-005-15 | GitLab provider contract and dependency test | Invoke `IReleaseFinalizer` with recording provider/client boundaries across absent/matching/conflicting state and inspect the public API graph | Exact query/create/reuse/fail sequence and zero unrelated calls hold; public contracts contain no GitLab SDK/API type | Same command |
| BC-005-1–15 | Build/Combine contract tests | Use the provider through Combine's neutral models | Build and Combine public APIs expose no GitLab type | Same command |

## ⏱️ Workload Estimate

- Planning range: 0.35–0.55 person-months.
- Confidence: Low–Medium.
- Assumptions: At least one candidate supports explicit API URLs, path prefixes, Generic Package operations, idempotent Release-only lookup/create without package bytes, and a fakeable boundary; otherwise this work package returns to design review.
- Exclusions: Acceptance against a real self-hosted server, SSO, proxy, and TLS deployment.
- Actual effort: Not completed.
- Variance: Not completed.

## 📋 Completion Evidence

Record the Phase A PoC, approved supplemental ADR, selected dependency version, URL/authentication-redaction tests, Generic Package ordering tests, Release-only finalization tests, idempotency tests, and a consumer build using a non-secret self-hosted URL configuration. The side-effect-free GitLab CI dry run is owned by P2-NUPIPE-006.

## 🔄 Migration and Rollback

Use the reference repository as evidence only; do not create a cross-repository `ProjectReference`. Release consumers can remove Combine or disable remote switches to roll back. Never fall back to GitLab.com after configuration failure.

## ⚠️ Risks and Open Questions

| Item | Impact | Owner or next decision |
| --- | --- | --- |
| HTTP client cannot handle reverse-proxy path prefixes | Self-hosted GitLab fails | Prove support in the supplemental ADR; reject the client if it fails |
| Job Token permissions vary by GitLab configuration | MR/Release operations fail | Document minimum permissions and allow consumers to select PrivateToken |
