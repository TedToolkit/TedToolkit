# P2-NUPIPE-002: Extract the Platform-neutral Build Package

## 📌 Status

Approved

## 🚦 Delivery Priority

- Priority: P2
- Rationale: Provides a vendor-neutral, testable foundation for composition and platform adapters.
- Blocking prerequisites: Approved ADR-001 and architecture record.

## 🔗 Delivery Context

- Parent change: [P2-nuget-ready-modular-pipelines](../README.md)
- Approved design baseline: `0e01cced32f4d8e0946e54609880c617199a5b4c`
- Prerequisites: ADR-001 and architecture record.
- Applicable principles: None.

## 🧩 Explicit Governance Constraints

Build and its public APIs must not reference or expose GitHub, GitLab, Octokit, NGitLab, Gemini, OpenAI, `Microsoft.Extensions.AI`, TextCopy, or notification SDK types. It embeds separate shared `.editorconfig`, commit-message, and change-request instruction baselines plus overridable conventions. It provides only the optional neutral `IChangeDescriptionGenerator`. Build must not create platform resources or send notifications.

## 🎯 Outcome, Scope, and Non-goals

The outcome is an independently packable `TedToolkit.ModularPipelines.Build` project that first establishes one compatible ModularPipelines dependency baseline and then provides workspace handling, build inputs, a pure date/daily-release-counter policy, a recoverable temporary MSBuild-version transaction, explicit pack, a versioned artifact manifest, neutral description models, general lifecycle modules, and test doubles. It is the only pipeline package required by consumers that need only build and verification.

Non-goals: no PR/MR, Release, platform authentication, or notification creation; no Gemini/OpenAI/Feishu implementation; no repository-specific rule, prompt difference, branch, secret, or project list.

## 🔍 Current Behavior and Impact Boundary

`TedPipeline.cs` currently configures both the pipeline and GitHub extensions. `SharedHelpers.cs` fixes repository paths and branches, while `Modules` mixes general build and release behavior. `BumpVersionModule` derives a date/revision version from provider Release history and temporarily rewrites `Directory.Build.props`; that rewrite is what makes SDK-compiled outputs receive the release version. Build-host and module namespaces are affected. The observable format, build, test, pack, compiled-version, calendar-version, exact restoration, and artifact results must be reproduced without provider coupling or a persistent/committed version-file change.

## 🧪 Behavior Cases

