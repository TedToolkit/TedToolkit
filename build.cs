#:property TargetFramework=net10.0
#:property PublishAot=false

using System.Diagnostics;

const string DisableBuildPacking = "-p:GeneratePackageOnBuild=false";
var configuration = Environment.GetEnvironmentVariable("BUILD_CONFIGURATION") ?? "Release";

try
{
    return ReadRequired("BUILD_STAGE") switch
    {
        "build" => await RunDotNetAsync(["build", ReadFile("BUILD_SOLUTION"), "-c", configuration, DisableBuildPacking]),
        "test" => await TestAsync(ReadProjects("BUILD_TEST_PROJECTS"), configuration),
        "pack" => await PackAsync(ReadProjects("BUILD_PACK_PROJECTS"), configuration),
        "publish" => await PublishAsync(ReadRequired("NUGET_SOURCE"), ReadRequired("NUGET_API_KEY")),
        _ => throw new InvalidOperationException("BUILD_STAGE must be build, test, pack, or publish."),
    };
}
catch (Exception error) when (error is IOException or InvalidOperationException)
{
    Console.Error.WriteLine(error.Message);
    return 1;
}

static async Task<int> TestAsync(string[] projects, string configuration)
{
    var output = Directory.CreateDirectory("output/test").FullName;
    var failed = false;
    foreach (var (project, index) in projects.Select((path, index) => (path, index)))
    {
        var report = $"{Path.GetFileNameWithoutExtension(project)}-{index + 1}.trx";
        var reportPath = Path.Combine(output, report);
        File.Delete(reportPath);

        var exitCode = await RunDotNetAsync([
            "run", "--project", project, "-c", configuration, "--no-launch-profile",
            DisableBuildPacking, "--", "--report-trx", "--results-directory", output, "--report-trx-filename", report,
        ]);
        if (exitCode != 0 || !File.Exists(reportPath))
        {
            Console.Error.WriteLine($"Test project '{project}' failed or did not produce a TRX report (exit code {exitCode}).");
            failed = true;
        }
    }

    return failed ? 1 : 0;
}

static async Task<int> PackAsync(string[] projects, string configuration)
{
    var output = Directory.CreateDirectory("output/nuget").FullName;
    if (Directory.EnumerateFileSystemEntries(output).Any())
    {
        throw new InvalidOperationException("Clear output/nuget before packaging.");
    }

    var version = Environment.GetEnvironmentVariable("BUILD_PACKAGE_VERSION");
    string[] versionArguments = string.IsNullOrWhiteSpace(version)
        ? []
        : [$"-p:Version={version}", $"-p:PackageVersion={version}"];

    foreach (var project in projects)
    {
        var exitCode = await RunDotNetAsync([
            "pack", project, "-c", configuration, "-o", output, DisableBuildPacking, .. versionArguments,
        ]);
        if (exitCode != 0)
        {
            return exitCode;
        }
    }

    _ = ReadPackages();
    return 0;
}

static async Task<int> PublishAsync(string source, string apiKey)
{
    foreach (var package in ReadPackages())
    {
        var exitCode = await RunDotNetAsync([
            "nuget", "push", package, "--source", source, "--api-key", apiKey, "--skip-duplicate",
        ]);
        if (exitCode != 0)
        {
            return exitCode;
        }
    }

    return 0;
}

static string[] ReadPackages()
{
    const string output = "output/nuget";
    var packages = Directory.Exists(output)
        ? Directory.GetFiles(output, "*.nupkg")
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

static string ReadFile(string variable) => RequireFile(ReadRequired(variable), variable);

static string[] ReadProjects(string variable) => ReadRequired(variable)
    .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
    .Select(path => RequireFile(path, variable))
    .Distinct(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal)
    .ToArray();

static string RequireFile(string path, string variable)
{
    var fullPath = Path.GetFullPath(path);
    return File.Exists(fullPath)
        ? fullPath
        : throw new FileNotFoundException($"The path '{path}' in {variable} does not exist.", fullPath);
}

static async Task<int> RunDotNetAsync(string[] arguments)
{
    var startInfo = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet");
    foreach (var argument in arguments)
    {
        startInfo.ArgumentList.Add(argument);
    }

    using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start dotnet.");
    await process.WaitForExitAsync();
    return process.ExitCode;
}