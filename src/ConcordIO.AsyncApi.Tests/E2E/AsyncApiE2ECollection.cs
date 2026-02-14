namespace ConcordIO.AsyncApi.Tests.E2E;

[CollectionDefinition(Name, DisableParallelization = true)]
public class AsyncApiE2ECollection : ICollectionFixture<AsyncApiPackageFixture>
{
    public const string Name = "AsyncApi E2E";
}

internal static class AsyncApiE2ECommandVerbosity
{
    // Use 'normal' verbosity in CI to reduce output volume and prevent pipe buffer deadlocks.
    // Local debugging can override via environment variable if needed.
    private const string DotNetVerbosity = "-v normal";

    public static string AddDotNetVerbosity(string args)
    {
        if (string.IsNullOrWhiteSpace(args))
        {
            return DotNetVerbosity;
        }

        var normalizedArgs = NormalizeDotNetVerbosityArgs(args);
        return string.IsNullOrWhiteSpace(normalizedArgs)
            ? DotNetVerbosity
            : $"{normalizedArgs} {DotNetVerbosity}";
    }

    private static string NormalizeDotNetVerbosityArgs(string args)
    {
        var parts = args.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var filtered = new List<string>(parts.Length);

        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            if (string.Equals(part, "-v", StringComparison.OrdinalIgnoreCase)
                || string.Equals(part, "--verbosity", StringComparison.OrdinalIgnoreCase))
            {
                i++;
                continue;
            }

            if (part.StartsWith("--verbosity=", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            filtered.Add(part);
        }

        return string.Join(' ', filtered);
    }
}
