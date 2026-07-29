using ModularPipelines;
using ModularPipelines.Extensions;

using TedToolkit.ModularPipelines.Build.Conventions;
using TedToolkit.ModularPipelines.Build.Inputs;
using TedToolkit.ModularPipelines.Combine.Configuration;
using TedToolkit.ModularPipelines.Combine.Execution;

var root = new DirectoryInfo(Path.GetFullPath(
    Path.Combine(AppContext.BaseDirectory, "..", "..", "..")));
var sourceRevision = new string('c', 40);
var consume = string.Equals(
    Environment.GetEnvironmentVariable("CONSUME_ARTIFACTS"),
    "true",
    StringComparison.OrdinalIgnoreCase);
var builder = Pipeline.CreateBuilder();
builder.AddStandardPipeline(
    root,
    new()
    {
        ExecutionPolicy = PipelineExecutionPolicy.Manual,
        ManualProfile = PipelineProfile.Validate,
        InputMode = consume
            ? PipelineInputMode.ConsumeArtifacts
            : PipelineInputMode.RunBuild,
        ArtifactManifestPath = consume
            ? "output/pipeline-artifacts.v1.json"
            : null,
        ExecutionContext = new()
        {
            Trigger = PipelineTriggerKind.Manual,
            IsCi = true,
        },
        ArtifactValidation = new()
        {
            ExpectedSourceRevision = sourceRevision,
        },
        GitLab = new()
        {
            InstanceUrl = new("https://gitlab.example/group-prefix"),
            ApiUrl = new("https://api.gitlab.example/custom/v4"),
            ProjectId = 42,
        },
    },
    consume
        ? null
        : new()
        {
            RootDirectory = root,
            ArtifactRootDirectory = new(
                Path.Combine(root.FullName, "output")),
            SourceRevision = sourceRevision,
            MsBuildVersion = new()
            {
                TargetFile = new(
                    Path.Combine(root.FullName, "fixture.props")),
                RecoveryFile = new(
                    Path.Combine(root.FullName, ".recovery.json")),
            },
            BuildTargets = [],
        },
    new()
    {
        RunFormat = false,
    });
await builder.ExecutePipelineAsync().ConfigureAwait(false);
