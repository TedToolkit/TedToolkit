namespace TedToolkit.ModularPipelines.Combine.Tests;

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
            "TedToolkit.Combine.Tests",
            Guid.NewGuid().ToString("N"));
        var directory = System.IO.Directory.CreateDirectory(path);
        return new(directory);
    }

    public void Dispose()
    {
        if (Directory.Exists)
        {
            Directory.Delete(recursive: true);
        }
    }
}