---
name: maintain-tedtoolkit-build-pipelines
description: >-
  Use when updating a repository Build project based on TedToolkit.ModularPipelines or diagnosing its compilation, test, and release flow.
---

# Maintain TedToolkit Build Pipelines

Identify the entry point for the repository and whether the target is build, test, package creation, or release. Use the solution and project paths already selected by the Build project.

## Workflow

- Locate `Build/Program.cs` and inspect the solution and test paths passed to `TedPipeline`. Keep production, verification, and release inputs separate.
- When debugging the pipeline, start with the corresponding direct Release build or test command to separate SDK failures from orchestration failures.
- Check the existing branch and release conditions before invoking a pipeline that may write externally. Verify the build or test stage that changed without publishing packages.

## Example

```shell
dotnet build path/to/Solution.slnx -c Release
dotnet run --project path/to/Tests.csproj -c Release
```

## Check the result

Run the relevant direct commands before diagnosing orchestration. Inspect the first failed module and any tests that were canceled as a result. Verify package artifacts locally and keep publication at the configured release boundary.
