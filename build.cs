#:property TargetFramework=net10.0
#:property PublishAot=false
#:project TedToolkit.ModularPipelines/TedToolkit.ModularPipelines.csproj

using System.Text;

using ModularPipelines.Extensions;

using TedToolkit.ModularPipelines;
using TedToolkit.ModularPipelines.Modules;

Console.OutputEncoding = Encoding.UTF8;

var solutionPath = Environment.GetEnvironmentVariable("BUILD_SOLUTION");
if (string.IsNullOrWhiteSpace(solutionPath))
{
    throw new InvalidOperationException("Set BUILD_SOLUTION to the solution path in the repository being built.");
}

var solution = RequireFile(solutionPath.Trim(), "BUILD_SOLUTION");
var testProjects = (Environment.GetEnvironmentVariable("BUILD_TEST_PROJECTS") ?? "")
    .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
    .Distinct(StringComparer.Ordinal)
    .Select(path => RequireFile(path, "BUILD_TEST_PROJECTS"))
    .ToArray();

var pipeline = new TedPipeline(new()
{
    BuildFiles = [solution,],
    Solution = solution,
    TestFiles = testProjects,
});

var builder = pipeline.CreateNoModules()
    .AddModule<AssertBuildTestModule>()
    .AddModule<FormatAllCodeModule>()
    .AddModule<CleanOutputModule>()
    .AddModule<TestModule>()
    .AddModule<NugetPushModule>()
    .AddModule<DotnetBuildModule>();

if (Environment.GetEnvironmentVariable("BUILD_MANAGE_RELEASE_VERSION") == "true")
{
    builder.AddModule<BumpVersionModule>()
        .AddModule<CreateReleaseModule>();
}

await builder.ExecutePipelineAsync().ConfigureAwait(false);

static FileInfo RequireFile(string path, string variable)
{
    var file = new FileInfo(path);
    if (!file.Exists)
    {
        throw new FileNotFoundException($"The path '{path}' in {variable} does not exist.", file.FullName);
    }

    return file;
}