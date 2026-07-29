using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using Microsoft.CodeAnalysis.CodeFixes;

if (args.Length < 2)
{
    Console.Error.WriteLine(
        "Pass one or more analyzer asset directories followed by the expected assembly count.");
    return 2;
}

var assetRoots = args[..^1]
    .Select(Path.GetFullPath)
    .ToArray();
var expectedAssemblyCount = int.Parse(
    args[^1],
    System.Globalization.CultureInfo.InvariantCulture);
var codeFixAssemblies = assetRoots
    .SelectMany(root => Directory.GetFiles(
        root,
        "*CodeFixes.dll",
        SearchOption.TopDirectoryOnly))
    .ToArray();

if (codeFixAssemblies.Length != expectedAssemblyCount)
{
    Console.Error.WriteLine(
        $"Expected {expectedAssemblyCount} CodeFix assemblies but found {codeFixAssemblies.Length}.");
    return 3;
}

var loadContext = new AnalyzerLoadContext(assetRoots);
var providerCount = 0;

foreach (var assemblyPath in codeFixAssemblies)
{
    var assembly = loadContext.LoadFromAssemblyPath(assemblyPath);
    var providers = assembly.GetTypes()
        .Where(type => !type.IsAbstract
                       && typeof(CodeFixProvider).IsAssignableFrom(type))
        .ToArray();

    if (providers.Length == 0)
    {
        Console.Error.WriteLine(
            $"{Path.GetFileName(assemblyPath)} contains no concrete CodeFixProvider.");
        return 4;
    }

    foreach (var provider in providers)
    {
        _ = Activator.CreateInstance(provider)
            ?? throw new InvalidOperationException(
                $"Could not instantiate {provider.FullName}.");
    }

    providerCount += providers.Length;
}

Console.WriteLine(JsonSerializer.Serialize(
    new
    {
        assemblies = codeFixAssemblies.Length,
        providers = providerCount,
    }));
return 0;

internal sealed class AnalyzerLoadContext(IReadOnlyList<string> assetRoots)
    : AssemblyLoadContext
{
    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var sharedAssembly = Default.Assemblies.FirstOrDefault(
            assembly => AssemblyName.ReferenceMatchesDefinition(
                assembly.GetName(),
                assemblyName));

        if (sharedAssembly is not null)
        {
            return sharedAssembly;
        }

        foreach (var assetRoot in assetRoots)
        {
            var assetPath = Path.Combine(
                assetRoot,
                $"{assemblyName.Name}.dll");

            if (File.Exists(assetPath))
            {
                return LoadFromAssemblyPath(assetPath);
            }
        }

        return null;
    }
}
