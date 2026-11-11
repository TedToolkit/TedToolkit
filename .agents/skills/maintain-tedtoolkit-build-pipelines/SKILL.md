---
name: maintain-tedtoolkit-build-pipelines
description: >-
  Use when configuring the TedToolkit shared build workflow or diagnosing its compilation, test, and release flow.
---

# Maintain TedToolkit Build Pipelines

Identify the entry point for the repository and whether the target is build, test, package creation, or release.

## Workflow

- For the shared `build.yml`, check the calling repository's `BUILD_SOLUTION`, `BUILD_TEST_PROJECTS`, and `BUILD_PACK_PROJECTS` Actions variables. List one test or package target per line.
- For a local run, set `BUILD_STAGE` to `build`, `test`, or `pack` and set the corresponding target variable. Run `dotnet run --file /path/to/TedToolkit/build.cs` from the target repository root.
- When debugging the pipeline, start with the corresponding direct Release build or test command to separate SDK failures from orchestration failures.
- Check the existing branch and release conditions before invoking a pipeline that may write externally. Verify the build or test stage that changed without publishing packages.

## Example

```shell
dotnet build path/to/Solution.slnx -c Release
dotnet run --project path/to/Tests.csproj -c Release
```

## Check the result

Run the relevant direct commands before diagnosing orchestration. Inspect the first failed module and any tests that were canceled as a result. Verify package artifacts locally and keep publication at the configured release boundary.
