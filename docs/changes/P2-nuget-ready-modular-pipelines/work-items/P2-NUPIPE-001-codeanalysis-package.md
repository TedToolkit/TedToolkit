# P2-NUPIPE-001: Deliver an Independently Consumable CodeAnalysis Package

## 📌 Status

Completed

## 🚦 Delivery Priority

- Priority: P2
- Rationale: Establishes package assets, metadata, and consumer-verification patterns for public NuGet packages.
- Blocking prerequisite: Resolved by User approval of the committed ADR-002 design revision.

## 🔗 Delivery Context

- Parent change: [P2-nuget-ready-modular-pipelines](../README.md)
- Approved revised design baseline: `be0e9b19941251498807d91088eaa4f8ea4d17a6`
- Prerequisites: Accepted ADR-001, accepted ADR-002, and the approved revised architecture record.
- Applicable principles: None.

## 🧩 Explicit Governance Constraints

`TedToolkit.CodeAnalysis` remains one package and does not produce a Strict variant. It must not override consumer `.editorconfig`, StyleCop files, `AdditionalFiles`, or warning-escalation policy. Its copied NuGet assets must not depend on repository-relative paths or `ProjectReference`. Its only permitted MSBuild assets are `buildTransitive/TedToolkit.CodeAnalysis.props` and `buildTransitive/TedToolkit.CodeAnalysis.targets`, which activate the exact external `SonarAnalyzer.CSharp` dependency and fail closed when its expected analyzer DLL is absent. Verification must restore the generated `.nupkg`, not reference the solution project. TedToolkit's repository release supplies the same explicit version to CodeAnalysis, Build, and Combine so the complete package set fits one manifest.

## 🎯 Outcome, Scope, and Non-goals

The outcome is a complete, audited analyzer package that consumers reference once while retaining their own rule configuration and current CodeFix behavior. PB-04 is resolved: copy the complete licensed current Roslynator/StyleCop code-fix provider set and every audited host-compatible binary variant. Host-neutral copied assets remain under `analyzers/dotnet/cs/`; upstream Roslyn-versioned sets retain distinct paths such as `analyzers/dotnet/roslyn3.8/cs/` and `analyzers/dotnet/roslyn4.7/cs/` so the NuGet/SDK host selector can choose one compatible set without filename collisions. Sonar is handled differently: the nuspec declares the exact external dependency `SonarAnalyzer.CSharp [10.23.0.137933]`, the package contains no Sonar DLL, and the two ADR-002 activation assets add only that restored DLL as an `Analyzer`. A required copied asset or variant that fails license, NOTICE, dependency closure, selection, or isolated load validation blocks this work package and returns for revised User approval; it is not flattened or silently omitted. Failure of the exact Sonar dependency/activation PoC also blocks this work package and must not fall back to copying the DLL. Scope then covers the project file, package-specific README, analyzer assets, activation adapter, package inspection, and offline consumer/host fixtures. For those fixtures, both the candidate package and its approved upstream closure are pre-populated in local file feeds; restore sources contain no HTTP endpoint, so success proves a complete loadable package closure. The separate release restore still performs the online vulnerability/deprecation audit. The first release contains no `lib` or `build` assets and no `buildTransitive` assets other than the two named activation files.

Non-goals: no new analyzer rules; no `TedToolkit.CodeAnalysis.Strict`; no modification of external consumer repositories; no publication to an external feed. This repository's own package/configuration references do migrate as implementation work.

## 🔍 Current Behavior and Impact Boundary

`TedToolkit.CodeAnalysis/TedToolkit.CodeAnalysis.csproj` currently declares analyzer and code-fix references. The repository-level `props/CodeAnalysis.props` injects a relative `ProjectReference` and `assets/stylecop.json` into this repository's projects, which cannot become a stable NuGet consumption surface. This change affects package consumption; the repository producer's local code-analysis configuration must be preserved explicitly without creating a candidate-package bootstrap loop.

## 🧪 Behavior Cases

