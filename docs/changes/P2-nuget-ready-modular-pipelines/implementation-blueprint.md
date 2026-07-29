# P2 Implementation Blueprint: NuGet Packages, Directories, APIs, and Tests

> Status: Proposed revision. Last approved design baseline: `0e01cced32f4d8e0946e54609880c617199a5b4c`. Proposed revised baseline: pending commit and User approval. This document constrains implementation of the [P2 change](README.md). If it conflicts with the [architecture record](../../architecture/modular-pipelines-packaging.md), ADR-001, or ADR-002, the accepted ADR takes precedence.

## 1. Final Solution Structure

```text
TedToolkit.slnx
│
├─ TedToolkit.CodeAnalysis/
│  ├─ TedToolkit.CodeAnalysis.csproj
│  ├─ buildTransitive/
│  │  ├─ TedToolkit.CodeAnalysis.props
│  │  └─ TedToolkit.CodeAnalysis.targets
│  └─ README.md
│
├─ TedToolkit.ModularPipelines.Build/
│  ├─ TedToolkit.ModularPipelines.Build.csproj
│  ├─ Assets/
│  │  ├─ EditorConfig.base
│  │  ├─ CommitMessage.base.md
│  │  └─ ChangeRequest.base.md
│  ├─ Artifacts/
│  ├─ Conventions/
│  ├─ Descriptions/
│  ├─ Inputs/
│  ├─ Versioning/
│  ├─ Modules/
│  │  ├─ Stages/
│  │  ├─ Clean/
│  │  ├─ Prepare/
│  │  ├─ Compile/
│  │  └─ Check/
│  ├─ Resources/
│  └─ README.md
│
├─ TedToolkit.ModularPipelines.Combine/
│  ├─ TedToolkit.ModularPipelines.Combine.csproj
│  ├─ Configuration/
│  ├─ Execution/
│  ├─ Events/
│  ├─ Modules/
│  ├─ Models/
│  ├─ Providers/
│  │  ├─ Internal/
│  │  ├─ GitHub/
│  │  └─ GitLab/
│  └─ README.md
│
├─ Build/
│  ├─ Build.csproj
│  ├─ Program.cs
│  └─ appsettings.example.json
│
├─ tests/
│  ├─ TedToolkit.CodeAnalysis.PackageTests/
│  ├─ TedToolkit.ModularPipelines.Build.Tests/
│  ├─ TedToolkit.ModularPipelines.Combine.Tests/
│  ├─ TedToolkit.ModularPipelines.Combine.GitHub.Tests/
│  ├─ TedToolkit.ModularPipelines.Combine.GitLab.Tests/
│  ├─ ReleaseContract.Tests/
│  ├─ HostCompatibility/
│  │  └─ TedToolkitBuildHostCompatibility/
│  └─ Consumers/
│     ├─ consumer-baselines.json
│     ├─ Templates/
│     │  ├─ github-actions.yml
│     │  └─ gitlab-ci.yml
│     ├─ BuildOnlyConsumer/
│     ├─ GitHubConsumer/
│     ├─ GitLabConsumer/
│     └─ EverythingButTheSinkConsumer/
│
└─ docs/
   ├─ adr/ADR-001-public-pipeline-package-boundaries.md
   ├─ adr/ADR-002-activate-sonar-through-an-external-package-dependency.md
   ├─ architecture/modular-pipelines-packaging.md
   └─ changes/P2-nuget-ready-modular-pipelines/
```

Keep the old `TedToolkit.ModularPipelines/` as a transitional source directory until migration is complete. Then move every implementation into Build or Combine and remove the duplicate behavior.

## 2. Project References and NuGet Dependencies

```text
TedToolkit.CodeAnalysis
  ├─ copied audited Roslynator/StyleCop analyzer and CodeFix assets
  └─ exact external dependency: SonarAnalyzer.CSharp [10.23.0.137933]

TedToolkit.ModularPipelines.Build
  ├─ ModularPipelines
  ├─ ModularPipelines.DotNet
  ├─ ModularPipelines.Git
  ├─ NuGet.Versioning
  └─ no source-hosting, AI, or notification SDK

TedToolkit.ModularPipelines.Combine
  ├─ TedToolkit.ModularPipelines.Build
  ├─ GitHub client selected by the P2-NUPIPE-004 evidence ADR
  └─ GitLab HTTP client selected by the P2-NUPIPE-005 evidence ADR

Repository Build host
  ├─ ProjectReference: TedToolkit.ModularPipelines.Build
  └─ ProjectReference: TedToolkit.ModularPipelines.Combine

Independent host that calls both APIs
  ├─ PackageReference: TedToolkit.ModularPipelines.Build
  └─ PackageReference: TedToolkit.ModularPipelines.Combine
```

Hard rules:

- CodeAnalysis references neither Build, Combine, nor another repository project.
- CodeAnalysis never copies `SonarAnalyzer.CSharp.dll`; its only permitted MSBuild assets are the two ADR-002 `buildTransitive` activation files.
- Build references neither Combine nor GitHub, GitLab, Octokit, NGitLab, or an equivalent hosting SDK.
- Build references no Gemini, OpenAI, `Microsoft.Extensions.AI`, TextCopy, Feishu, or concrete AI/notification SDK.
- Combine references no Feishu, Slack, Teams, or other notification SDK. Consumers register notification implementations through neutral interfaces.
- Combine references Build through `ProjectReference`; GitHub/GitLab SDKs appear only in Combine's project file and `Providers/`.
- `Build/Build.csproj` directly references both new projects and never restores a candidate package to bootstrap the package producer.
- Build and Combine source projects may use an explicit repository-only analyzer `ProjectReference` to `TedToolkit.CodeAnalysis` with analyzer-only/private metadata; package inspection must prove that it creates neither a runtime assembly reference nor a nuspec dependency. External consumers use the generated CodeAnalysis package.
- `tests/Consumers/*` reference only `.nupkg` files generated into a temporary source and contain no `ProjectReference`; `BuildOnlyConsumer` references only Build, while hosts that call both APIs reference Build and Combine directly.
- Independent hosts that call both Build and Combine declare direct same-version PackageReferences to both; Combine's transitive dependency is not their source-level contract.
- CodeAnalysis, Build, and Combine have the same candidate version in a coordinated TedToolkit release. Combine's nuspec dependency on Build is the exact range `[<release-version>]`.
- The Combine project keeps its compile-time `ProjectReference` to Build and owns one isolated pack target after `_GetProjectReferenceVersions`; the target requires exactly one matching Build item and rewrites only its generated `ProjectVersion` metadata to `[$(PackageVersion)]`. A focused PoC runs against one exact .NET 10 SDK patch; after it passes, repository-root `global.json` pins that patch with roll-forward disabled, any required test-runner selection is preserved, CI installs the same version, and every release-contract pack inspects the resulting nuspec. Changing the SDK requires an approved dependency-baseline update and a fresh PoC. Missing/changed SDK pack metadata fails pack rather than silently emitting the default minimum-version dependency; it does not add a candidate `PackageReference`, custom nuspec, or post-pack archive rewrite.

Client selection is not an implementer-local decision. P2-NUPIPE-004/005 each begin under `eng/provider-pocs/<provider>/<candidate>/`, excluded from `TedToolkit.slnx` and default CI. When durable evidence is needed, the supplemental ADR uses `docs/adr/ADR-<number>-<provider-client>/README.md` with its evidence index and retained results under that ADR's `evidence/` directory; it records commands, versions, licenses, dependency graphs, fake-test results, and conclusions. Evidence covers target-framework compatibility, maintenance/security posture, Enterprise/self-managed URLs, injectable HTTP, authentication, path prefixes, and required API coverage. Performance is measured only if it decides selection. No candidate dependency enters a package project before User approves the linked supplemental ADR; rejected PoC binaries are never packaged.

## 3. CodeAnalysis File and Asset Structure

```text
TedToolkit.CodeAnalysis.nupkg
├─ README.md
├─ Icon.jpg
├─ LICENSE.txt
├─ THIRD-PARTY-NOTICES.txt
├─ buildTransitive/
│  ├─ TedToolkit.CodeAnalysis.props
│  └─ TedToolkit.CodeAnalysis.targets
└─ analyzers/dotnet/
   ├─ cs/
   │  └─ <audited host-neutral analyzer/CodeFix DLLs and runtime dependencies>
   ├─ roslyn3.8/cs/
   │  └─ <audited Roslyn 3.8-compatible variant set>
   └─ roslyn4.7/cs/
      └─ <audited Roslyn 4.7-compatible variant set>
```

