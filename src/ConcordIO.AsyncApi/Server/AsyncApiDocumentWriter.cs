using Neuroglia.AsyncApi.v3;
using Neuroglia.Serialization;
using Neuroglia.Serialization.Yaml;
using JsonSerializer = Neuroglia.Serialization.Json.JsonSerializer;

namespace ConcordIO.AsyncApi.Server;

/// <summary>
/// Writes AsyncAPI documents to files.
/// </summary>
public class AsyncApiDocumentWriter
{
    /// <summary>
    /// Writes an AsyncAPI document to a YAML file.
    /// </summary>
    /// <param name="document">The document to write.</param>
    /// <param name="outputPath">The output file path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task WriteYamlAsync(
        V3AsyncApiDocument document,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        EnsureDirectoryExists(outputPath);

        var yaml = YamlSerializer.Default.SerializeToText(document);
        await File.WriteAllTextAsync(outputPath, yaml, cancellationToken);
    }

    /// <summary>
    /// Writes an AsyncAPI document to a JSON file.
    /// </summary>
    /// <param name="document">The document to write.</param>
    /// <param name="outputPath">The output file path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task WriteJsonAsync(
        V3AsyncApiDocument document,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        EnsureDirectoryExists(outputPath);

        var json = JsonSerializer.Default.SerializeToText(document);
        await File.WriteAllTextAsync(outputPath, json, cancellationToken);
    }

    private static void EnsureDirectoryExists(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}
