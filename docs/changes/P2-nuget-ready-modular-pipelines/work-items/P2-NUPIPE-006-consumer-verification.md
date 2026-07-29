# P2-NUPIPE-006: Verify Consumers, CI Examples, and Release Readiness

## 📌 Status

Approved

## 🚦 Delivery Priority

- Priority: P2
- Rationale: Public NuGet packages are release-ready only after verification outside this repository.
- Blocking prerequisites: P2-NUPIPE-001 through P2-NUPIPE-005, approved ADR-001, secret rotation, and User-provided access to one GitHub Actions test repository plus one GitLab test project/runner before CI evidence is collected.

## 🔗 Delivery Context

- Parent change: [P2-nuget-ready-modular-pipelines](../README.md)
- Approved design baseline: `0e01cced32f4d8e0946e54609880c617199a5b4c`
- Prerequisites: P2-NUPIPE-001–005, ADR-001, and the architecture record.
- Applicable principles: None.

## 🧩 Explicit Governance Constraints

Verification consumers reference only generated CodeAnalysis, Build, and Combine `.nupkg` files and never use a main-project `ProjectReference`. GitHub Actions and GitLab CI examples call the consumer's Build host and select a provider through the same Combine configuration. The GitLab example preserves separate Build and Combine jobs with manifest handoff. EverythingButTheSink contributes pinned shared-behavior evidence and a derived compatibility fixture only; this work package does not modify that repository. Model and notification adapters belong to consumers and may be absent. Secrets enter only through platform secret/variable mechanisms. Examples contain no real token, fixed GitLab instance, or automatic repository-file overwrite.

## 🎯 Outcome, Scope, and Non-goals

The outcome is a set of consumer fixtures backed by a temporary candidate source and controlled upstream sources, cross-process manifest verification, package-content inspection, GitHub/GitLab CI examples, side-effect-free real CI dry runs, an EverythingButTheSink-derived compatibility fixture, a release checklist, and release readiness.

Non-goals: no external NuGet push; no real remote PR/MR/Release creation; no creation of CI secrets or organization settings; no changes to the external EverythingButTheSink checkout or remote repository.

## 🔍 Current Behavior and Impact Boundary

After P2-NUPIPE-003, the actual repository Build host and its GitHub workflow are already migrated and compatibility-tested through direct ProjectReferences. This repository still has no GitLab CI example and no independent fixture that verifies analyzers or pipelines only through NuGet dependencies. This work package proves the distinct public-package boundary rather than repeating repository-host migration.

## 🧪 Behavior Cases