| ID | Preconditions and input | Action | Expected observable behavior |
| --- | --- | --- | --- |
| BC-001-0 | Candidate upstream analyzer packages/versions, their Roslyn-versioned asset paths, and the complete current explicit and analyzer-bundled code-fix provider set are known | Audit licenses, notices, analyzer/code-fix DLLs, runtime dependencies, supported Roslyn hosts, same-name binaries across variants, and copy-versus-external-dependency disposition | Produce an approved per-path/per-host inventory for every copied Roslynator/StyleCop asset and separate exact Sonar dependency evidence; distinct compatible copied variants remain separate; any copied asset lacking redistribution evidence or a complete loadable dependency closure blocks 001 |
| BC-001-1 | Clean package-only fixtures cover the supported NuGet/SDK selector, every retained isolated Roslyn-host variant, and the exact controlled Sonar package | Restore/build consumers, assert selected Analyzer items, and load the complete approved code-fix closure for each variant | Exactly one compatible copied versioned set is selected per consumer; sibling variants do not load together; the exact external Sonar DLL is automatically present as an Analyzer; analyzers load and builds succeed; every expected current code-fix provider loads with all required runtime assemblies |
| BC-001-2 | The fixture contains a custom `.editorconfig` | Build code that triggers a known diagnostic | Consumer severity takes effect and the package does not override it |
| BC-001-3 | Coordinated release pack, resolved PB-04 preservation inventory, and exact Sonar dependency | Inspect `.nupkg` and nuspec | License, NOTICE, README, icon, repository metadata, every approved copied analyzer/code-fix asset, exact `SonarAnalyzer.CSharp [10.23.0.137933]` dependency, the two named activation files, and the supplied coordinated version exist; no Sonar DLL or other build asset exists |

## 🛠️ Implementation Contract

| Boundary or artifact | Required change | Compatibility or invariant |
| --- | --- | --- |
| Analyzer inventory | Record upstream package/version, license, notices, selected DLLs, runtime dependencies, supported Roslyn hosts, and copy-versus-external-dependency disposition before packing | Audit failure narrows or blocks the copied asset set; Sonar remains a separately licensed exact dependency |
| PB-04 preservation record | Record the User decision to preserve every current code-fix provider and its audited host-compatible variants; map each relative package path to license, dependency, NuGet/SDK selection, and isolated-load evidence | No flattening, partial, or implicit removal; any failed required asset or variant blocks 001 and requires revised User approval |
| `TedToolkit.CodeAnalysis.csproj` | Declare stable PackageId, license, icon, README, and repository metadata; generate correct analyzer assets | Do not expose repository-relative paths; do not produce this repository's symbols or SourceLink claims |
| Copied analyzer assets | Put only licensed copied analyzer DLLs, the complete approved current copied CodeFix provider set, and their runtime dependencies under compatible `analyzers/dotnet/.../cs/` paths; retain host-neutral `dotnet/cs` assets and each audited upstream `dotnet/roslyn<major>.<minor>/cs` subtree | Consumer configuration wins; same-name binaries from different variants never share a package path; no silent CodeFix removal; no Sonar DLL is copied |
| Sonar dependency and activation | Declare `SonarAnalyzer.CSharp [10.23.0.137933]`; add only `buildTransitive/TedToolkit.CodeAnalysis.props` and `.targets` to locate the exact restored DLL, add it as an `Analyzer`, and fail closed if missing | Do not set rules, severity, warnings, `AdditionalFiles`, package sources, credentials, or unrelated MSBuild properties/items |
| `LICENSE.txt` and third-party notices | Use `LICENSE.txt` as `PackageLicenseFile` for the TedToolkit aggregate package; record every copied upstream version, license, NOTICE, and included DLL in `THIRD-PARTY-NOTICES.txt`; separately identify the exact Sonar dependency, SSAL license, and corresponding source location | Do not claim third-party binaries or the external Sonar dependency are LGPL; do not package an analyzer before its audit is complete |
| Fixture | Consume only through `PackageReference` and a local package source | No reference to the main solution project |
| In-repository analyzer use | Replace the implicit package-facing reference with an explicit repository-only analyzer `ProjectReference` where Build/Combine source projects require the candidate analyzers; set analyzer-only/private metadata and verify the packed nuspec graph | The producer can analyze the source revision it is building without restoring its own unpublished CodeAnalysis package; no ProjectReference or CodeAnalysis dependency leaks into Build/Combine nupkg metadata |

## ✅ Verification Plan

| Behavior case | Test level and location | Setup and action | Observable assertion | Command |
| --- | --- | --- | --- | --- |
| BC-001-0 | Evidence review and package contract test | Build the disposition inventory and compare it with candidate package contents and nuspec | Every copied analyzer/runtime DLL has approved redistribution evidence; Sonar appears only as the exact dependency and never as a copied DLL | `dotnet run --project tests/TedToolkit.CodeAnalysis.PackageTests -c Release` |
| BC-001-1 | Offline package-selector consumers plus isolated Roslyn-host matrix | Pack, mirror the approved dependency closure into local file feeds, restore/build supported selector fixtures with a known Sonar diagnostic, inspect resolved Analyzer paths, and discover/instantiate every allowlisted current code-fix provider under each retained host variant without network access | Diagnostic appears; restore has no HTTP source; the exact external Sonar asset is activated; every TedToolkit package comes from the candidate source; assets contain no ProjectReference; exactly one compatible copied variant is selected per consumer; sibling variants remain unloaded; every preserved provider and its runtime closure loads for each retained host variant | Fixture script |
| BC-001-2 | Package integration test | Set a known diagnostic severity in a custom `.editorconfig` | `none`, `warning`, and `error` each take effect | `dotnet run --project tests/TedToolkit.CodeAnalysis.PackageTests -c Release` |
| BC-001-3 | Package-content test | Open nupkg/nuspec and compare every copied analyzer/code-fix DLL with the resolved PB-04 preservation inventory | Every approved copied asset, exact Sonar dependency, two named activation files, separate LICENSE/THIRD-PARTY-NOTICES, README, icon, and repository metadata exists; the Sonar DLL and unrelated/prohibited assets do not | Same command |

