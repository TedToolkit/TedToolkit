namespace TedToolkit.ModularPipelines.Build.Tests;

internal sealed class TemporaryDirectory : IDisposable
{
    private TemporaryDirectory(DirectoryInfo directory)
    {
        Directory = directory;
    }

    public DirectoryInfo Directory { get; }

    public static TemporaryDirectory Create()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "TedToolkit.ModularPipelines.Build.Tests",
            Guid.NewGuid().ToString("N"));
        return new TemporaryDirectory(System.IO.Directory.CreateDirectory(path));
    }

    public void Dispose()
    {
        if (!Directory.Exists)
        {
            return;
        }

        var expectedRoot = Path.GetFullPath(Path.Combine(
            Path.GetTempPath(),
            "TedToolkit.ModularPipelines.Build.Tests"));
        var fullPath = Path.GetFullPath(Directory.FullName);

        if (!fullPath.StartsWith(
                string.Concat(
                    Path.TrimEndingDirectorySeparator(expectedRoot),
                    Path.DirectorySeparatorChar),
                OperatingSystem.IsWindows()
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The temporary directory escaped its owned test root.");
        }

        foreach (var path in System.IO.Directory.EnumerateFiles(
                     fullPath,
                     "*",
                     SearchOption.AllDirectories))
        {
            File.SetAttributes(
                path,
                File.GetAttributes(path) & ~FileAttributes.ReadOnly);
        }

        Directory.Delete(recursive: true);
    }
}