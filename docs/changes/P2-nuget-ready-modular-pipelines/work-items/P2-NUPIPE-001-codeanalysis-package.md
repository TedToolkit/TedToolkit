# P2-NUPIPE-001: Deliver an Independently Consumable CodeAnalysis Package

## 📌 Status

Approved

## 🚦 Delivery Priority

- Priority: P2
- Rationale: Establishes package assets, metadata, and consumer-verification patterns for public NuGet packages.
- Blocking prerequisite: Approved design baseline.

## 🔗 Delivery Context

- Parent change: [P2-nuget-ready-modular-pipelines](../README.md)
- Approved design baseline: `0e01cced32f4d8e0946e54609880c617199a5b4c`
- Prerequisites: Approved ADR-001 and architecture record.
- Applicable principles: None.

## 🧩 Explicit Governance Constraints

`TedToolkit.CodeAnalysis` remains one package and does not produce a Strict variant. It must not override consumer `.editorconfig`, StyleCop files, or warning-escalation policy. Its NuGet assets must not depend on repository-relative paths or `ProjectReference`. Verification must restore the generated `.nupkg`, not reference the solution project. TedToolkit's repository release supplies the same explicit version to CodeAnalysis, Build, and Combine so the complete package set fits one manifest.

## 🎯 Outcome, Scope, and Non-goals

The outcome is a complete, audited analyzer asset package that consumers can reference while retaining their own rule configuration and current CodeFix behavior. PB-04 is resolved: preserve the complete current code-fix provider set, including the explicit `Roslynator.CodeFixes` dependency and code-fix DLLs bundled by selected analyzer packages, plus every audited host-compatible binary variant. Host-neutral assets remain under `analyzers/dotnet/cs/`; upstream Roslyn-versioned sets retain distinct paths such as `analyzers/dotnet/roslyn3.8/cs/` and `analyzers/dotnet/roslyn4.7/cs/` so the NuGet/SDK host selector can choose one compatible set without filename collisions. A required asset or variant that fails license, NOTICE, dependency closure, selection, or isolated load validation blocks this work package and returns for revised User approval; it is not flattened or silently omitted. Scope then covers the project file, package-specific README, analyzer assets, package inspection, and offline consumer/host fixtures. For those fixtures, both the candidate package and its approved upstream closure are pre-populated in local file feeds; restore sources contain no HTTP endpoint, so success proves a complete loadable package closure. The separate release restore still performs the online vulnerability/deprecation audit. The first release contains no `lib`, `build`, or `buildTransitive` assets.

Non-goals: no new analyzer rules; no `TedToolkit.CodeAnalysis.Strict`; no modification of external consumer repositories; no publication to an external feed. This repository's own package/configuration references do migrate as implementation work.

## 🔍 Current Behavior and Impact Boundary

`TedToolkit.CodeAnalysis/TedToolkit.CodeAnalysis.csproj` currently declares analyzer and code-fix references. The repository-level `props/CodeAnalysis.props` injects a relative `ProjectReference` and `assets/stylecop.json` into this repository's projects, which cannot become a stable NuGet consumption surface. This change affects package consumption; the repository producer's local code-analysis configuration must be preserved explicitly without creating a candidate-package bootstrap loop.

## 🧪 Behavior Cases

| ID | Preconditions and input | Action | Expected observable behavior |
| --- | --- | --- | --- |
| BC-001-0 | Candidate upstream analyzer packages/versions, their Roslyn-versioned asset paths, and the complete current explicit and analyzer-bundled code-fix provider set are known | Audit licenses, notices, analyzer/code-fix DLLs, runtime dependencies, supported Roslyn hosts, and same-name binaries across variants | Produce an approved per-path/per-host inventory for every preserved asset; distinct compatible variants remain separate; any required asset lacking redistribution evidence or a complete loadable dependency closure blocks 001 and requires revised User approval |
| BC-001-1 | Clean package-only fixtures cover the supported NuGet/SDK selector and every retained isolated Roslyn-host variant | Restore/build consumers, assert selected Analyzer items, and load the complete approved code-fix closure for each variant | Exactly one compatible versioned set is selected per consumer; sibling variants do not load together; analyzers load and builds succeed; every expected current code-fix provider loads with all required runtime assemblies |
| BC-001-2 | The fixture contains a custom `.editorconfig` | Build code that triggers a known diagnostic | Consumer severity takes effect and the package does not override it |
| BC-001-3 | Coordinated release pack and resolved PB-04 preservation inventory | Inspect `.nupkg` | License, NOTICE, README, icon, repository metadata, every approved current analyzer/code-fix asset, and the supplied coordinated version exist; unapproved and build assets do not |

## 🛠️ Implementation Contract

