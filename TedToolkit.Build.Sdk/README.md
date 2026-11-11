# TedToolkit.Build.Sdk

The SDK supplies the shared MSBuild configuration used by TedToolkit repositories. MSBuild restores the version pinned in the repository's `global.json` before evaluating project imports.

```json
{
  "msbuild-sdks": {
    "TedToolkit.Build.Sdk": "0.1.0-preview.1"
  }
}
```

Import the configuration a project uses. Keep the imports in the same order as any existing project-specific overrides.

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <Import Project="CodeAnalysis.props" Sdk="TedToolkit.Build.Sdk" />
  <Import Project="NugetPackage.props" Sdk="TedToolkit.Build.Sdk" />
  <Import Project="AlmostAllFrameworks.props" Sdk="TedToolkit.Build.Sdk" />
</Project>
```

The code analysis import supplies the Roslynator and StyleCop analyzers as private package references. It uses the repository's `.editorconfig` when available and supplies the shared C# rules as a global analyzer config otherwise. The packaging import includes the shared package icon.

The build workflow runs the same projects as a local `dotnet build`. The project's configured NuGet sources must include the source that hosts its pinned SDK version. The SDK carries build configuration and assets. The reusable workflow supplies the CI jobs.

The [preview package source](https://raw.githubusercontent.com/TedToolkit/TedToolkit/refs/tags/build-sdk-v0.1.0-preview.1/index.json) contains `0.1.0-preview.1`. Map only `TedToolkit.Build.Sdk` to this source. Resolve the analyzers and application dependencies from NuGet.org.

Pack the SDK with `dotnet pack TedToolkit.Build.Sdk/TedToolkit.Build.Sdk.csproj -c Release`.