CodeAnalysis contains no `lib/` or `build/`. It copies the audited Roslynator/StyleCop analyzer and CodeFix assets because normal NuGet dependency flow does not guarantee transitive analyzer loading. Sonar is the sole exception: the nuspec declares the exact external dependency `SonarAnalyzer.CSharp [10.23.0.137933]`, the nupkg contains no Sonar DLL, and only the two named `buildTransitive` files may locate that exact restored DLL, add it as an `Analyzer`, and fail closed when it is absent. They must not set an `.editorconfig`, ruleset, `AdditionalFiles`, warning property, severity, package source, credential, or unrelated MSBuild property/item. PB-04 is resolved by preserving the complete current copied CodeFix provider set plus every audited host-compatible variant. P2-NUPIPE-001 selects only licensed copied analyzer DLLs, every required copied CodeFix DLL, and their runtime dependencies from audited upstream packages. Host-neutral copied assets use `analyzers/dotnet/cs/`; each upstream Roslyn-versioned set retains its distinct `analyzers/dotnet/roslyn<major>.<minor>/cs/` path. The implementation must not flatten same-name binaries from different variants. Offline package-selector fixtures must prove that each supported consumer selects exactly one compatible copied set and automatically receives the exact external Sonar analyzer; isolated Roslyn-host fixtures must load the complete copied provider/dependency closure for every retained variant. If any required copied asset or variant fails license, dependency-closure, selection, or load validation, or if the Sonar activation PoC is unstable, 001 blocks and returns for revised User approval rather than flattening, silently omitting behavior, or copying Sonar.

It must not contain or transitively introduce:

- `TreatWarningsAsErrors`, `WarningsNotAsErrors`, or `AnalysisMode`;
- `.editorconfig`, `stylecop.json`, or `AdditionalFiles`;
- references to `assets/`, `props/`, or another relative project path.

CodeAnalysis has one public behavior: selecting installed analyzers. Diagnostic severity belongs to the consumer `.editorconfig`; shared pipeline rules belong to Build resources.

## 4. Build Public Namespaces and Types

```text
TedToolkit.ModularPipelines.Build
├─ Conventions
│  ├─ PipelineConventions
│  ├─ PipelineConventionOptions
│  ├─ PipelineLayout
│  ├─ PipelineTriggerKind
│  └─ PipelineExecutionContext
├─ Inputs
│  ├─ BuildInputs
│  ├─ BuildOptions
│  ├─ BuildExecutionProfile
│  ├─ ChangeDescriptionFailureMode
│  ├─ DotNetTestCommand
│  ├─ FormatScope
│  ├─ FormatTarget
│  ├─ BuildTarget
│  ├─ TestTarget
│  ├─ PackTarget
│  ├─ DotnetPublishTarget
│  └─ BuildResult
├─ Execution
│  └─ BuildPipelineExtensions.AddBuildPipeline
├─ Artifacts
│  ├─ PipelineArtifactManifest
│  ├─ PipelineArtifact
│  ├─ PipelineArtifactKind
│  ├─ PipelineRunStatus
│  ├─ TargetExecutionResult
│  ├─ TestExecutionResult
│  ├─ PipelineFailure
│  ├─ ArtifactValidationOptions
│  ├─ IPipelineArtifactManifestReader
│  └─ IPipelineArtifactManifestWriter
├─ Resources
│  ├─ StandardResources
│  ├─ EmbeddedResourceOptions
│  ├─ PipelineResourceSource
│  ├─ PipelineResourceDocument
│  ├─ PipelineResourceWriteResult
│  └─ IPipelineResourceComposer
├─ Descriptions
│  ├─ IChangeDescriptionGenerator
│  ├─ ChangeDescriptionKind
│  ├─ ChangeDescriptionRequest
│  ├─ ChangeDescription
│  └─ GenerateCommitMessageModule
├─ Versioning
│  ├─ DailyReleaseVersionPolicy
│  ├─ ConsumedReleaseDisposition
│  ├─ ConsumedReleaseVersionRecord
│  ├─ DailyReleaseVersionRequest
│  ├─ ReleaseVersionResolutionKind
│  ├─ ReleaseVersionResolution
│  └─ TemporaryMsBuildVersionOptions
└─ Modules
   ├─ CleanStageModule<T>
   ├─ PrepareStageModule<T>
   ├─ CompileStageModule<T>
   ├─ CheckStageModule<T>
   ├─ UpdateEditorConfigModule
   ├─ CleanOutputModule
   ├─ FormatCodeModule
   ├─ DotnetBuildModule
   ├─ TestModule
   ├─ AssertBuildTestModule
   ├─ DotnetPackModule
   ├─ DotnetPublishModule
   ├─ CollectPublishArtifactsModule
   └─ WriteArtifactManifestModule
```

### 4.1 Input and Output Models

| Type | Required members | Defaults and constraints |
| --- | --- | --- |
| `BuildInputs` | `RootDirectory`, strict-descendant `ArtifactRootDirectory`, optional `MsBuildVersion`, and explicit target lists | Roots exist; every target/resource resolves inside RootDirectory; no path is guessed from process state; symlink/reparse escapes are rejected |
| `FormatTarget` | Explicit solution/project, `WorkingTreeTracked`/`HeadTracked`/`All` scope, arguments, timeout | Required only with RunFormat; `HeadTracked` is the general default, while the TedToolkit host selects legacy-compatible `WorkingTreeTracked`; no Git network access; LocalBuild-only |
| `BuildTarget` | Logical `Name`, `File`, `Configuration`, `CleanBeforeBuild`, structured `Arguments` | Configuration defaults to `Release`; optional clean uses the exact same target/configuration and gates build |
| `TestTarget` | Logical name/file, test command kind, configuration, structured command/runner arguments | Supports Microsoft.Testing.Platform through run or test and conventional VSTest through logger syntax; each target has a clean unique result directory |
| `BuildOptions` | Conventions/resources, format/generation switches, positive max parallelism, description failure mode | Format/generation are opt-in; parallelism is bounded (default at most 4) with deterministic result order; ordinary format/resource writes are LocalBuild-only |
| `PackTarget` | Logical `Name`, `File`, `PackageVersion`, `Configuration`, structured `Arguments` | Explicit `dotnet pack`; version is non-empty and equal across one run |
| `ConsumedReleaseVersionRecord` | Normalized calendar version, target source revision, and `PackageSucceeded` or `Abandoned` disposition | Caller validates provider-owned baseline/tag metadata before mapping; manifest hashes, CI run identities, annotations, and provider SDK types never enter Build |
| `DailyReleaseVersionRequest` | Caller-selected `DateOnly`, current source revision, consumed-version records, and `HasActivePublicationReservation` | Contains only version-policy facts; it carries no tag or recovery mechanism |
| `DailyReleaseVersionPolicy` | One `DailyReleaseVersionRequest` | Pure calculation; no clock/time-zone choice; `yyyy.M.d` for counter zero and `yyyy.M.d.<counter>` through counter 65534; exhaustion fails; package-success and abandoned dispositions consume counters; an active reservation blocks new Build; conflicts fail; no environment, provider, file, manifest-hash, or CI-run access |
| `TemporaryMsBuildVersionOptions` | Root-contained MSBuild XML file, property name, optional normalized target version, and root-contained recovery filename outside the artifact root | Recovery paths are supplied for every active RunBuild profile; LocalBuild/Validate/Message use null target for recovery only; non-null target is allowed only for Pack/Publish; None remains zero-call; TedToolkit selects `Directory.Build.props` and `Version`; target/recovery paths reject links/reparse escapes; all PackTargets must match |
| Internal `TemporaryMsBuildVersionTransaction` | Recover, begin, and restore operations over explicit options and validated roots | Atomic recovery file held under an exclusive lifetime lock plus atomic target replacement; concurrent runs fail safely; exact-byte restoration; recovery content is never logged/packed/uploaded; no Git/provider operation; restore failure fails the run |
| `DotnetPublishTarget` | Logical `Name`, publication `ArtifactName`, `File`, `Configuration`, optional framework/RID/self-contained, structured arguments | Generates and archives local output only; RID requires explicit self-contained choice; never pushes remotely |
| `PipelineArtifact` | Relative path, kind, logical name, SHA-256, size, optional nuspec ID/version and RID/TFM | Kinds are NuGetPackage, SymbolPackage, PublishArchive, TestResult, or FailureLog; package identity comes from nuspec |
| `BuildResult` | Status, target/test results, artifacts, failures, optional generated description | Contains no SDK/platform object; durable fields round-trip through the manifest, while generated text is same-process-only |
| `PipelineConventionOptions` | Main/development branches, Git remote name, and layout | Defaults to `main`, `development`, `origin`, and the standard layout; every Git name is validated before use |
| `PipelineLayout` | Folder/file-name properties | Defaults include `props/output/externals/nuget/test/publish/pipeline-artifacts.v1.json`; the manifest replaces legacy handoff files |