| ID | Preconditions and input | Action | Expected observable behavior |
| --- | --- | --- | --- |
| BC-006-1 | Local source contains every same-version new nupkg; one fixture declares only Build, and the GitHub fixture declares direct PackageReferences to Build plus Combine | Restore/build both fixtures | The Build-only host composes through `AddBuildPipeline`; the GitHub host composes through `AddStandardPipeline`; both succeed without ProjectReferences or reliance on a transitive Build reference |
| BC-006-2 | Local source contains every same-version new nupkg and the GitLab fixture declares direct PackageReferences to Build plus Combine | Restore/build GitLab fixture with custom URL under `Validate` | Validate succeeds without ProjectReferences, configuration binding preserves the URL, and no provider/network operation occurs |
| BC-006-3 | Two CI examples | Run static/template checks | Both invoke the host, request full history only for enabled read features, scope secret placeholders only to mutation steps/jobs, and contain no value |
| BC-006-4 | Coordinated calendar-release pack output and version-pinned dependency graph | Run package/security/license/version-restoration checks | Three package nuspecs share one normalized `yyyy.M.d[.<counter>]` version; Build/Combine assembly and file versions equal `yyyy.M.d.0` for counter zero or the exact four-part positive-counter version, and their informational-version cores match the package version; assets/symbol/SourceLink/path rules pass; Combine depends exactly on same-version Build; `Directory.Build.props` is byte-identical after the run with no staged/committed version difference; dependency audit has no unapproved vulnerability, deprecation, or license conflict |
| BC-006-5 | Validate profile with remote actions off | Run fixtures | Mock platform services record no network call |
| BC-006-6 | EverythingButTheSink-derived compatibility fixture references only new nupkg files | Process A builds/tests/packs/publishes archives; copy artifact root; process B uses `ConsumeArtifacts` for GitLab Publish/Message with fake boundaries | Typed manifest, source revision, tests, multi-RID archives, MR/Release, NuGet/Generic Package, and extension/event ordering preserve evidenced semantics without modifying the source repository |
| BC-006-7 | Consumer first omits AI/notification adapters, then registers fakes | Run Build/Message/Publish | No-adapter run succeeds with zero calls; adapters receive only neutral models and create no Gemini/Feishu public dependency |
| BC-006-8 | Candidate packages and CI templates are ready, with every remote switch off | Run GitHub Actions and GitLab CI dry runs in test consumers | Both restore candidate packages, run the intended profile/job handoff, expose no secret, and create no remote resource |
| BC-006-9 | Baseline/fixture is derived from external evidence containing repository-specific source/resources/configuration | Validate provenance/content | Pin the approved source identifier/SHA without an internal URL/path; record only allowlisted semantic facts and independently implement synthetic inputs/expected outcomes; copy no source, complete resource, configuration, or secret value; no runtime access/write to external checkout |
| BC-006-10 | P2-NUPIPE-003 supplies the proposed pre-cutover release-history file, capture evidence, file hash, and User acceptance record | Run release-readiness checks | Evidence proves paginated authoritative capture, complete-local-tag verification, exact accepted SHA-256, no secret/raw authenticated response, and unchanged accepted file bytes; absent approval, a later unapproved edit, or an unverifiable record blocks release |

## 🛠️ Implementation Contract

| Boundary or artifact | Required change | Compatibility or invariant |
| --- | --- | --- |
| Consumer fixtures | Provide a minimal Build-only host plus GitHub and GitLab hosts with direct same-version PackageReferences to Build and Combine and different providers | PackageReference only; candidate TedToolkit packages resolve only from the temporary source; Build-only has no Combine asset; dual-API hosts do not rely on a transitive Build compile asset |
| EverythingButTheSink compatibility fixture | Pin source identifier `EverythingButTheSink` at `6ccbdc3498be6ef3c3d14a91646bb6649c46f5c9` (or a later User-reapproved revision), without storing an internal URL/path; independently implement synthetic LocalBuild props and Gemini/Feishu neutral fakes from allowlisted semantic facts | No copied source/complete resource/configuration/secret, source modification, runtime access, or cross-repository ProjectReference; two independent processes map semantic results rather than legacy filenames |
| CI examples | Store canonical templates at `tests/Consumers/Templates/github-actions.yml` and `tests/Consumers/Templates/gitlab-ci.yml`, then copy those exact bytes into the User-provided test repository/project for retained CI evidence | Use GitLab `GIT_DEPTH: 0`/GitHub `fetch-depth: 0` only for history features; scope credentials to mutation steps/jobs; handoff only through access-controlled same-pipeline artifacts; package writes no CI files; retained run evidence records the template SHA-256 |
| CI dry runs | Execute candidate-package consumers on GitHub Actions and GitLab CI with remote switches disabled | Real CI context and artifact handoff are verified without PR/MR/Release/package-push side effects |
| Release checks | Inspect compiled metadata, nupkg metadata/content/symbols, dependency boundaries, and repository-file hashes | Nuspec, assembly/file four-part mapping, informational-version core, and manifest satisfy the exact release contract; Combine's exact Build dependency matches; the temporary version rewrite is restored byte-for-byte and never committed; no concrete AI/notification dependency |
| Configuration hygiene | Remove real-value Build configuration from project/package output; keep only ignored local config and `.example` placeholders after rotation | History is not treated as secret revocation; User rotation closes PB-02 |
| Release-history readiness | Verify the P2-NUPIPE-003 capture evidence, User acceptance record, and current file SHA-256 | The accepted baseline is immutable operational history; later correction requires a separately approved audited data-correction change |

## ✅ Verification Plan