| ID | Preconditions and input | Action | Expected observable behavior |
| --- | --- | --- | --- |
| BC-002-1 | Only Build is installed | Compose through `AddBuildPipeline` and run each applicable Build execution profile | No Combine or platform SDK loads; the fixed local graph runs and build/test results are collected |
| BC-002-2 | Custom paths and `BuildInputs` | Run pack | Artifacts are written only to the configured output directory |
| BC-002-3 | Inspect Build modules, services, dependencies, and public APIs | Run dependency and module contract checks | No package-push, PR/MR, Release, provider-client, notification-delivery, or Git staging/commit implementation exists; the only version-file writer is the explicit recoverable temporary MSBuild transaction |
| BC-002-4 | Inspect Build assembly references | Run API/dependency contract checks | Public APIs and dependencies contain no hosting-platform type |
| BC-002-5 | No resource or convention override | Read resources and run the update module | Embedded shared baseline and standard branch values are returned without writing a file |
| BC-002-6 | Overrides include valid/invalid UTF-8, NUL, oversize, line endings, branch override, and explicit write | Resolve/write | Validate bounds; compose deterministically with version/hash/LF/one newline; write atomically only when enabled |
| BC-002-7 | TedToolkit's current resources plus synthetic overlays for allowlisted EverythingButTheSink differences include distinct commit/PR language and rules | Expand all resources | TedToolkit matches its normalized pre-change text; synthetic overlays match approved shared/differing semantics without storing an external complete resource, cross-kind rule leakage, or a language switch |
| BC-002-8 | Neutral `ConsumedReleaseVersionRecord` values with `PackageSucceeded`/`Abandoned` dispositions plus `HasActivePublicationReservation` cover no release today, multiple same-day revisions, an abandoned partial attempt, counters 65534/exhausted, any pending publication, unrelated pre-filtered input, invalid/duplicate records, a package-success rerun, and conflicts; valid/malformed version files, Pack targets, and recovery states then vary | Resolve a version from a caller-supplied `DailyReleaseVersionRequest`, then—only for a new-version result—begin the temporary MSBuild-version transaction and run Build and Pack | Pure policy returns `yyyy.M.d` for counter zero or one beyond the highest consumed counter through 65534, fails exhaustion before Build, increments package-success reruns, never treats abandoned as success, and rejects invalid/conflicting records without choosing a clock or touching file/environment/provider/tag/manifest/CI state; an active reservation returns `PublicationRecoveryRequired` with no buildable version; the transaction atomically stamps the SDK build, maps `yyyy.M.d` to assembly/file `yyyy.M.d.0` and a positive counter to the same four components, keeps informational/nuspec cores equal to the normalized package version, restores exact original bytes, and rejects mixed/mismatching/duplicate or unsafe recovery state without overwrite |
| BC-002-9 | Pack/Test/Publish produce valid/malformed root-level manifests with auxiliary or unlisted files | Write/read | Parent-derived artifact root, exact atomic JSON, full inventory, dirty/status/path/hash/nuspec/result relations round-trip; residual/unlisted/partial/schema/revision errors fail |
| BC-002-10 | No `IChangeDescriptionGenerator` is registered | Run LocalBuild/Pack | Description generation skips; build/test/pack succeed; no model secret is resolved |
| BC-002-11 | Minimal independently written TedToolkit and EverythingButTheSink-shaped extensions represent their evidenced stage/API needs across different current ModularPipelines versions | Compile focused PoCs against one candidate baseline | Both synthetic shapes compile through intended public APIs without copied external source or platform/consumer-specific dependencies |
| BC-002-12 | Inputs include invalid parallelism, duplicate/empty names, root equality, traversal, drive/UNC, link/reparse escapes, arguments overriding owned switches, or Build targets with/without clean-first | Validate inputs and run clean/build | Reject unsafe input before commands; valid targets use bounded parallelism/deterministic results; artifact Clean deletes only its validated root; clean-first runs exact clean then build and gates on clean failure |
| BC-002-13 | MTP run, MTP test, and VSTest targets produce fresh/stale/malformed/empty TRX and nonzero exits | Run Test targets | Use the declared CLI/separator/logger form and unique directory; aggregate outcomes; stale-only, malformed, zero-test, no-result, or nonzero is failure |
| BC-002-14 | Build/test fails, cooperative cancellation occurs, restoration fails, or an earlier process left a valid/conflicting recovery file | Execute Check graph or start any later configured active RunBuild invocation | No new dependent producer runs; restoration precedes the failed/cancelled manifest; valid interrupted state recovers before artifact cleanup even when the later profile is LocalBuild/Validate/Message; None remains zero-call and defers recovery to the next active profile; conflicting state is not overwritten; restoration failure forbids publication; the TedToolkit recovery file is covered by the exact anchored root ignore rule; hard termination is the documented immediate-restoration exception |
| BC-002-15 | Publish has multiple target shapes, valid/invalid/colliding names, links, empty output, and repeated identical input | Run Publish | Valid targets create atomic deterministic archives with safe sorted entries/fixed timestamps; unsafe/empty/colliding cases fail |
| BC-002-16 | Generator is toggled with staged, unstaged, untracked, empty, binary, or oversized changes and valid/invalid/oversized output | Run description flow | Explicit enablement only; local diff scope, request/output bounds, and normalization hold; invalid output follows failure mode; logs/clipboard expose nothing |
| BC-002-17 | Format/resource writes, temporary version stamping, or consumer Prepare writes are requested across profiles; each format scope sees staged/unstaged/untracked paths | Validate/run | Ordinary repository writes are explicit LocalBuild-only; every active RunBuild profile supplies the temporary-version recovery paths, LocalBuild/Validate/Message use a null target version for recovery only, Pack/Publish alone may start a new temporary write that restores before manifest, and None remains zero-call; WorkingTreeTracked, HeadTracked, and All select their exact declared scope; invalid combinations fail before callbacks |

## 🛠️ Implementation Contract

