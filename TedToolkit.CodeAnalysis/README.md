# TedToolkit.CodeAnalysis

`TedToolkit.CodeAnalysis` provides one-reference access to the analyzer and
code-fix set used by TedToolkit without imposing warning levels or rule
configuration on the consuming project.

The package contains audited Roslynator and StyleCop analyzer assets. It also
declares the exact external dependency `SonarAnalyzer.CSharp
[10.23.0.137933]`; Sonar remains separately licensed and its DLL is not copied
into this package.

## Usage

```xml
<PackageReference Include="TedToolkit.CodeAnalysis"
                  Version="1.0.0"
                  PrivateAssets="all" />
```

Configure diagnostic severity in the consumer's own `.editorconfig`. This
package does not add an `.editorconfig`, `stylecop.json`, ruleset,
`AdditionalFiles`, warning escalation, or a package source.

See `THIRD-PARTY-NOTICES.txt`, `analyzer-assets.csv`, and
`analyzer-dependencies.csv` in the package for the audited asset and dependency
inventory.