### 4.2 Resource Behavior

`IPipelineResourceComposer` provides `GetEditorConfigAsync`, `GetCommitMessageInstructionsAsync`, `GetChangeRequestInstructionsAsync`, and `WriteEditorConfigAsync` over an explicit root and `EmbeddedResourceOptions`. It returns `PipelineResourceDocument` with content, resource version, source disposition, and hash; the write method returns the document, target file, and whether bytes changed.

When `WriteEditorConfig=false`, `WriteEditorConfigAsync` returns a no-write result. When explicitly enabled, it compares hashes, leaves identical files untouched, and atomically replaces changed content to avoid meaningless Git diffs.

Each resource independently uses `Replacement > EmbeddedBase + Overlay > EmbeddedBase`. Setting its replacement and overlay together is an error. Every path resolves inside RootDirectory.

`BuildPipelineExtensions.AddBuildPipeline(PipelineBuilder, BuildExecutionProfile, BuildInputs, BuildOptions?)` is the Build-only entry point and returns the same builder. It validates inputs, repository-write compatibility, and target requirements before registering the fixed graph. Its `Publish` value means only local pack/`dotnet publish` artifact production; Build contains no remote action. Combine maps its five active same-named profiles to this enum and calls nothing for `None`.

`IPipelineArtifactManifestReader.ReadAsync` accepts the explicit consumer root, manifest file, validation options, and cancellation token. `IPipelineArtifactManifestWriter.WriteAsync` accepts the artifact root, manifest filename, fully populated manifest, and cancellation token and returns the committed manifest file. `DailyReleaseVersionPolicy.Resolve(DailyReleaseVersionRequest)` is synchronous and pure. The temporary transaction, lock, and recovery-state types are internal implementation services driven by public `TemporaryMsBuildVersionOptions` through `AddBuildPipeline`.

### 4.3 Neutral Change-description Behavior

`IChangeDescriptionGenerator` is optional. Build provides no concrete model or model key. Separate language-neutral commit and change-request instruction bases prevent commit-only rules from leaking into PR/MR summaries; repository differences use matching overlays/replacements.

Registration alone is inert. Generation runs only when `GenerateChangeDescriptions=true`, the profile permits it, and the generator exists. If it is absent, returns `null`, or is unnecessary, the module skips. `ChangeDescriptionFailureMode` defaults to `Continue`; only explicit `FailPipeline` turns an implementation exception into pipeline failure. Build never logs the diff, prompt, or generated value and has no clipboard dependency or output sink. Consumers or explicitly enabled Combine modules decide whether to persist, display, or apply a result.

### 4.4 Pack and Artifact Manifest

Before commands, Build validates all roots/targets and rejects mixed `PackTarget` versions. `DotnetPackModule` runs explicit `dotnet pack -c Release` with the shared version and output directory. It does not pass `--no-build`; `dotnet pack` remains independently correct. Original `.nupkg`/`.snupkg` files are preserved. `DotnetPublishModule` runs every declared Publish target in the Publish profile and archives each under its explicit `ArtifactName`.

`DailyReleaseVersionPolicy` recognizes normalized neutral consumed-version records shaped as `yyyy.M.d` or `yyyy.M.d.<positive-counter>`. Counter zero is represented by the three-component form because NuGet normalizes a fourth zero component away. The caller supplies the release date explicitly; the TedToolkit host uses runner-local date to preserve `DateTime.Today`, while another consumer may select a different clock policy. With no active reservation, the first run on a date receives counter zero and every later run receives one more than that date's highest consumed counter; package-success reruns increment normally, while an `Abandoned` disposition only prevents reuse of its unsafe/partial version. Counter 65534 is the largest accepted value because the same numeric components must fit assembly/file versions; a further same-day attempt fails before Build. TedToolkit's checked-in pre-cutover baseline seeds old package-success history, and the host maps only schema-valid annotated tags to later package-success/abandoned records. An active reservation returns `PublicationRecoveryRequired`; a new build never reuses its version. The host filters unrelated tags and rejects reserved-namespace conflicts before calling Build. The policy itself has no file access. The host gives a new-version result to `TemporaryMsBuildVersionTransaction` and every `PackTarget`.

For every active TedToolkit RunBuild profile, in-process version recovery runs before artifact cleanup; LocalBuild/Validate/Message pass no target version and stop the transaction after recovery. None performs no Build call, so the next active profile recovers before any producer. For Pack/Publish, the transaction then stores the original `Directory.Build.props` bytes and hashes in an atomic recovery file inside the repository root but outside `output/`, holds an exclusive lock on it, atomically writes the normalized `<Version>`, and keeps it active through pipeline-owned clean/build/test/pack. A concurrent invocation fails while the lock is held; a later invocation may recover after an interrupted process releases it. The recovery file is excluded from logs, packages, and uploaded artifacts. The outer non-packable `dotnet run` bootstrap may compile before recovery but contributes no package/manifest output; the nested clean/rebuild after recovery is authoritative.

For `yyyy.M.d`, Build/Combine `AssemblyVersion` and `AssemblyFileVersion` equal `yyyy.M.d.0`; for `yyyy.M.d.<counter>`, both equal that numeric version. `AssemblyInformationalVersion` uses the normalized package version as its core and may add only source-revision metadata. CodeAnalysis is checked through nuspec only. The success path is `Recover → Clean artifacts → Begin version transaction → Compile → Test → Assert → Pack/Publish → Collect → Restore version → Manifest`. Failure and cancellation edges skip dependent producers but still run `Restore version → Manifest` with a separate cleanup token. The manifest is accepted only after exact-byte restoration and recovery-file deletion; restoration failure makes it Failed. A later configured invocation recovers surviving state before cleaning artifacts and refuses to overwrite a target that matches neither recorded hash. The workflow never stages or commits the temporary difference.

SchemaVersion is exactly integer `1`. The manifest records status, source revision/tree-dirty state, package version, target/test results, failures, and typed artifacts. The manifest is a root-level file whose parent is the artifact root in both processes. Paths and JSON obey the exact deterministic/relational safety contract; files validate existence, size, and SHA-256; package identity comes from nuspec. Source mismatch fails unless explicitly allowed. Any remote mutation additionally requires a successful, clean, revision-bearing manifest. It contains no secret, query value, username, environment snapshot, generated text, or absolute path.

