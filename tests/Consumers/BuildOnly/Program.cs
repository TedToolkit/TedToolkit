using ModularPipelines;
using ModularPipelines.Extensions;

using TedToolkit.ModularPipelines.Build.Execution;
using TedToolkit.ModularPipelines.Build.Inputs;

var root = new DirectoryInfo(Path.GetFullPath(
    Path.Combine(AppContext.BaseDirectory, "..", "..", "..")));
var builder = Pipeline.CreateBuilder();
builder.AddBuildPipeline(
    BuildExecutionProfile.Validate,
    new()
    {
        RootDirectory = root,
        ArtifactRootDirectory = new(Path.Combine(root.FullName, "output")),
        SourceRevision = new string('a', 40),
        MsBuildVersion = new()
        {
            TargetFile = new(Path.Combine(root.FullName, "fixture.props")),
            RecoveryFile = new(Path.Combine(root.FullName, ".recovery.json")),
        },
        BuildTargets = [],
    },
    new()
    {
        RunFormat = false,
    });
await builder.ExecutePipelineAsync().ConfigureAwait(false);
