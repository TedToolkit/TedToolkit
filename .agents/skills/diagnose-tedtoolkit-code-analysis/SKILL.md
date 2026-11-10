---
name: diagnose-tedtoolkit-code-analysis
description: >-
  Use when maintaining analyzer configuration or diagnosing build differences in a project that consumes TedToolkit.CodeAnalysis or its shared CodeAnalysis.props.
---

# Diagnose TedToolkit Code Analysis

- Identify which analyzer reports the diagnostic and inspect the effective `.editorconfig` and MSBuild imports for the affected project.
- Check Release settings separately because shared build properties can promote diagnostics to errors. Preserve existing project-specific suppressions unless their rationale has changed.
- Verify the result by building the affected project in Release with the same analyzer configuration as CI.
