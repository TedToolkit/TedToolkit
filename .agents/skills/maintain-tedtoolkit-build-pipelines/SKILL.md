---
name: maintain-tedtoolkit-build-pipelines
description: >-
  Use when updating a repository Build project based on TedToolkit.ModularPipelines or diagnosing its compilation, test, and release flow.
---

# Maintain TedToolkit Build Pipelines

- Locate `Build/Program.cs` and inspect the solution and test paths passed to `TedPipeline`. Keep production, verification, and release inputs separate.
- When debugging the pipeline, start with the corresponding direct Release build or test command to separate SDK failures from orchestration failures.
- Check the existing branch and release conditions before invoking a pipeline that may write externally. Verify the build or test stage that changed without publishing packages.
