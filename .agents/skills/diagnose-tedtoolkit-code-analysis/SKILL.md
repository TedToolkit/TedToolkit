---
name: diagnose-tedtoolkit-code-analysis
description: >-
  Use when maintaining analyzer configuration or diagnosing build differences in a project that consumes TedToolkit.CodeAnalysis or its shared CodeAnalysis.props.
---

# Diagnose TedToolkit Code Analysis

Use the diagnostic ID to identify the reporting analyzer before changing the code or its configuration. Inspect the effective project and `.editorconfig` hierarchy.

## Workflow

- Identify which analyzer reports the diagnostic and inspect the effective `.editorconfig` and MSBuild imports for the affected project.
- Check Release settings separately because shared build properties can promote diagnostics to errors. Preserve existing project-specific suppressions unless their rationale has changed.
- Verify the result by building the affected project in Release with the same analyzer configuration as CI.

## Example

```shell
dotnet build path/to/Consumer.csproj -c Release
```

## Check the result

Compare the exact diagnostic in CI and the same configuration locally. When a suppression is intentional, keep it scoped to the declaration or project that needs it and retain its rationale.