| Boundary or artifact | Required change | Compatibility or invariant |
| --- | --- | --- |
| Analyzer inventory | Record upstream package/version, license, notices, selected DLLs, runtime dependencies, and supported Roslyn hosts before packing | Audit failure narrows or blocks the asset set; it does not silently become a package assumption |
| PB-04 preservation record | Record the User decision to preserve every current code-fix provider and its audited host-compatible variants; map each relative package path to license, dependency, NuGet/SDK selection, and isolated-load evidence | No flattening, partial, or implicit removal; any failed required asset or variant blocks 001 and requires revised User approval |
| `TedToolkit.CodeAnalysis.csproj` | Declare stable PackageId, license, icon, README, and repository metadata; generate correct analyzer assets | Do not expose repository-relative paths; do not produce this repository's symbols or SourceLink claims |
| Analyzer assets | Put only licensed analyzer DLLs, the complete approved current CodeFix provider set, and their runtime dependencies under compatible `analyzers/dotnet/.../cs/` paths; retain host-neutral `dotnet/cs` assets and each audited upstream `dotnet/roslyn<major>.<minor>/cs` subtree; do not generate `build`/`buildTransitive` props | Consumer configuration wins; zero MSBuild side effects; same-name binaries from different variants never share a package path; no silent CodeFix removal |
| `LICENSE.txt` and third-party notices | Use `LICENSE.txt` as `PackageLicenseFile` for the TedToolkit aggregate package; record every upstream version, license, NOTICE, and included DLL in `THIRD-PARTY-NOTICES.txt` | Do not claim third-party binaries are LGPL; do not package an analyzer before its audit is complete |
| Fixture | Consume only through `PackageReference` and a local package source | No reference to the main solution project |
| In-repository analyzer use | Replace the implicit package-facing reference with an explicit repository-only analyzer `ProjectReference` where Build/Combine source projects require the candidate analyzers; set analyzer-only/private metadata and verify the packed nuspec graph | The producer can analyze the source revision it is building without restoring its own unpublished CodeAnalysis package; no ProjectReference or CodeAnalysis dependency leaks into Build/Combine nupkg metadata |

## ✅ Verification Plan

| Behavior case | Test level and location | Setup and action | Observable assertion | Command |
| --- | --- | --- | --- | --- |
| BC-001-0 | Evidence review and package contract test | Build the inventory and compare it with candidate package contents | Every packed analyzer/runtime DLL has an inventory entry and approved redistribution evidence | `dotnet run --project tests/TedToolkit.CodeAnalysis.PackageTests -c Release` |
| BC-001-1 | Offline package-selector consumers plus isolated Roslyn-host matrix | Pack, mirror the approved dependency closure into local file feeds, restore/build supported selector fixtures with a known diagnostic, inspect resolved Analyzer paths, and discover/instantiate every allowlisted current code-fix provider under each retained host variant without network access | Diagnostic appears; restore has no HTTP source; every TedToolkit package comes from the candidate source; assets contain no ProjectReference; exactly one compatible variant is selected per consumer; sibling variants remain unloaded; every preserved provider and its runtime closure loads for each retained host variant | Fixture script |
| BC-001-2 | Package integration test | Set a known diagnostic severity in a custom `.editorconfig` | `none`, `warning`, and `error` each take effect | `dotnet run --project tests/TedToolkit.CodeAnalysis.PackageTests -c Release` |
| BC-001-3 | Package-content test | Open nupkg/nuspec and compare every analyzer/code-fix DLL with the resolved PB-04 preservation inventory | Every approved current asset, separate LICENSE/THIRD-PARTY-NOTICES, README, icon, and repository metadata exists; unrelated/prohibited assets do not | Same command |

## ⏱️ Workload Estimate

- Planning range: 0.15–0.25 person-months.
- Confidence: Low–Medium.
- Assumptions: The complete current explicit and analyzer-bundled CodeFix provider set and each retained Roslyn-versioned variant have redistribution evidence, preserve valid NuGet/SDK selection, and have dependency closures that load in their isolated offline Roslyn hosts; any failure blocks 001 and requires revised User approval.
- Exclusions: External NuGet publication and package signing.
- Actual effort: Not completed.
- Variance: Not completed.

## 📋 Completion Evidence

Record the coordinated candidate version, upstream-license inventory, complete per-path/per-host preserved CodeFix inventory, NuGet/SDK Analyzer-selection evidence, isolated Roslyn-host matrix results, pack output, fixture restore/build, TUnit results, nupkg content inspection, and resolved package versions.

## 🔄 Migration and Rollback

Replace the implicit package-facing reference in `props/CodeAnalysis.props` with explicit repository-only analyzer project references or equivalent local configuration. External consumers and fixtures use the generated package; the producer does not restore its own unpublished candidate. If a release is defective, stop publishing later versions; never overwrite a published version.

## ⚠️ Risks and Open Questions

| Item | Impact | Owner or next decision |
| --- | --- | --- |
| A required explicit or analyzer-bundled CodeFix asset or Roslyn-versioned variant fails license, dependency-closure, NuGet/SDK selection, or isolated Roslyn-host validation | The approved compatibility-preserving asset set cannot ship | Block 001 and request revised User approval; do not flatten variants, omit the asset, or downgrade to partial preservation |
| Redistributed analyzer dependency closure is incomplete | Consumers cannot load analyzers | Validate through the license/dependency inventory and BC-001-1; adjust the asset list if it fails |