| Behavior case | Test level and location | Setup and action | Observable assertion | Command |
| --- | --- | --- | --- | --- |
| BC-006-1, BC-006-2 | Consumer integration | Restore/build the Build-only, GitHub, and GitLab hosts with package source mapping | Exit code 0; no ProjectReference; Build-only resolves no Combine package; both direct pipeline PackageReferences and every TedToolkit package come from the candidate source at the coordinated version | Fixture script |
| BC-006-3 | Static template test | Read the two canonical files under `tests/Consumers/Templates` | Build invocation and secret placeholders exist; tokens and fixed instances do not; the files copied for real CI evidence have the same SHA-256 | `dotnet run --project tests/ReleaseContract.Tests -c Release` |
| BC-006-4 | Package-content and version-restoration test | Parse assemblies, nupkg/nuspec/symbols and compare pre/post repository hashes/status | Required assets and metadata exist; only Build/Combine have matching symbols/SourceLink; exact assembly/file/informational/nuspec/manifest mapping agrees; the version file is restored with no version-only Git difference | Same command |
| BC-006-5 | Fixture integration | Disable/mock remote services | No network or remote calls | Fixture script |
| BC-006-6 | EverythingButTheSink-derived compatibility integration | Process A writes manifest, copy artifact, process B registers fake provider and consumes it | No duplicate build; manifest, source revision, call sequence, output paths, RIDs, and extension ordering match | Fixture script |
| BC-006-7 | Consumer contract test | Run with no adapters, then fake neutral adapters | AI/notification are optional and adapters exist only in the consumer assembly | Fixture script |
| BC-006-8 | Manual pre-release CI gate with retained logs | Run the GitHub and GitLab templates against candidate packages with remote switches off | Both CI runs pass, GitLab transfers the artifact root between jobs, and recorded remote mutation calls remain zero | Platform run links recorded as completion evidence |
| BC-006-9 | Provenance/static/secret contract test | Read baseline JSON and scan fixture source/config/runtime paths | Approved identifier/SHA match; no internal URL/path is stored; allowlist only; no secret-like copied value, external access, or write exists | `dotnet run --project tests/ReleaseContract.Tests -c Release` |
| BC-006-10 | Release-readiness evidence and hash check | Read the capture record, acceptance record, baseline file, and complete local tags | Capture fields and normalized-version/exact-tag/target mappings validate, including legacy counter-zero `.0`; current SHA-256 equals the User-accepted value; no unapproved mutation is present | `dotnet run --project tests/ReleaseContract.Tests -c Release` plus completion-evidence review |

## ⏱️ Workload Estimate

- Planning range: 0.35–0.60 person-months.
- Confidence: Low–Medium.
- Assumptions: Candidate packages restore through source mapping, the external baseline is accessible, CI artifact copying preserves content, P2-NUPIPE-003 supplies User-accepted immutable release-history evidence, and User can provide test-project/runner access with permission to execute workflows but no permission or enabled switch for remote mutation.
- Exclusions: NuGet.org permissions, remote resource creation, production CI rollout, modification of EverythingButTheSink, and final release approval.
- Actual effort: Not completed.
- Variance: Not completed.

## 📋 Completion Evidence

Record the temporary-source location, fixture restore/build output, manifest copy and source-revision validation, test results, package checks, CI template checks, side-effect-free GitHub/GitLab run links and logs, coordinated resolved version, accepted release-history evidence/hash verification, and the pre-release manual checklist.

## 🔄 Migration and Rollback

Consumer fixtures and CI examples are additive test/documentation assets and can be removed independently. Do not push a release that fails verification. Correct already-published versions only through a new version.

## ⚠️ Risks and Open Questions

| Item | Impact | Owner or next decision |
| --- | --- | --- |
| Test CI projects differ from production repositories | Production-only policy or permission defects remain possible | Require side-effect-free GitHub and GitLab CI dry runs and document that production rollout remains a consumer responsibility |
| A GitHub Actions test repository or GitLab test project/runner is unavailable | The package can pass local tests but cannot satisfy release readiness | User provides both execution environments; retain the release gate as blocked and do not substitute mocked evidence |
