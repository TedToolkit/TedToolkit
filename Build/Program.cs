// See https://aka.ms/new-console-template for more information

using Sourcy.DotNet;

using TedToolkit.ModularPipelines;
using TedToolkit.ModularPipelines.Modules;

Console.OutputEncoding = System.Text.Encoding.UTF8;

var pipeline = new TedPipeline(
    new()
    {
        BuildFiles =
        [
            Solutions.TedToolkit,
        ],
        Solution = Solutions.TedToolkit,
        TestFiles = [],
    },
    new FileInfo(Path.Combine(Projects.Build.Directory!.FullName, "appsettings.json")));


var builder = pipeline.CreateNoModules()
    .AddModule<GenerateCommitMessageModule>()
    .AddModule<AssertBuildTestModule>()
    .AddModule<FormatAllCodeModule>()
    .AddModule<CleanOutputModule>()
    .AddModule<TestModule>()
    .AddModule<NugetPushModule>()
    .AddModule<DotnetBuildModule>();
await builder.ExecutePipelineAsync().ConfigureAwait(false);