Official command constraints are pinned to the .NET documentation: [`dotnet pack` builds by default](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-pack), and a RID publish must make the self-contained choice explicit as described by [`dotnet publish`](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-publish).
The current TUnit shape follows its official [TRX extension contract](https://tunit.dev/docs/extending/built-in-extensions/). Runner selection and argument placement follow the .NET 10 [`dotnet test` contract](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-test); MTP test mode requires a root-contained runner selection, while the executable run mode remains independent of it.

## 5. Combine Public and Internal Structure

```text
TedToolkit.ModularPipelines.Combine
├─ Configuration/
│  ├─ PipelineConfiguration
│  ├─ PipelineConfigurationLoader
│  ├─ CombineOptions
│  ├─ PipelineExecutionPolicy
│  ├─ PipelineProfile
│  ├─ PipelineInputMode
│  ├─ PipelineActionOptions
│  ├─ PipelineExtensionKind
│  ├─ PipelineModuleRegistration
│  ├─ NuGetPushOptions
│  ├─ NuGetAuthenticationMode
│  ├─ PackagePublicationCheckpointContext
│  ├─ PackagePublicationCheckpointRecord
│  ├─ IPackagePublicationCheckpoint
│  ├─ PublicationOptions
│  ├─ ChangeRequestOptions
│  ├─ NotificationFailureMode
│  ├─ RepositoryProviderKind
│  ├─ IPipelineSecretResolver
│  ├─ GitHubAuthenticationMode
│  ├─ GitHubConnectionOptions
│  ├─ GitLabAuthenticationMode
│  └─ GitLabConnectionOptions
├─ Models/
│  ├─ RepositoryContext
│  ├─ CommitSummary
│  ├─ ChangeRequest
│  ├─ ChangeRequestState
│  ├─ CreateChangeRequestRequest
│  ├─ Release
│  ├─ ArtifactPublicationRequest
│  ├─ PublishedArtifact
│  ├─ PackagePushResult
│  ├─ ReleasePublicationRequest
│  ├─ ReleasePublicationResult
│  ├─ OperationDisposition
│  └─ PipelineRunResult
├─ Events/
│  ├─ PipelineEvent
│  ├─ PipelineEventKind
│  └─ IPipelineEventSink
├─ Execution/
│  ├─ Internal/StandardProfileResolver
│  ├─ Internal/CombineOptionsValidator
│  ├─ ReleaseFinalizationOptions
│  ├─ IReleaseFinalizer
│  └─ AddStandardPipeline extension
├─ Modules/
│  ├─ ReleaseStageModule<T>
│  └─ Internal/
│     ├─ NuGetPushModule
│     ├─ PublishReleaseModule
│     ├─ CreateChangeRequestModule
│     ├─ UpdateChangeRequestModule
│     ├─ EmitPipelineEventsModule
│     └─ CreateReleaseModule
└─ Providers/
   ├─ Internal/IRepositoryProvider.cs
   ├─ Internal/PipelineProviderException.cs
   ├─ GitHub/GitHubRepositoryProvider.cs
   └─ GitLab/GitLabRepositoryProvider.cs
```

The `Internal` execution/module branches and `Providers/Internal` are assembly-internal. `ReleaseStageModule<T>` is the public Combine stage marker for extensions; standard action modules are implementation details. Consumers use unified models but cannot implement replacement providers through provider SDK types. A third platform requires a later ADR and is not a public extension point in this change.

`IReleaseFinalizer` is a separate provider-neutral Combine API for an explicitly authorized Release-only retry after a caller has already proved package success. It accepts `ReleaseFinalizationOptions` containing the selected `RepositoryProviderKind`, the empty-artifact `ReleasePublicationRequest`, and exactly the selected GitHub or GitLab connection options; the unselected connection must be null. It validates normalized version/tag/target/title/body plus provider configuration, resolves only that provider credential, and creates or reuses the Release through the internal provider. It runs no pipeline profile, Build, package push, artifact upload, change request, notification, or tag cleanup. Consumer policy owns audit evidence and may delete its pending marker only after the finalizer succeeds.

### 5.1 Combine Execution Order

```text
Resolve explicit or provider-neutral execution context
  → StandardProfileResolver or ManualProfile
  → validate registration metadata, input mode, repository-write compatibility, and the complete profile/action matrix
  → compose the selected fixed Build graph for RunBuild, or the manifest-reader boundary for ConsumeArtifacts
  → invoke matching trusted registration delegates once, after public Build stages exist for RunBuild or after the manifest-reader boundary exists for ConsumeArtifacts, and before provider modules
  → register only enabled event sinks and only the selected provider modules required by enabled actions
  → execute the graph: RunBuild produces the final manifest, while ConsumeArtifacts structurally reads and validates it
  → stop every remote action when manifest status is Failed
  → execute enabled change-request/package/artifact/Release actions idempotently
  → emit the terminal outcome under the sink-failure contract
```

Consumer registrations precede provider modules and declare profiles, BuildStage/CombineHook kind, repository-write metadata, and a trusted configure delegate. Metadata is validated before invocation. BuildStage is rejected for ConsumeArtifacts; repository-write registrations may name only LocalBuild; None invokes none. Extensions depend only on public Build stages or Combine hooks, never provider module types. Notifications use `IPipelineEventSink`. EverythingButTheSink's props module is a LocalBuild writing BuildStage registration; its Feishu adapter is a separate Message sink.

`PipelineInputMode.RunBuild` is the local default. Active RunBuild accepts LocalBuild, Validate, Pack, Publish, or Message, requires `BuildInputs`, and rejects a manifest path. Active ConsumeArtifacts accepts only Validate, Publish, or Message, requires a manifest path, rejects `BuildInputs`, and registers no clean/build/test/pack/publish module. None reads neither input. Standard policy preserves an explicitly configured input mode and never changes it merely because CI is detected. Composition always receives one explicit absolute consumer root; relative configuration path strings resolve against it, while direct code-model FileInfo/DirectoryInfo values must already be absolute.

The public entry point is contractually equivalent to:

```csharp
public static PipelineBuilder AddStandardPipeline(
    this PipelineBuilder builder,
    DirectoryInfo rootDirectory,
    CombineOptions combineOptions,
    BuildInputs? buildInputs = null,
    BuildOptions? buildOptions = null,
    IEnumerable<PipelineModuleRegistration>? moduleRegistrations = null);
```

For an active RunBuild profile, `buildInputs.RootDirectory` equals `rootDirectory`, and Build writes the manifest directly under its artifact root. For an active ConsumeArtifacts profile, `ArtifactManifestPath` resolves inside the same root; its regular non-link file's parent is the artifact root, and the reader uses `ArtifactValidation`. ConsumeArtifacts rejects Build-stage registration. The one resolved `buildOptions` value supplies conventions to both Build and Combine and contains Build resources plus opt-in neutral description behavior; `CombineOptions` has no second conventions property. `CombineOptions.ExecutionContext`, when present, is the explicit neutral context and wins over environment readers. Manual requires `ManualProfile`; Standard rejects it.

### 5.2 Remote-action Switches

| Switch | Provider required | Behavior | Default |
| --- | --- | --- | --- |
| `PushNuGetPackages` | No; uses Combine package publisher and secret resolver | Push manifest nupkg files to configured source | false |
| `PublishArtifacts` | Yes | Upload GitHub Release assets or GitLab Generic Packages through provider-controlled ordering | false |
| `CreateChangeRequest` | Yes | Create or update PR/MR | false |
| `CreateRelease` | Yes | Create GitHub/GitLab Release | false |
| `EmitNotifications` | No; zero registered sinks is a valid no-op | Dispatch neutral events to registered `IPipelineEventSink` instances | false |

The compatibility matrix is normative:

| Profile | Push NuGet | Publish artifacts | Change request | Release | Notifications |
| --- | --- | --- | --- | --- | --- |
| `LocalBuild` | Forbidden | Forbidden | Forbidden | Forbidden | Allowed |
| `Validate` | Forbidden | Forbidden | Forbidden | Forbidden | Allowed |
| `Pack` | Forbidden | Forbidden | Forbidden | Forbidden | Allowed |
| `Publish` | Allowed | Allowed | Forbidden | Allowed | Allowed |
| `Message` | Forbidden | Forbidden | Allowed | Forbidden | Allowed |
| `None` | Forbidden | Forbidden | Forbidden | Forbidden | Forbidden |

An enabled forbidden switch or incompatible repository-write option fails before secret resolution, provider/client creation, Build commands, callbacks, network calls, or sinks. A failed manifest also gates every mutation. `EmitNotifications=true` with no sink is a valid no-op.

`PublicationOptions` supplies a required explicit version plus tag, title, and body whenever artifact or Release publication is enabled. Publish archives carry per-artifact logical names from `DotnetPublishTarget.ArtifactName`; only PublishArchive entries are uploaded, while NuGet push selects NuGetPackage entries. Version matches the normalized manifest package version when one exists; a publish-archive-only manifest has no package version, so the normalized explicit publication version is authoritative. Target revision is the successful manifest revision, and prerelease status is derived from the publication version. Provider operations query before retry, reuse only hash-matching remote files, fail on mismatch, never silently overwrite, and never blindly retry non-idempotent mutations. GitHub creates a draft when no Release exists after first validating that the tag is absent or targets the requested revision; failed drafts remain resumable, while an already published Release is never redrafted and receives only verified-missing assets. Artifact-only GitHub publication requires a Release, not merely a tag. GitLab reruns reuse verified Generic Packages before retrying a failed Release.

`NuGetPushOptions` uses `ApiKey` or `NuGetConfig`, optional symbol source, logical credential references, optional config-file path, `SkipDuplicate=false` by default, `UsePackagePublicationCheckpoint=false` by default, and explicit insecure-HTTP opt-in. `ApiKey` resolves only immediately before an enabled push; `NuGetConfig` forbids references and uses the noninteractive NuGet credential/configuration chain. Source URLs reject user information, query, and fragment. Each selected primary nupkg is pushed once; its matching sibling snupkg is handled by the same command. `SkippedDuplicate` is an explicit disposition, not hash proof. A generic consumer may opt in while accepting that limitation. TedToolkit never enables duplicate skipping.

When `UsePackagePublicationCheckpoint=true`, Publish plus NuGet push must have `SkipDuplicate=false` and exactly one explicitly registered trusted `IPackagePublicationCheckpoint`; checkpointing plus duplicate skipping fails configuration before callbacks, secrets, or commands. Combine creates `PackagePublicationCheckpointContext` from the validated manifest SHA-256, source revision, normalized version, ordinal-ignore-case-unique exact package IDs, nupkg hashes, and optional sibling snupkg hashes. Before resolving the NuGet secret, it reads completed IDs, rejects a null/unknown/duplicate/case-conflicting result, and removes only those packages from the request. NuGet credentials resolve only immediately before a remaining command; an all-completed read makes no NuGet resolver call. After each remaining command returns confirmed success it records that package before starting the next. When the complete context package set is accounted for, Combine invokes `CompleteAsync` exactly once—even if read initially returned every ID—and does not start provider artifact/Release publication until it succeeds. Checkpoint failure stops publication; an already-pushed but unmarked package is deliberately treated as uncertain. The callback is never invoked when the option/action/profile is absent or invalid and receives no secret/provider type. TedToolkit's non-packable host implements read/record through immutable Git progress tags and completion through full-set validation plus package-success tag creation/reuse. See the official [`dotnet nuget push` option contract](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-nuget-push) and [symbol-package push behavior](https://learn.microsoft.com/en-us/nuget/create-packages/symbol-packages-snupkg).

Change requests route development to main and every other non-main branch to development; main is invalid for Message. Generation is an optional enhancement enabled separately. Without generated text, the deterministic title is `ChangeRequestOptions.ReleaseTitle` (default `Release`) or `Merge {source} into {target}`, and the body is empty except for preserved issue-closing directives. Draft defaults to true; labels are explicit; existing open requests are reused/updated.

### 5.3 Standard Context Resolution

`PipelineExecutionContext` carries only neutral trigger, CI, repository/run URLs, branch, source/before revisions, actor, and compare data. Explicit `CombineOptions.ExecutionContext` wins. Otherwise exactly one internal environment reader may map GitHub Actions or GitLab CI; dual markers and inconsistent/invalid context fail, while generic `CI=true` alone does not infer a provider. `Branch` is populated only from a branch ref. A GitHub tag push keeps `Trigger=Push` but has `Branch=null`; its tag name is never mapped as a branch. Standard maps local to LocalBuild, branch push on main to Publish, branch push on non-main to Message, manual/reusable call on main to Publish, and every other context—including every tag push—to None. A tag-triggered workflow therefore invokes no pipeline-owned Build command, release-lifecycle read/mutation, provider, secret resolver, callback, or sink and cannot feed back from automation-owned tags. Diff is read only for enabled generation; commit summaries are read only for enabled registered sinks, never through a network fetch, are capped at the newest 200 in chronological order, and report truncation. Full CI history is required for either capability. Branch/ref values are structured, never shell-interpolated.

## 6. Package Configuration Keys

```json
{
  "TedToolkit": {
    "Build": {
      "Conventions": {
        "MainBranch": "main",
        "DevelopmentBranch": "development",
        "GitRemoteName": "origin"
      },
      "Resources": {
        "EditorConfigOverlayPath": "build/editorconfig.overlay",
        "CommitMessageOverlayPath": "build/commit-message.overlay.md",
        "ChangeRequestOverlayPath": "build/change-request.overlay.md"
      },
      "RunFormat": false,
      "GenerateChangeDescriptions": false,
      "MaxDegreeOfParallelism": 4,
      "ChangeDescriptionFailureMode": "Continue"
    },
    "Pipeline": {
      "ExecutionPolicy": "Standard",
      "InputMode": "ConsumeArtifacts",
      "ArtifactManifestPath": "output/pipeline-artifacts.v1.json",
      "ArtifactValidation": {
        "AllowSourceRevisionMismatch": false
      },
      "Actions": {
        "PushNuGetPackages": false,
        "PublishArtifacts": false,
        "CreateChangeRequest": false,
        "CreateRelease": false,
        "EmitNotifications": false,
        "NotificationTimeout": "00:00:30",
        "NotificationFailureMode": "Continue"
      },
      "NuGetPush": {
        "Source": "https://api.nuget.org/v3/index.json",
        "AuthenticationMode": "ApiKey",
        "CredentialReference": "NUGET_API_KEY",
        "SkipDuplicate": false,
        "UsePackagePublicationCheckpoint": false,
        "AllowInsecureHttp": false,
        "Timeout": "00:05:00"
      },
      "Publication": {
        "Version": "2026.7.29.1",
        "TagName": "2026.7.29.1",
        "Title": "TedToolkit 2026.7.29.1",
        "Body": ""
      },
      "ChangeRequest": {
        "ReleaseTitle": "Release",
        "Draft": true,
        "Labels": [],
        "PreserveIssueClosingDirectives": true
      },
      "RepositoryProvider": "GitLab",
      "GitLab": {
        "InstanceUrl": "https://gitlab.example.com",
        "ApiUrl": "https://gitlab.example.com/api/v4",
        "ProjectId": 123,
        "AuthenticationMode": "JobToken",
        "AllowInsecureHttp": false,
        "RequestTimeout": "00:02:00"
      }
    }
  }
}
```

Non-secret environment variables use the same double-underscore hierarchy, for example `TedToolkit__Pipeline__GitLab__ApiUrl` and `TedToolkit__Build__Resources__EditorConfigOverlayPath`. The reference host composes `IConfiguration` with JSON first and the standard environment-variable provider second, without passing a provider `prefix` argument; this preserves the leading `TedToolkit` section and lets those variables override the matching JSON keys. `PipelineConfigurationLoader` still reads only the two package-owned subtrees, so unrelated process variables are ignored. Using `AddEnvironmentVariables(prefix: "TedToolkit__")` here is explicitly incorrect because that API strips the prefix and would produce `Pipeline:*`/`Build:*` keys outside the loader's sections. Alternatively, a consumer constructs `BuildOptions`/`CombineOptions` directly and passes them to `AddStandardPipeline`; those explicit objects are authoritative and are not partially merged with bound values. GitLab CI and GitHub Actions variables fill only missing provider fields and never override explicit typed or bound package configuration.

Public options contain only logical credential references, never credential values. Public `IPipelineSecretResolver` defaults to environment lookup and may be replaced by a consumer secret store; it is invoked only when an owning action with a logical reference is enabled. ActionsToken and JobToken instead read only `GITHUB_TOKEN` or `CI_JOB_TOKEN` through their internal CI environment reader for an enabled owning action. The optional public `PipelineConfigurationLoader.Load(IConfiguration)` returns `PipelineConfiguration(BuildOptions Build, CombineOptions Pipeline)`. It reads only the package-owned `TedToolkit:Build` and `TedToolkit:Pipeline` sections from the already composed configuration; it performs no file, environment, or secret lookup itself. Before binding, it walks both raw key trees, rejects unknown keys, and rejects any non-empty `Token`, `ApiKey`, `Password`, or `Secret` property case-insensitively without echoing the value. It performs key/conversion/shape validation only; `AddStandardPipeline` retains profile/action-dependent semantic validation so a disabled capability requires no related settings. This makes an inline JSON/environment secret a deterministic startup error instead of relying on the default binder to ignore it. Model and notification-adapter secrets belong entirely to consumer configuration outside the TedToolkit-owned sections and never enter package options, examples, manifests, or logs.

### 6.1 Migration Mapping

There is no public or runtime compatibility shim. Before the old host binding is deleted, a checked-in fixture captures its effective non-secret options and compares them with the new host configuration. The current TedToolkit Build host then migrates to these keys; EverythingButTheSink is evidence only and is not changed. Strict binding rejects obsolete package-owned keys.

| Current concern | New owner |
| --- | --- |
| `DotNet.Configuration` | Configuration on each Build/Test/Pack/Publish target |
| `DotNet.Format` | `Build.RunFormat` |
| inverse `SkipUpdateEditorConfig` | `Build.Resources.WriteEditorConfig` |
| `LoadLocalConditionString` / props generation | Consumer-owned LocalBuild Prepare extension |
| `AI.*` | Consumer `IChangeDescriptionGenerator` configuration outside package sections |
| NuGet source/API key | `Pipeline.NuGetPush.Source` plus logical credential reference |
| NuGet project/release URL | Consumer release body or repository metadata |
| GitHub workflow profile/release selection | Explicit host options supplied by the workflow; provider environment fallback remains internal to Combine for provider-owned actions |
| tracked-file version bump | Split into Build's pure `DailyReleaseVersionPolicy` plus `TemporaryMsBuildVersionTransaction`; the latter preserves compiled-version stamping, restores the original file byte-for-byte, and never stages or commits the temporary difference |
| `Version.txt`, failure-list handoff | Removed; the manifest carries the runtime package version, status, and results |
| EverythingButTheSink Feishu settings | Consumer `IPipelineEventSink` |
| EverythingButTheSink GitLab token | GitLab authentication mode plus logical credential reference |
| direct enumeration/deletion of output folders | Validated manifest and typed artifact selection |

### 6.2 TedToolkit Build-host Compatibility Configuration

`Build/Build.csproj` remains the repository command-line entry point and directly `ProjectReference`s both Build and Combine. Its host configuration is explicit:

- repository root: the current repository root;
- pre-cutover release baseline: checked-in `Build/release-history.v1.json`, exact schema 1, containing only sorted normalized package-version/exact legacy tag-name/target-revision mappings obtained by a one-time read-only GitHub Release query and verified against complete local tags at cutover; schema and generation rules are design-approved, while the generated contents plus their SHA-256 and non-secret query/verification evidence require User acceptance in P2-NUPIPE-003 completion evidence; accepted contents are read-only at runtime, contain no URL or credential, and any later correction requires a separately approved audited data-correction change;
- build target: `TedToolkit.slnx`, `Release`, `CleanBeforeBuild=true`;
- format target: `TedToolkit.slnx`, `FormatScope.WorkingTreeTracked`, enabled only by the migrated equivalent of the current format option;
- resource writing: disabled for the TedToolkit host because the current `Build/Program.cs` does not register the editor-config update module;
- test targets: none until the repository intentionally adds a test project to the current host inventory;
- pack targets: `TedToolkit.CodeAnalysis`, `TedToolkit.ModularPipelines.Build`, and `TedToolkit.ModularPipelines.Combine`, all receiving one required coordinated release version;
- temporary MSBuild version target: repository-root `Directory.Build.props`, property `Version`, and repository-root recovery file `.tedtoolkit-release-version.recovery`; every active RunBuild profile supplies paths, LocalBuild/Validate/Message use no target version, coordinated Pack/Publish may supply it, and None remains zero-call; the root `.gitignore` contains the exact anchored rule `/.tedtoolkit-release-version.recovery`; the recovery file is transient, never packed/uploaded/committed, and retained only when safe automatic restoration cannot complete;
- artifact root: `output/`, preserving original nupkg/snupkg files and the final manifest;
- NuGet push: the configured provider-neutral source and logical credential reference, disabled unless `PushNuGetPackages=true`;
- GitHub Release: enabled in the gated TedToolkit publication job as an approved correction to the current entry point; it reuses the annotated package-success tag, uses the normalized version as tag/title, and is idempotently finalized before pending deletion; the non-packable repository host owns an exact three-entry package-page-link map keyed by the expected PackageIds, validates every value as an absolute HTTPS URI without user information/query/fragment, and constructs the Release body from those links rather than asking Combine to infer a feed UI URL; exact body whitespace/date wording is not a compatibility boundary;
- description adapter/presenter: optional host-only dependencies outside all packable projects, with continuation on absence or failure.

`Build/release-history.v1.json` has the exact shape below. It is UTF-8 without BOM, rejects unknown/duplicate properties and duplicate normalized versions or exact tag names, requires normalized calendar package versions, canonical legacy calendar tag names, and lowercase full Git object IDs (40 or 64 hexadecimal characters), and sorts records by normalized version then ordinal tag name then target revision. `tagName` preserves the exact pre-cutover GitHub tag while `version` is its NuGet-normalized consumed identity: legacy `yyyy.M.d.0` and `yyyy.M.d` both map to `yyyy.M.d`, while a positive fourth component remains. Every fetched `tagName` must peel to `targetRevision`; a reserved-pattern tag absent from both this baseline and a valid post-cutover annotation is a conflict.

The one-time capture is a non-packable P2-NUPIPE-003 migration command using the repository's already-present GitHub client boundary and a read-only built-in GitHub credential; it adds no candidate P2-NUPIPE-004 dependency and is removed or made unreachable after User acceptance. It enumerates every GitHub Release rather than trusting provider ordering or only the first result and ignores unrelated tag namespaces. Every accepted non-draft Release tag must be a canonical legacy calendar tag (`yyyy.M.d`, `yyyy.M.d.0`, or `yyyy.M.d.<positive-counter>` with no leading-zero components), must exist in the complete local tag set, and must peel to the recorded target. Capture stores its exact string as `tagName` and its NuGet-normalized identity as `version`. Pagination must be exhausted. A draft using the reserved calendar namespace, a calendar-like but noncanonical/malformed tag, duplicate normalized version or exact tag, missing/moving tag, or inconsistent target fails capture. Completion evidence records the UTC query time, repository identity, page/total/accepted/ignored counts, existing client/tool version, generated file SHA-256, and a secret-free comparison result; it records neither the credential nor raw authenticated responses. User acceptance must cite the implementation commit containing that exact baseline and hash. Runtime and later builds contain no capture command or API call. Runtime version selection intentionally uses the highest validated consumed counter for a date instead of legacy `releases[0]`; monotonic valid history is equivalent, while out-of-order or conflicting history fails safely.

```json
{
  "schemaVersion": 1,
  "releases": [
    {
      "version": "2026.7.28",
      "tagName": "2026.7.28.0",
      "targetRevision": "0123456789abcdef0123456789abcdef01234567"
    }
  ]
}
```

Host-only environment configuration lives outside the package-owned `TedToolkit:Build` and `TedToolkit:Pipeline` sections:

- `TedToolkitHost__Profile`: required in CI as `Validate` or `Publish`;
- `TedToolkitHost__InputMode`: `RunBuild` for validation/build/pack and `ConsumeArtifacts` for the separate push job;
- `TedToolkitHost__ReleaseVersion`: required for main `RunBuild` Publish and passed to all three PackTargets; also required as the exact lifecycle version selected by either audited recovery action;
- `TedToolkitHost__ArtifactManifestPath`: required for `ConsumeArtifacts`;
- `TedToolkitHost__PushNuGetPackages`: true only in the gated publication job;
- `TedToolkitHost__UsePackagePublicationCheckpoint`: true only in that same job; it requires the TedToolkit immutable Git-tag checkpoint and keeps `SkipDuplicate=false`;
- `TedToolkitHost__CreateRelease`: true in that same TedToolkit publication job; other consumers retain Combine's default-off neutral action;
- `TedToolkitHost__RecoveryAction`: absent normally; accepted values are `FinalizeRelease` and `AbandonPublication`. `FinalizeRelease` requires matching package-success-plus-pending with no abandoned marker, creates/reuses the matching finalized audit marker after Release success, and deletes pending only afterward; matching finalized-plus-success-plus-pending performs cleanup without another provider call. `AbandonPublication` requires matching pending with no package-success/finalized marker and creates/reuses the matching abandoned marker before pending deletion. Both disable Build, package push, artifact publication, change request, and notifications;
- `TedToolkitHost__AuditReference`: required for either recovery action and constrained to the same opaque non-secret ID syntax as abandonment.

The host validates these values and constructs the neutral package options; the public libraries do not infer TedToolkit release policy from GitHub variables. For a main release, checkout fetches complete Git tag history. The repository host validates each pre-cutover normalized-version/exact-tag/target mapping and every reserved post-cutover lifecycle annotation against that history. It maps only package-success/abandoned version, target revision, and disposition to `ConsumedReleaseVersionRecord`, maps valid pending presence to `HasActivePublicationReservation`, and keeps package-progress/finalized/audit metadata entirely in host lifecycle state. It supplies only those neutral records, the reservation Boolean, runner-local `DateOnly`, and source revision to `DailyReleaseVersionPolicy`. Manifest hashes, run identities, exact legacy tag strings, and tag annotations remain outside Build. No GitHub API query is required for runtime version resolution. It places a new normalized version in `TedToolkitHost__ReleaseVersion`; `PublicationRecoveryRequired` stops the build and directs the operator to retry the owning publication job or complete the applicable audited recovery procedure. Every main version-resolving workflow and both manually dispatched recovery actions use one stable repository-scoped release concurrency group with `cancel-in-progress: false` and `queue: max`; the workflow-level lock starts before lifecycle-state read and remains held through build, publication, and the final tag mutation or cleanup, while queued triggers are retained. Non-main validation uses a separate group. Resolution failure or recovery-required occurs before pipeline-owned Build commands, pack, or NuGet secret access.

Pending, package-progress, package-success, abandoned, and manual-finalization marker-tag mutation is repository-host/workflow policy, not a Build or Combine public tag action. Pending is an annotated tag whose exact LF-delimited message is `tedtoolkit-nuget-pending-v1`, `manifest-sha256=<lowercase-sha256>`, and `run-identity=github:<GITHUB_RUN_ID>`; it targets the manifest source revision. `GITHUB_RUN_ID`, rather than the attempt number, keeps the identity stable when the owning workflow run's publication job is retried. TedToolkit registers one `IPackagePublicationCheckpoint`: it maps confirmed packages to immutable `nuget-package/<version>/<lowercase-package-id>` tags at the same revision; the tag segment is the exact expected PackageId converted with `ToLowerInvariant()` and validated as a legal Git ref component before any push. The tag has exact LF-delimited `tedtoolkit-nuget-package-v1`, exact nuspec `package-id`, `package-sha256`, `symbol-sha256=<hash-or-none>`, matching `manifest-sha256`, and `run-identity`. Its read method accepts only a subset of the expected package IDs with matching target and every field; its record method creates/reuses exactly one matching tag after command success and never moves one. Combine skips matching marked packages, pushes remaining packages with `SkipDuplicate=false`, and invokes the record method before the next push. Once the complete expected set is accounted for, Combine invokes the completion method; the host revalidates all expected progress tags and creates/reuses the matching package-success tag before the callback returns, including on an all-marked retry. Only then may Combine create the GitHub Release. An unmarked duplicate/uncertain completion creates no progress marker and never authorizes package success. The package-success calendar tag uses `tedtoolkit-nuget-success-v1` plus the identical manifest/run fields; it does not by itself prove Release finalization, so pending remains. If the original artifact becomes irrecoverable before package success or an unmarked result is unverifiable, an operator invokes the host-only `AbandonPublication` action with an audit reference. It creates/reuses annotated `nuget-abandoned/<version>` at the same revision with `tedtoolkit-nuget-abandoned-v1`, the copied manifest/run fields, and `audit-reference=<opaque-id>`, pushes it, and only then deletes pending. The action performs no Build, package push, provider Release/artifact action, change request, or notification. Valid progress tags remain immutable partial-publication evidence. Pending alone anchors a valid partial set; package-success or abandoned anchors retained evidence after pending cleanup; pending plus either marker is the corresponding interrupted cleanup state. Package-success and abandoned together, or progress with none of those anchors, is a hard conflict. The opaque ID matches `[A-Za-z0-9][A-Za-z0-9._-]{0,127}` and contains no credential or URL. The abandoned marker consumes the version but authorizes no success. Matching package-success-plus-pending validates target/fields/progress set and finalizes the required Release if needed. In the manual path, Release success is followed by immutable annotated `nuget-finalized/<version>` at the same revision with exact header `tedtoolkit-nuget-finalized-v1`, copied `manifest-sha256` and original `run-identity`, `recovery-run-identity=github:<marker-creating-recovery-run-id>`, and the audit reference; only then may the host delete pending. Matching finalized-plus-success-plus-pending validates every field, preserves the marker-creating recovery identity instead of comparing it with the retry's current run ID, and deletes pending without another provider call. Finalized-plus-success without pending is a terminal audited recovery state; finalized without success or together with abandoned is a conflict. Matching abandoned-plus-pending validates target/fields and deletes pending only. Package-success-only, finalized-plus-success-only, and abandoned-only are terminal states; rerunning normal publication or either recovery action validates the applicable state and returns without recreating pending or calling a provider. An abandoned version never resumes package publication. The workflow uses the checked-out authenticated remote without embedding a credential in a URL or logged command, validates every tag target and field, rejects unknown or duplicate fields, and never moves an existing package-progress, package-success, finalized, or abandoned tag.

The non-packable TedToolkit host owns the audit and tag-evidence policy for `FinalizeRelease`, but no GitHub API implementation. It is used only if packages and the annotated package-success tag completed but the required GitHub Release/pending cleanup did not and the original CI artifact has expired. The host validates matching package-success/pending target, manifest hash, and run identity; requires the opaque audit reference; forbids every Build, package, asset, change-request, and notification action; constructs an empty-artifact neutral `ReleasePublicationRequest`; and calls Combine's `IReleaseFinalizer`. Combine resolves only the selected internal GitHub provider and idempotently queries/creates the Release for the existing tag. After that call succeeds, the host creates or validates the finalized audit marker and only then deletes pending. The marker records the run that first created it; a later cleanup run validates that preserved identity and the exact same audit reference but does not require the preserved run ID to equal its own or rewrite the marker. Neither layer changes the package-success tag or claims an abandoned version. Normal publication still uses Combine and the validated manifest but creates no manual-finalization marker. Every handled completion of either manual recovery action writes one sanitized GitHub job-summary record containing the current recovery run identity, action, normalized version, opaque audit reference, validated pre/post lifecycle states, and terminal disposition; it contains no credential, authenticated URL, raw provider response, or package content. Hard runner/process termination may prevent that auxiliary summary, so durable audit comes from the immutable finalized or abandoned marker and the still-retryable pending state.

The existing GitHub workflow keeps its triggers, Build-project entry point, artifact name, and `output/` path. It splits validation/build from credentialed publication:

| GitHub job/context | Profile/actions and credential boundary |
| --- | --- |
| Non-main push/manual/reusable | Run `Build/Build.csproj` with explicit Validate, `contents: read`, and no AI, NuGet, or publication credential |
| Main build/pack | Under the shared workflow-level repository release concurrency group, capture `NUGET_RELEASE_ENABLED` once as the Boolean `release-enabled-at-build` job output; fetch complete Git tags with read-only checkout access; validate the checked-in pre-cutover baseline and parse schema-valid post-cutover tags; block when active pending requires publication recovery; resolve `yyyy.M.d[.<counter>]` from runner-local date and package-success/abandoned consumed counters; recover any interrupted version transaction; temporarily stamp `Directory.Build.props`; run explicit Publish with remote actions false; pack all three projects under that runtime version; restore the original bytes before writing/uploading `output/` as `build-output`; no NuGet credential and no staged or committed version difference. False-captured artifacts remain non-publishable; first enablement requires a fresh full build |
| Main package publication | Depend on a successful main build/pack job and run only when its original `release-enabled-at-build` output is exactly true; do not re-read the repository variable. Download and validate `build-output`; invoke the same host in `ConsumeArtifacts` Publish mode with `PushNuGetPackages=true`, `UsePackagePublicationCheckpoint=true`, `SkipDuplicate=false`, and `CreateRelease=true`; validate the exact version's lifecycle before credentials or pending creation, returning terminal no-op for matching package-success-only/finalized-plus-success-only/abandoned-only. Matching package-success-plus-pending resolves no NuGet credential, finalizes Release, and deletes pending without rebuilding or package push; after artifact expiry the separate audited recovery uses immutable host evidence plus Combine's `IReleaseFinalizer`, then finalized audit-marker creation, then pending deletion. Matching finalized-plus-success-plus-pending or abandoned-plus-pending resolves no NuGet/provider credential and performs pending-deletion-only cleanup. For an active pre-success attempt, create/reuse annotated pending metadata; checkpoint-read matching immutable package-progress tags before NuGet secret resolution; resolve the NuGet credential only immediately before the first unmarked package command; push only unmarked packages in deterministic order and checkpoint-record each confirmed success before the next; reject every unmarked duplicate/uncertain disposition; invoke checkpoint completion after the full set to create/reuse package success, then and only then create/reuse the GitHub Release and delete pending. If the original artifact is irrecoverable or an unmarked package result is unverifiable before package success, a separate audited operator procedure creates immutable abandoned before deleting pending; it performs no Build/package push and the next run increments past that version. Abandonment is forbidden after package success |
| Manually dispatched audited recovery | Enter the same repository release concurrency group as main before reading lifecycle tags; require an explicit version, `TedToolkitHost__RecoveryAction`, and `TedToolkitHost__AuditReference`; fetch complete tags and validate exact lifecycle state before mutation. Matching abandoned-only, package-success-only, or finalized-plus-success-only returns the corresponding terminal result with zero mutation. Otherwise `AbandonPublication` receives only `contents: write`, requires pending without package success/finalized, creates/reuses abandoned before deleting pending, and never receives a NuGet credential. `FinalizeRelease` receives the GitHub Release permission needed by Combine's internal provider, requires package-success-plus-pending, creates/reuses the Release, creates/reuses finalized evidence, and then deletes pending; matching finalized-plus-success-plus-pending receives no provider credential and deletes only. Every handled path writes one sanitized job-summary record with run/action/version/reference/pre-state/post-state/disposition only; hard termination may omit the summary but leaves immutable evidence plus pending for retry. Neither action runs Build, pushes a package/artifact, creates a change request, or emits a notification |

`NUGET_RELEASE_ENABLED` is a repository-owned non-secret gate that defaults false. The User approves its one-time enablement before the first external push; a fresh full main workflow then captures true at build-job start, and every later successful main run publishes automatically while preserving the existing trigger set without per-run approval. A publication-job-only rerun uses the original captured output and cannot upgrade a false-captured artifact. A successful full rerun receives the next counter. Any active pending state blocks new builds until the original publication job completes package/Release finalization or, before package success only, an explicitly audited operator converts it to an immutable abandoned marker; expired/missing original artifacts cannot be silently rebuilt. The Build/pack job needs no write permission. The publication job receives no AI credential and only the NuGet and built-in GitHub credentials required for package/tag/Release publication. Combine's provider Release action remains generally governed by `CreateRelease` and default-off; the TedToolkit host explicitly enables it as an approved corrected outcome, and it is not the version counter's state store.

The compatibility fixture must pass before the old monolithic reference, old keys, or old workflow mapping are removed. It executes the actual project-referenced host and compares effective target/profile/action/source/output/exit semantics without storing secret values. Separate package-only consumers prove the two direct PackageReferences. The upload remains named `build-output` and targets `output/`. Exact module types, model-generated text, logs, timing, raw command-output files, `FailedProjects.txt`, and unpack/re-zip intermediates are intentionally outside the compatibility contract.

## 7. Migration Order and Deletion Boundary

1. Create CodeAnalysis package assets and fixtures without migrating pipeline code.
2. In parallel, create Build after selecting one ModularPipelines baseline through a PoC compiling the TedToolkit and EverythingButTheSink extension shapes; then migrate resources, conventions, helpers, stage bases, explicit pack, manifest, and platform-neutral modules. Lock TedToolkit's exact resource expansion and the allowlisted EverythingButTheSink semantics through synthetic overlay tests.
3. After 1 and 2, create Combine and migrate standard policy, two input modes, neutral extension models, remote-action modules, and unified models.
4. Under P2-NUPIPE-003, capture the actual `Build/Program.cs` host baseline; perform the one-time paginated read-only GitHub Release capture, local-tag verification, file-hash recording, and User acceptance for `Build/release-history.v1.json`; change `Build/Build.csproj` to project-reference both new libraries; change `Build/Program.cs` to use their APIs; migrate the existing GitHub workflow into nonpublication-credentialed build/pack plus gated package/tag-publication jobs; and pass the local/event/version/release/failure/optional-adapter compatibility and intentional-correction cases.
5. Complete the GitHub client PoC and supplemental ADR gate, then migrate the GitHub provider and pass its provider-specific contract tests; the existing workflow continues using the event/profile mapping established in step 4.
6. Complete the GitLab client PoC and supplemental ADR gate, then migrate the GitLab provider and Generic Package publication and run the two-process EverythingButTheSink-derived fixture from new nupkg files.
7. In the fixture only, independently re-express allowlisted EverythingButTheSink semantic differences as synthetic overlays/expected outcomes, keep a synthetic props generator as a LocalBuild-only extension module, represent Feishu through a fake Message-profile `IPipelineEventSink`, and represent Gemini through a fake consumer `IChangeDescriptionGenerator`. Copy no external source, complete resource, configuration value, or secret, and do not modify the external repository.
8. Delete the duplicate old `TedToolkit.ModularPipelines` implementation only after every fixture passes.

Shared content that must remain: resource baselines, branch constants, directory conventions, safe Git diff helper, both TUnit `run --report-trx` and standard `test --logger trx` evaluation, neutral change-description contract, manifest, and stage dependency model.

Content that must be removed or moved: concrete Gemini/Feishu registration, unsafe ad hoc repository version mutation without recovery, TedToolkit-specific complete resource copies, fixed project lists, fixed NuGet URLs/keys, GitHub SDK calls, fixed repository IDs, and consumer-specific extension modules.

## 8. Completion Commands

```powershell
dotnet run --project tests/TedToolkit.CodeAnalysis.PackageTests -c Release
dotnet run --project tests/TedToolkit.ModularPipelines.Build.Tests -c Release
dotnet run --project tests/TedToolkit.ModularPipelines.Combine.Tests -c Release
dotnet run --project tests/TedToolkit.ModularPipelines.Combine.GitHub.Tests -c Release
dotnet run --project tests/TedToolkit.ModularPipelines.Combine.GitLab.Tests -c Release
dotnet run --project tests/HostCompatibility/TedToolkitBuildHostCompatibility -c Release
dotnet run --project tests/ReleaseContract.Tests -c Release
dotnet run --project Build/Build.csproj -c Release
```

The final bare host command is the default LocalBuild compatibility smoke test: it preserves the existing command surface, performs recovery-only version handling, clean/build behavior, and no package or remote publication. Deterministic release inputs, the temporary `Directory.Build.props` transaction, and production of all three package candidates are exercised by `tests/HostCompatibility/TedToolkitBuildHostCompatibility`; the bare command does not pretend to receive those harness inputs. Consumer fixture scripts then place those same-version `.nupkg` files into a temporary candidate source. Package source mapping resolves `TedToolkit.*` only from that source and third-party dependencies only from controlled upstream sources. Hosts that call both APIs directly reference both Build and Combine packages. The scripts then run `dotnet restore` and `dotnet build`. Evidence must show that no consumer fixture resolves a main-solution project reference. These commands do not publish to NuGet.org.

Exact NuGet dependency range syntax is verified against the official [package versioning contract](https://learn.microsoft.com/en-us/nuget/concepts/package-versioning). Source Link uses the .NET 10 SDK-integrated tooling introduced in .NET 8; implementation must not add a redundant GitHub SourceLink provider package merely for this repository ([SDK compatibility note](https://learn.microsoft.com/en-us/dotnet/core/compatibility/sdk/8.0/source-link)).
