#:property TargetFramework=net10.0
#:property PublishAot=false
#:property ManagePackageVersionsCentrally=false
#:package TedToolkit.FileSystem@2026.10.16
#:package TedToolkit.CiContext.GitHubActions@2026.10.16

using System.Diagnostics;
using System.Globalization;

using TedToolkit.FileSystem;
using TedToolkit.GitHub;

const string DisableBuildPacking = "-p:GeneratePackageOnBuild=false";
var configuration = Environment.GetEnvironmentVariable("BUILD_CONFIGURATION") ?? "Release";

try
{
    return ReadRequired("BUILD_STAGE") switch
    {
        "generate" => await RunDotNetAsync(["run", "--project", ReadFile("BUILD_GENERATOR_PROJECT").FullName, "-c", configuration, "--no-launch-profile", DisableBuildPacking]),
        "build" => await RunDotNetAsync(["build", ReadFile("BUILD_SOLUTION").FullName, "-c", configuration, DisableBuildPacking]),
        "test" => await TestAsync(ReadProjects("BUILD_TEST_PROJECTS"), configuration),
        "pack" => await PackAsync(ReadProjects("BUILD_PACK_PROJECTS"), configuration),
        "publish" => await PublishAsync(ReadRequired("NUGET_SOURCE"), ReadRequired("NUGET_API_KEY")),
        "version" => await ResolveVersionAsync(),
        "release" => await CreateReleaseAsync(),
        _ => throw new InvalidOperationException("Unknown BUILD_STAGE."),
    };
}
catch (Exception error) when (error is IOException or InvalidOperationException or System.ComponentModel.Win32Exception)
{
    Console.Error.WriteLine(error.Message);
    return 1;
}

static async Task<int> TestAsync(FilePath[] projects, string configuration)
{
    var buildExit = await PrepareSolutionAsync(configuration, []);
    if (buildExit != 0)
    {
        return buildExit;
    }

    var framework = Environment.GetEnvironmentVariable("BUILD_TEST_FRAMEWORK");
    string[] frameworkArguments = string.IsNullOrWhiteSpace(framework) ? [] : ["--framework", framework];
    var output = (DirectoryPath.CurrentDirectory / "output" / "test").Create();
    var failed = false;
    foreach (var (project, index) in projects.Select((path, index) => (path, index)))
    {
        var report = output / $"{project.NameWithoutExtension}-{index + 1}".AsFileName("trx");
        report.Delete();

        var exitCode = await RunDotNetAsync([
            "run", "--project", project.FullName, "-c", configuration, "--no-launch-profile", .. frameworkArguments,
            DisableBuildPacking, "--", "--report-trx", "--results-directory", output.FullName, "--report-trx-filename", report.Name,
        ]);
        if (exitCode != 0 || !report.Exists)
        {
            Console.Error.WriteLine($"Test project '{project.FullName}' failed or did not produce a TRX report (exit code {exitCode}).");
            failed = true;
        }
    }

    return failed ? 1 : 0;
}

static async Task<int> PackAsync(FilePath[] projects, string configuration)
{
    var output = PackageDirectory().Create();
    if (output.EnumerateFileSystemEntries().Any())
    {
        throw new InvalidOperationException("Clear output/nuget before packaging.");
    }

    var version = Environment.GetEnvironmentVariable("BUILD_PACKAGE_VERSION");
    string[] versionArguments = string.IsNullOrWhiteSpace(version)
        ? []
        : [$"-p:Version={version}", $"-p:PackageVersion={version}"];
    var buildExit = await PrepareSolutionAsync(configuration, versionArguments);
    if (buildExit != 0)
    {
        return buildExit;
    }

    foreach (var project in projects)
    {
        var exitCode = await RunDotNetAsync([
            "pack", project.FullName, "-c", configuration, "-o", output.FullName, DisableBuildPacking, .. versionArguments,
        ]);
        if (exitCode != 0)
        {
            return exitCode;
        }
    }

    _ = ReadPackages();
    return 0;
}