| Boundary or artifact | Required change | Compatibility or invariant |
| --- | --- | --- |
| Dependency baseline | Select and centrally pin one ModularPipelines version after compiling both repositories' extension shapes | Version selection is part of this work package, not an external blocker |
| Build public models | Define explicit root/artifact roots, named Build/Test/Pack/Publish targets, test command kind, structured arguments, results, options, conventions, and layout | Paths are never process-derived; formatting/generation are opt-in; RID requires explicit self-contained choice |
| Build-only composition | Define `BuildExecutionProfile` plus `BuildPipelineExtensions.AddBuildPipeline(PipelineBuilder, BuildExecutionProfile, BuildInputs, BuildOptions?)` returning the same builder | LocalBuild/Validate/Pack/Publish/Message register only the fixed local Build graph; Publish means local artifact production, not a remote action; no Combine reference is required |
| Build modules | Migrate clean, format, both test command forms, build, explicit `dotnet pack`, local `dotnet publish`, archive, and artifact collection | Check DAG gates pack/publish on assertion and always writes the manifest last; no remote publication service |
| Calendar release versioning | Define `DailyReleaseVersionPolicy`, `DailyReleaseVersionRequest`, `ConsumedReleaseVersionRecord`, `ConsumedReleaseDisposition`, and resolution models over an explicit caller-selected date/source revision/consumed history/active-reservation Boolean | Pure and provider-neutral; it chooses no clock or time zone; counter zero normalizes to `yyyy.M.d`; later runs increment beyond package-success or abandoned consumed counters through 65534 and exhaustion fails; abandoned never represents success; an active reservation returns recovery-required and no buildable version; provider tag annotations, manifest hashes, CI run identities, and recovery metadata are excluded; conflicts fail |
| Temporary MSBuild versioning | Add optional `BuildInputs.MsBuildVersion`; define options with explicit file/property/recovery path/nullable target version, atomic begin/restore, root-contained non-artifact recovery file/single-writer lock, startup recovery, compiled-version validation, and an exact root-anchored TedToolkit `.gitignore` rule | Every active RunBuild profile supplies recovery paths; LocalBuild/Validate/Message are recovery-only, only Pack/Publish may supply a version, and None invokes no Build operation; the outer non-packable `dotnet run` bootstrap is excluded from release artifacts, while the nested recovered clean/rebuild is authoritative; recovery content is never logged/packed/uploaded; `git check-ignore` proves only the intended host recovery path is ignored; exact original bytes return before manifest; mismatched recovery fails without overwrite; no Git/provider action or version commit |
| Manifest | Define exact schema 1, status, typed artifacts, normalized relative paths, SHA-256/size, nuspec identity, source revision, target/test/failure records, reader, and writer | Reject every other schema, traversal/link escape, missing/hash/size mismatch, mixed versions, and non-opted-in revision mismatch; failed manifests authorize no mutation |
| Resources and descriptions | Embed editor plus separate language-neutral commit/change-request instruction bases, opt-in generator, and matching overlays | No cross-kind prompt leakage, language switch, concrete AI, clipboard/output sink, or sensitive logging |
| Description failures | Define `ChangeDescriptionFailureMode` with `Continue` default and explicit `FailPipeline` | Missing/null generator always skips; concrete model behavior remains consumer-owned |

## ✅ Verification Plan

