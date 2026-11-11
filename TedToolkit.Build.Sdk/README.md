# TedToolkit.Build.Sdk

The SDK supplies TedToolkit's shared target frameworks, code analysis, and packaging settings. Local builds and CI resolve the same SDK version. The reusable workflow runs `build.cs` to orchestrate code generation, build, test, and pack.

## Use the SDK

Add the SDK version to `global.json`. Keep any existing .NET SDK settings in that file.

```json
{
  "msbuild-sdks": {
    "TedToolkit.Build.Sdk": "0.1.0-preview.1"
  }
}
```

Add the GitHub feed to the repository's `NuGet.Config` and map only `TedToolkit.Build.Sdk` to it. Merge these entries into any existing source configuration.

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
    <add key="TedToolkit SDK"
         value="https://raw.githubusercontent.com/TedToolkit/TedToolkit/refs/tags/build-sdk-v0.1.0-preview.1/index.json"
         protocolVersion="3" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="nuget.org">
      <package pattern="*" />
    </packageSource>
    <packageSource key="TedToolkit SDK">
      <package pattern="TedToolkit.Build.Sdk" />
    </packageSource>
  </packageSourceMapping>
</configuration>
```

Import the settings a project needs. When replacing a shared file import, keep its position relative to project-specific overrides.

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <Import Project="CodeAnalysis.props" Sdk="TedToolkit.Build.Sdk" />
  <Import Project="NugetPackage.props" Sdk="TedToolkit.Build.Sdk" />
  <Import Project="AlmostAllFrameworks.props" Sdk="TedToolkit.Build.Sdk" />
</Project>
```

`CodeAnalysis.props` adds Roslynator and StyleCop as private package references and supplies the shared C# rules when the project has no applicable `.editorconfig`. `NugetPackage.props` configures packaging and includes the shared icon. `AlmostAllFrameworks.props` provides the shared target framework list.

Run `dotnet build -c Release` from the consumer repository. MSBuild restores the pinned SDK from GitHub before it evaluates the imports. The feed is public, so local builds and CI can restore it anonymously.

## Publish an SDK version on GitHub

Publish a new SDK version when consumers need a change to the shared MSBuild configuration. Each version has its own Git tag and static NuGet V3 feed. The [first preview tag](https://github.com/TedToolkit/TedToolkit/tree/build-sdk-v0.1.0-preview.1) shows the published files.

1. Set `Version` in `TedToolkit.Build.Sdk.csproj`, commit the source, and pack from the TedToolkit repository root.

   ```shell
   dotnet pack TedToolkit.Build.Sdk/TedToolkit.Build.Sdk.csproj -c Release -o artifacts/sdk-pack
   ```

2. Prepare an empty directory for the feed. Use the lowercase, normalized NuGet version in the package paths and list the same version in the package's `index.json` as `{"versions":["<version>"]}`. Copy the `.nupkg`, extract its `.nuspec` from the ZIP archive, and record the package SHA-512 as Base64 in the `.nupkg.sha512` file. In `manifest.json`, record `package`, `version`, `sourceCommit` and the package's hexadecimal `sha256`.

   ```text
   index.json
   manifest.json
   packages/tedtoolkit.build.sdk/index.json
   packages/tedtoolkit.build.sdk/<version>/tedtoolkit.build.sdk.<version>.nupkg
   packages/tedtoolkit.build.sdk/<version>/tedtoolkit.build.sdk.<version>.nupkg.sha512
   packages/tedtoolkit.build.sdk/<version>/tedtoolkit.build.sdk.nuspec
   ```

   Point the root `index.json` at the tag's `packages/` directory. [NuGet package content API](https://learn.microsoft.com/en-us/nuget/api/package-base-address-resource)

   ```json
   {
     "version": "3.0.0",
     "resources": [
       {
         "@type": "PackageBaseAddress/3.0.0",
         "@id": "https://raw.githubusercontent.com/TedToolkit/TedToolkit/refs/tags/build-sdk-v<version>/packages/"
       }
     ]
   }
   ```

3. Commit the feed files in a separate Git repository and push its version tag to TedToolkit. Run the following commands from the prepared feed directory and replace `<version>` with the SDK version.

   ```shell
   git init --initial-branch=sdk-feed
   git add -- index.json manifest.json packages
   git commit -m "Publish TedToolkit.Build.Sdk <version>"
   git tag "build-sdk-v<version>"
   git push https://github.com/TedToolkit/TedToolkit.git "refs/tags/build-sdk-v<version>"
   ```

4. Download the package from the public feed and compare its SHA-256 with `manifest.json`. Update both the SDK version in the consumer's `global.json` and the feed tag in its `NuGet.Config`, then build from a clean checkout with an empty NuGet package cache. Preserve the original tags and package bytes so existing pins remain reproducible.