static Task<int> PrepareSolutionAsync(string configuration, string[] properties)
{
    var solution = Environment.GetEnvironmentVariable("BUILD_SOLUTION");
    return string.IsNullOrWhiteSpace(solution)
        ? Task.FromResult(0)
        : RunDotNetAsync([
            "build", RequireFile(solution.Trim(), "BUILD_SOLUTION").FullName, "-c", configuration,
            DisableBuildPacking, .. properties,
        ]);
}

static async Task<int> PublishAsync(string source, string apiKey)
{
    foreach (var package in ReadPackages())
    {
        var exitCode = await RunDotNetAsync([
            "nuget", "push", package.FullName, "--source", source, "--api-key", apiKey, "--skip-duplicate",
        ]);
        if (exitCode != 0)
        {
            return exitCode;
        }
    }

    return 0;
}

static DirectoryPath PackageDirectory() => DirectoryPath.CurrentDirectory / "output" / "nuget";

static FilePath[] ReadPackages()
{
    var output = PackageDirectory();
    var packages = output.Exists
        ? output.EnumerateFiles("*.nupkg").ToArray()
        : [];
    return packages.Length > 0
        ? packages
        : throw new InvalidOperationException("No NuGet packages were found in output/nuget.");
}

static string ReadRequired(string variable)
{
    var value = Environment.GetEnvironmentVariable(variable);
    return string.IsNullOrWhiteSpace(value)
        ? throw new InvalidOperationException($"Set the {variable} environment variable.")
        : value.Trim();
}

static FilePath ReadFile(string variable) => RequireFile(ReadRequired(variable), variable);

static FilePath[] ReadProjects(string variable) => ReadRequired(variable)
    .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
    .Select(path => RequireFile(path, variable))
    .DistinctBy(file => file.FullName, OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal)
    .ToArray();

static FilePath RequireFile(string path, string variable)
{
    var file = new FilePath(path).GetFullPath(DirectoryPath.CurrentDirectory);
    return file.Exists
        ? file
        : throw new FileNotFoundException($"The path '{path}' in {variable} does not exist.", file.FullName);
}

static async Task<int> ResolveVersionAsync()
{
    var repository = GitHubActions.Repository.NameWithOwner
        ?? throw new InvalidOperationException("GitHub Actions repository information is required.");
    var output = GitHubActions.WorkflowCommands.OutputFile
        ?? throw new InvalidOperationException("GitHub Actions step output is required.");
    using var process = StartProcess("gh", [
        "api", $"repos/{repository}/releases?per_page=1", "--jq", ".[0].tag_name // \"\"",
    ], captureOutput: true);
    var latestTag = (await process.StandardOutput.ReadToEndAsync()).Trim();
    await process.WaitForExitAsync();
    if (process.ExitCode != 0)
    {
        return process.ExitCode;
    }

    var date = DateTime.UtcNow;
    var revision = Version.TryParse(latestTag, out var latest)
        && latest.Major == date.Year && latest.Minor == date.Month && latest.Build == date.Day
        ? Math.Max(0, latest.Revision) + 1
        : 0;
    output.AppendAllText(FormattableString.Invariant($"value={date.Year}.{date.Month}.{date.Day}.{revision}\n"));
    return 0;
}

static Task<int> CreateReleaseAsync()
{
    var sha = GitHubActions.Ref.Sha
        ?? throw new InvalidOperationException("The GitHub Actions commit SHA is required.");
    return RunProcessAsync("gh", ["release", "create", ReadRequired("RELEASE_VERSION"), "--target", sha, "--generate-notes"]);
}

static Task<int> RunDotNetAsync(string[] arguments) =>
    RunProcessAsync(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet", arguments);

static async Task<int> RunProcessAsync(string executable, string[] arguments)
{
    using var process = StartProcess(executable, arguments);
    await process.WaitForExitAsync();
    return process.ExitCode;
}

static Process StartProcess(string executable, string[] arguments, bool captureOutput = false)
{
    var startInfo = new ProcessStartInfo(executable) { RedirectStandardOutput = captureOutput };
    foreach (var argument in arguments)
    {
        startInfo.ArgumentList.Add(argument);
    }

    return Process.Start(startInfo) ?? throw new InvalidOperationException($"Could not start {executable}.");
}