| Behavior case | Test level and location | Setup and action | Observable assertion | Command |
| --- | --- | --- | --- | --- |
| BC-002-1 | Build composition/unit tests | Reference only Build, call `AddBuildPipeline` for each applicable profile, and run with a fake command executor and explicit inputs | The returned builder, registered graph, call sequence, result collection, and absence of a Combine/platform dependency are correct | `dotnet run --project tests/TedToolkit.ModularPipelines.Build.Tests -c Release` |
| BC-002-2 | Build unit tests | Use a temporary directory and custom options | Writes remain inside the configured directory and outside paths are rejected | Same command |
| BC-002-3 | Build dependency/module contract test | Inspect assembly references, public APIs, registered modules, and service types | No remote publication, Git version mutation, provider client, or notification delivery exists; only the bounded temporary version transaction can write the configured file | Same command |
| BC-002-4 | Compilation/contract test | Reflect public APIs and assembly references | No hosting-platform assembly or type leaks | Same command |
| BC-002-5, BC-002-6 | Build unit tests | Read embedded resources and use temporary overrides | Baseline/constants, precedence, hashes, and write skipping are correct | Same command |
| BC-002-7 | Build unit tests | Load TedToolkit resources and synthetic semantic overlays, then normalize text | TedToolkit expansion matches exactly; synthetic cases prove approved EverythingButTheSink semantics without copied complete resources | Same command |
| BC-002-8 | Version-policy unit plus file-transaction/command/package integration test | Supply deterministic `DailyReleaseVersionRequest` values, package-success/abandoned neutral records including 65534/exhaustion, active-reservation flags, byte-varied XML files and recovery states, a fake executor, and a real temporary pack fixture | Calendar/consumed-counter/conflict results are exact; exhaustion and active reservation produce zero Build commands; abandoned advances without success; public versioning models contain no tag/manifest/run metadata; temporary XML is visible during pipeline-owned SDK commands; assembly/file versions use the specified four-part mapping, informational/nuspec/manifest versions agree, files return byte-identical, only explicit pack creates packages, and the pack command omits `--no-build` | Same command |
| BC-002-9 | Unit and process integration test | Process A writes a manifest; process B reads a copied artifact root at matching and mismatching revisions | Results are equivalent at the same revision; paths, hashes, RID/TFM, and failures validate; mismatch requires explicit opt-in | Same command |
| BC-002-10 | Unit test | Do not register a generator in DI | No model client resolves and Build/Pack completes | Same command |
| BC-002-11 | Focused compile PoC | Compile TedToolkit and EverythingButTheSink extension shapes against each candidate ModularPipelines baseline | One baseline supports both through public APIs and is pinned centrally | Same command |
| BC-002-12 | Input/filesystem-safety and command-order tests | Exercise names, reserved arguments, strict-descendant paths, junction/symlink, artifact clean, and Build clean-first | Every invalid input fails before executor/delete calls; only the validated artifact root is removable; clean-first ordering/configuration/failure gate are exact | Same command |
| BC-002-13 | Command/TRX tests | Run fake and real TUnit/standard fixtures with fresh/stale/missing results | Exact command, unique directory, counters, freshness, and failure rules hold | Same command |
| BC-002-14 | Failure-DAG and recovery tests | Fail build/test/restore, cancel execution, simulate matching/mismatching interrupted state followed by each profile, and run `git check-ignore` against the TedToolkit recovery path plus adjacent control paths | Assert gates producers; restoration is attempted before the final Failed manifest; every later active RunBuild profile recovers before clean, None remains zero-call and leaves state for the next active profile, mismatches are not overwritten, exactly the anchored recovery path is ignored, and no publication-authorizing result exists | Same command |
| BC-002-15 | Publish archive tests | Publish multiple target shapes | Explicit arguments and artifact names map one-to-one to validated PublishArchive entries | Same command |
| BC-002-16 | Description privacy/enablement tests | Toggle enablement around a recording generator/logger/clipboard sentinel | Call counts and safe logs match the contract | Same command |
| BC-002-17 | Profile/write/format-scope compatibility tests | Toggle built-in writes, all format scopes, profile-scoped extensions, recovery paths, and nullable target versions | Exact tracked-file selection and LocalBuild-only ordinary-write composition hold; LocalBuild/Validate/Message recover, Pack/Publish may begin a version transaction, None is zero-call, and invalid combinations fail before commands/callbacks | Same command |

## ⏱️ Workload Estimate

- Planning range: 0.48–0.75 person-months.
- Confidence: Low–Medium.
- Assumptions: At least one candidate ModularPipelines version supports the intended public stage extension; the change is revised if the PoC disproves the stage model. The estimate includes cross-platform atomic recovery and exact compiled-metadata mapping tests.
- Exclusions: Platform adapters, final publication, and CI templates.
- Actual effort: Not completed.
- Variance: Not completed.

## 📋 Completion Evidence

Record calendar-version policy cases, temporary and restored repository-file hashes, interrupted-recovery cases, compiled assembly/file/informational versions, build/pack output for each package, manifest round-trip results, unit tests, and public-API/dependency checks.

## 🔄 Migration and Rollback

Keep the monolithic project and current Build host available while this work package establishes the new Build project and tests. Repository-host migration is owned exclusively by P2-NUPIPE-003 after CodeAnalysis and Build are ready. If Build extraction is blocked, do not partially redirect the host or make the monolithic project the first public package surface.

## ⚠️ Risks and Open Questions

| Item | Impact | Owner or next decision |
| --- | --- | --- |
| Modules depend implicitly on current static helpers | Behavior changes after extraction | Cover behavior with BC-002-1 before migration |
| Manifest v1 misses an existing runtime layout | Cross-job Combine cannot consume Build output | Round-trip the current EverythingButTheSink `output/` before freezing the schema |
