# TedToolkit.ModularPipelines.Combine

Provider-neutral composition for explicit local-build, validation, packaging,
publication, and message profiles. Consumers opt into every remote action and
configure either GitHub or GitLab without exposing provider SDK types.

Use `AddStandardPipeline` with an explicit root and typed `CombineOptions`.
The package can run Build in process or validate a prior
`PipelineArtifactManifest` without rebuilding.
