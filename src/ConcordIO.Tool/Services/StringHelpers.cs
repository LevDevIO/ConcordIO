namespace ConcordIO.Tool.Services;

/// <summary>
/// Helper methods for string manipulation.
/// </summary>
public static class StringHelpers
{
    /// <summary>
    /// Converts a package ID to a valid C# class name by removing dots and capitalizing each segment.
    /// </summary>
    /// <example>
    /// "My.Package.Name" => "MyPackageName"
    /// </example>
    public static string SanitizeClassName(string name) =>
        string.Concat(name.Split('.').Select(part =>
            char.ToUpperInvariant(part[0]) + part[1..]));

    /// <summary>
    /// Ensures a string has the specified prefix (case-insensitive check).
    /// </summary>
    /// <example>
    /// NormalizePrefix("NSwag", "JsonLibrary") => "NSwagJsonLibrary"
    /// NormalizePrefix("NSwag", "NSwagJsonLibrary") => "NSwagJsonLibrary"
    /// </example>
    public static string NormalizePrefix(string prefix, string value)
    {
        if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }
        return prefix + value;
    }

    /// <summary>
    /// Parses an array of "key=value" strings into key-value pairs.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when a pair is not in valid key=value format.</exception>
    public static KeyValuePair<string, string>[] ParseKeyValuePairs(string[]? pairs) =>
        pairs?.Select(pair =>
        {
            var parts = pair.Split('=', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (parts.Length != 2)
                throw new ArgumentException($"Invalid key=value format: '{pair}'");

            return new KeyValuePair<string, string>(parts[0], parts[1]);
        }).ToArray() ?? [];
}
