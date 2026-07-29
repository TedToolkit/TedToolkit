using ModularPipelines;
using ModularPipelines.Extensions;

using TedToolkit.ModularPipelines.Build.Conventions;
using TedToolkit.ModularPipelines.Build.Inputs;
using TedToolkit.ModularPipelines.Combine.Configuration;
using TedToolkit.ModularPipelines.Combine.Execution;

var root = new DirectoryInfo(Path.GetFullPath(
    Path.Combine(AppContext.BaseDirectory, "..", "..", "..")));
var sourceRevision = new string('b', 40);
var builder = Pipeline.CreateBuilder();
builder.AddStandardPipeline(
    root,
    new()
    {
        ExecutionPolicy = PipelineExecutionPolicy.Manual,
        ManualProfile = PipelineProfile.Validate,
        ExecutionContext = new()
        {
            Trigger = PipelineTriggerKind.Manual,
            IsCi = true,
        },
        ArtifactValidation = new()
        {
            ExpectedSourceRevision = sourceRevision,
        },
        GitHub = new()
        {
            InstanceUrl = new("https://github.example/prefix"),
            ApiUrl = new("https://api.github.example/v3"),
            Owner = "consumer",
            Repository = "fixture",
        },
    },
    new()
    {
        RootDirectory = root,
        ArtifactRootDirectory = new(Path.Combine(root.FullName, "output")),
        SourceRevision = sourceRevision,
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
