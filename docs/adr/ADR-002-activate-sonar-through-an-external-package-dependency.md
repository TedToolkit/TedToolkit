# ADR-002: Activate Sonar through an External Package Dependency

- Status: Accepted
- Date: 2026-07-29
- Decision owner: User
- Decision scope: The first public `TedToolkit.CodeAnalysis` package and its automatic activation of `SonarAnalyzer.CSharp`.
- Applicable principles: None; the repository does not currently contain approved principle documents.
- Related change or work packages: [P2-NUPIPE-001](../changes/P2-nuget-ready-modular-pipelines/work-items/P2-NUPIPE-001-codeanalysis-package.md)
- Supersedes: None
- Superseded by: None

## 📌 Decision at a Glance

Keep `SonarAnalyzer.CSharp` as an exact external NuGet dependency and activate its analyzer through a narrowly scoped `buildTransitive` adapter instead of redistributing or relicensing the Sonar DLL inside `TedToolkit.CodeAnalysis`.

## 🧭 Context and Decision Question

The approved P2 baseline required `TedToolkit.CodeAnalysis` to copy every selected analyzer DLL into its own package and prohibited MSBuild assets. The P2-NUPIPE-001 license audit found that `SonarAnalyzer.CSharp` 10.23.0.137933 uses the SONAR Source-Available License v1.0 rather than LGPL. Copying that DLL would make TedToolkit a distributor subject to the license's purpose and source-availability conditions. Declaring only an ordinary dependency is insufficient for the required one-reference consumer experience because NuGet analyzer assets and packages marked as development dependencies do not reliably flow transitively.

The decision question is how a consumer can reference only `TedToolkit.CodeAnalysis` and still restore and run the current Sonar analyzer without TedToolkit copying or claiming a license over the Sonar binary.

## 🎯 Decision Drivers and Constraints

| Type | Driver or constraint | Evidence or source | Priority |
| --- | --- | --- | --- |
| Hard constraint | A consumer references one TedToolkit package and Sonar analysis runs automatically | User requirement on 2026-07-29 | Must |
| Hard constraint | TedToolkit does not copy or relicense the Sonar DLL | `SonarAnalyzer.CSharp` 10.23.0.137933 nuspec and bundled SONAR Source-Available License v1.0 | Must |
| Hard constraint | Consumer `.editorconfig`, warnings, rules, and `AdditionalFiles` remain untouched | Approved P2 behavior contract | Must |
| Hard constraint | The dependency version and analyzer path fail closed when the upstream package changes | Public package reproducibility and immutable NuGet versions | Must |
| Decision driver | Keep all other analyzer and CodeFix assets under the already approved explicit package paths | Approved PB-04 preservation decision | High |

## 🔎 Options and Evidence

| Option | Evidence and confidence | Meets drivers | Decisive trade-off | Outcome |
| --- | --- | --- | --- | --- |
| Copy the Sonar DLL and add its license | Documented license permits only conditioned distribution and requires source availability | No | Makes TedToolkit a distributor and does not remove downstream SSAL restrictions | Rejected |
| Declare only an ordinary Sonar dependency | Documented NuGet analyzer/private-asset behavior; package-only PoC still required | Partially | Restore may succeed while the analyzer does not flow into compilation | Rejected |
| Require every consumer to add Sonar directly | High confidence | No | Breaks the one-reference consumer requirement | Rejected |
| Exact dependency plus a minimal transitive activation adapter | Documented NuGet dependency/buildTransitive mechanisms; package-only PoC required | Yes, subject to PoC | Introduces a tightly bounded MSBuild asset and an upstream-layout coupling | Selected |

## ✅ Decision

`TedToolkit.CodeAnalysis` declares exactly one `SonarAnalyzer.CSharp` dependency with the exact range `[10.23.0.137933]`. It does not contain `SonarAnalyzer.CSharp.dll`. It may contain only two MSBuild activation assets, `buildTransitive/TedToolkit.CodeAnalysis.props` and `buildTransitive/TedToolkit.CodeAnalysis.targets`, whose complete responsibility is to add that exact dependency DLL as an `Analyzer` and fail before compilation when the expected restored asset is absent.

The adapter must not set or add an `.editorconfig`, ruleset, `AdditionalFiles`, warning property, severity, package source, credential, or unrelated analyzer. All copied Roslynator and StyleCop analyzer/CodeFix assets remain governed by the existing per-path inventory and host-selection contract.

## 💡 Why This Decision Now

The license discovered during implementation invalidates physical redistribution, while the User requirement preserves automatic Sonar activation. A minimal transitive adapter isolates the exception to the only asset type that cannot meet both constraints through ordinary package layout. This decision is invalid if a package-only PoC cannot prove exact dependency generation, deterministic path resolution, automatic analyzer execution, and fail-closed behavior.

## 🔗 Evidence and Links

- [SONAR Source-Available License v1.0](https://www.sonarsource.com/license/ssal-1-0-0/)
- [NuGet PackageReference asset-flow documentation](https://learn.microsoft.com/nuget/consume-packages/package-references-in-project-files)
- `SonarAnalyzer.CSharp` 10.23.0.137933 local package nuspec, license, and SHA-256 evidence recorded by P2-NUPIPE-001.

## ⚖️ Consequences and Accepted Trade-offs

Consumers receive Sonar automatically without a second explicit reference, but their restore graph contains a separately licensed SSAL dependency. TedToolkit's LGPL applies only to TedToolkit-owned package content. The README and third-party notices must name the exact Sonar version, license, and corresponding source location.

The package now has one deliberate MSBuild-asset exception. Its upstream package ID, version, and analyzer path are compatibility inputs protected by package-only tests. A Sonar version or layout change requires a reviewed inventory update and fresh consumer PoC.

## 🛠️ Implementation Handoff

P2-NUPIPE-001 owns the exact dependency, minimal adapter, package content checks, automatic diagnostic fixture, and license disclosure. The generated nupkg must contain no Sonar DLL and no MSBuild behavior beyond Sonar activation and fail-closed path validation.

## 🔄 Rollout, Rollback, and Exit

Before the first public release, rollback means stopping publication and retaining the existing repository-only direct Sonar reference. After publication, a defective version is not overwritten; a corrected coordinated version is released. If Sonar later provides a transitive analyzer contract or a redistribution-compatible license, a new ADR may remove the adapter.

## 📅 Follow-ups and Review Triggers

| Item | Owner | Due date or objective trigger | Status |
| --- | --- | --- | --- |
| Prove exact dependency and automatic analyzer activation from generated packages only | P2-NUPIPE-001 | Before packing approval | Open |
| Reassess the adapter | User | Sonar version, package layout, license, or NuGet asset-flow change | Open |