## ⏱️ Workload Estimate

- Planning range: 0.18–0.30 person-months.
- Confidence: Low–Medium.
- Assumptions: The complete copied Roslynator/StyleCop CodeFix provider set and each retained Roslyn-versioned variant have redistribution evidence, preserve valid NuGet/SDK selection, and have dependency closures that load in their isolated offline Roslyn hosts; the exact Sonar dependency has a stable restored path that the minimal adapter can activate. Any failure blocks 001 and requires revised User approval.
- Exclusions: External NuGet publication and package signing.
- Actual effort: One implementation and verification session on 2026-07-29; person-month accounting was not captured.
- Variance: No material scope variance from the approved revised baseline.

## 📋 Completion Evidence

Record the coordinated candidate version, upstream-license inventory, complete per-path/per-host preserved CodeFix inventory, NuGet/SDK Analyzer-selection evidence, isolated Roslyn-host matrix results, pack output, fixture restore/build, TUnit results, nupkg content inspection, and resolved package versions.

Implementation audit on 2026-07-29 found that `SonarAnalyzer.CSharp` 10.23.0.137933 is licensed under the SONAR Source-Available License v1.0 rather than LGPL. The User selected ADR-002's exact external dependency plus minimal activation adapter, so TedToolkit will neither copy nor relicense the Sonar DLL. The User approved revised design baseline `be0e9b19941251498807d91088eaa4f8ea4d17a6` on 2026-07-29.

Implementation completed on 2026-07-29 with coordinated candidate version `2026.7.29`. `analyzer-assets.csv` records 71 copied Roslynator/StyleCop DLLs with package path and SHA-256; `analyzer-dependencies.csv` records the exact external Sonar dependency and audited DLL hash. Package inspection proved that every copied entry matches its recorded hash, no additional analyzer DLL exists, the nupkg contains no Sonar DLL, and the nuspec has exactly one dependency at `[10.23.0.137933]`. Separate Roslyn 3.8.0 and 4.7.0 host processes restored their complete dependency closures exclusively from a temporary local source and each instantiated every concrete provider from its four variant CodeFix assemblies plus the neutral StyleCop CodeFix assembly. A package-only consumer restored exclusively from temporary local candidate/upstream sources; the default SDK 10.0.302 selector chose roslyn4.7 without loading roslyn3.8, while `CompilerApiVersion=roslyn3.8` chose roslyn3.8 without loading roslyn4.7. The same consumer loaded the external Sonar analyzer exactly once, verified its audited SHA-256, honored S1135 `none`/`warning`/`error`, and failed closed after the restored Sonar DLL was removed. The repository source project separately proved that its explicit private producer references resolve Roslynator, StyleCop, Sonar, and the candidate analyzer project.

Verification commands and results:

- `dotnet run --project tests/TedToolkit.CodeAnalysis.PackageTests -c Release --no-restore` — passed, 7/7 tests.
- `dotnet pack TedToolkit.CodeAnalysis/TedToolkit.CodeAnalysis.csproj -c Release --no-restore -p:PackageVersion=2026.7.29` — passed with no warnings.
- `dotnet build TedToolkit.slnx -c Release --no-restore` — passed with 0 warnings and 0 errors.

## 🔄 Migration and Rollback

Replace the implicit package-facing reference in `props/CodeAnalysis.props` with explicit repository-only analyzer project references or equivalent local configuration. External consumers and fixtures use the generated package; the producer does not restore its own unpublished candidate. If a release is defective, stop publishing later versions; never overwrite a published version.

## ⚠️ Risks and Open Questions

| Item | Impact | Owner or next decision |
| --- | --- | --- |
| A required explicit or analyzer-bundled CodeFix asset or Roslyn-versioned variant fails license, dependency-closure, NuGet/SDK selection, or isolated Roslyn-host validation | The approved compatibility-preserving asset set cannot ship | Block 001 and request revised User approval; do not flatten variants, omit the asset, or downgrade to partial preservation |
| The exact Sonar package layout or activation adapter is not stable in a package-only consumer | Consumers restore TedToolkit but do not run Sonar reliably | Block 001 and request revised User approval; do not fall back to copying the Sonar DLL |
