using ConcordIO.Tool.Services;
using DotMake.CommandLine;

namespace ConcordIO.Tool.CliCommands;

[CliCommand(Description = "ConcordIO - CLI tool for generating OpenAPI, Protobuf, and AsyncAPI contract packages")]
public partial class RootCommand
{
    [CliCommand(Name = "generate", Description = "Generate contract NuGet packages from OpenAPI, Protobuf, or AsyncAPI specifications")]
    public class GenerateCommand
    {
        [CliOption(Description = "Specification file(s) with optional kind (format: path[:kind], kind defaults to openapi). Can be specified multiple times.", Required = true)]
        public string[] Spec { get; set; } = [];

        [CliOption(Description = "Package ID for the generated NuGet package", Required = true)]
        public required string PackageId { get; set; }

        [CliOption(Description = "Package version", Required = true)]
        public required string Version { get; set; }

        [CliOption(Description = "Package authors", Required = false)]
        public string Authors { get; set; } = "ConcordIO";

        [CliOption(Description = "Package description", Required = false)]
        public string? Description { get; set; }

        [CliOption(Description = "Output directory for generated files", Required = false)]
        public string Output { get; set; } = ".";

        [CliOption(Description = "Also generate client package", Required = false)]
        public bool Client { get; set; } = true;

        [CliOption(Description = "Client package ID (defaults to PackageId.Client)", Required = false)]
        public string? ClientPackageId { get; set; }

        [CliOption(Description = "Client class name (for OpenAPI client generation)", Required = false)]
        public string? ClientClassName { get; set; }

        [CliOption(Description = "Additional NSwag options in key=value format (OpenAPI only)", Required = false)]
        public string[]? NswagOptions { get; set; }

        [CliOption(Description = "Additional client options in key=value format (AsyncAPI only)", Required = false)]
        public string[]? ClientOptions { get; set; }

        [CliOption(Description = "Additional package properties in key=value format", Required = false)]
        public string[]? PackageProperties { get; set; }

        /// <summary>
        /// Represents a parsed specification entry with file name and kind.
        /// </summary>
        private record SpecEntry(string FileName, string Kind);

        private static readonly string[] ValidKinds = SpecKind.All;

        public async Task<int> RunAsync()
        {
            // Parse spec entries
            var specs = ParseSpecEntries(Spec);
            if (specs.Count == 0)
            {
                Console.Error.WriteLine("Error: At least one specification file is required.");
                return 1;
            }

            // Validate all kinds
            var invalidKinds = specs.Select(s => s.Kind).Distinct().Except(ValidKinds).ToList();
            if (invalidKinds.Count > 0)
            {
                Console.Error.WriteLine($"Error: Invalid kind(s): {string.Join(", ", invalidKinds)}. Must be 'openapi', 'proto', or 'asyncapi'.");
                return 1;
            }

            // Group specs by kind
            var specsByKind = specs
                .GroupBy(s => s.Kind)
                .ToDictionary(g => g.Key, g => g.Select(s => s.FileName).ToList());

            var kindsSummary = string.Join(", ", specsByKind.Select(kvp => $"{kvp.Value.Count} {kvp.Key}"));
            var description = Description ?? $"Contract specifications for {PackageId} ({kindsSummary})";

            // Generate Contract package
            await GenerateContractPackageAsync(specsByKind, description);

            // Generate Client package if requested
            if (Client)
            {
                await GenerateClientPackageAsync(specsByKind, description);
            }

            Console.WriteLine($"Successfully generated package(s) in: {Path.GetFullPath(Output)}");
            return 0;
        }

        private List<SpecEntry> ParseSpecEntries(string[] specArgs)
        {
            var entries = new List<SpecEntry>();

            foreach (var spec in specArgs)
            {
                var colonIndex = spec.LastIndexOf(':');

                // Check if colon is part of a Windows path (e.g., C:\path)
                if (colonIndex > 1 && spec.Length > colonIndex + 1)
                {
                    var possibleKind = spec[(colonIndex + 1)..].ToLowerInvariant();
                    if (ValidKinds.Contains(possibleKind))
                    {
                        var filePath = spec[..colonIndex];
                        entries.Add(new SpecEntry(Path.GetFileName(filePath), possibleKind));
                        continue;
                    }
                }

                // No valid kind suffix, default to openapi
                entries.Add(new SpecEntry(Path.GetFileName(spec), SpecKind.OpenApi));
            }

            return entries;
        }

        private async Task GenerateContractPackageAsync(Dictionary<string, List<string>> specsByKind, string description)
        {
            var generator = CreateGenerator();
            var options = new ContractPackageOptions
            {
                PackageId = PackageId,
                Version = Version,
                Authors = Authors,
                Description = description,
                OutputDirectory = Output,
                PackageProperties = ParseKeyValuePairs(PackageProperties),
                SpecsByKind = specsByKind
            };

            var result = await generator.GenerateContractPackageAsync(options);
            Console.WriteLine($"Generated: {result.NuspecPath}");
            Console.WriteLine($"Generated: {result.TargetsPath}");
        }

        private async Task GenerateClientPackageAsync(Dictionary<string, List<string>> specsByKind, string description)
        {
            var clientPackageId = ClientPackageId ?? $"{PackageId}.Client";
            var hasOpenApi = specsByKind.ContainsKey(SpecKind.OpenApi);
            var hasAsyncApi = specsByKind.ContainsKey(SpecKind.AsyncApi);

            var clientClass = ClientClassName ?? $"{SanitizeClassName(PackageId)}Client";
            var normalizedNswagOptions = GetNormalizedNswagOptions(hasOpenApi);
            var clientOptions = hasAsyncApi
                ? ParseKeyValuePairs(ClientOptions)
                    .Select(kvp => new KeyValuePair<string, string>(NormalizePrefix("ConcordIOClient", kvp.Key), kvp.Value))
                    .ToList()
                : [];

            var generator = CreateGenerator();
            var options = new ClientPackageOptions
            {
                ClientPackageId = clientPackageId,
                ContractPackageId = PackageId,
                ContractVersion = Version,
                Version = Version,
                Authors = Authors,
                Description = $"Client generator for {PackageId}. Generates code from contract specifications.",
                OutputDirectory = Output,
                NSwagClientClassName = clientClass,
                NSwagOutputPath = clientClass,
                NSwagOptions = normalizedNswagOptions,
                ClientOptions = clientOptions,
                PackageProperties = ParseKeyValuePairs(PackageProperties),
                SpecsByKind = specsByKind
            };

            var result = await generator.GenerateClientPackageAsync(options);
            Console.WriteLine($"Generated: {result.NuspecPath}");
            Console.WriteLine($"Generated: {result.TargetsPath}");
        }

        private static string SanitizeClassName(string name) =>
            string.Concat(name.Split('.').Select(part =>
                    char.ToUpperInvariant(part[0]) + part[1..]));

        private static string NormalizePrefix(string prefix, string value)
        {
            if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }
            return prefix + value;
        }

        /// <summary>
        /// Not a dictionary because multiple key-values with the same key is expected
        /// it's the command line argument format for arrays
        /// </summary>
        private static KeyValuePair<string, string>[] ParseKeyValuePairs(string[]? pairs) =>
            pairs?.Select(pair =>
            {
                var parts = pair.Split('=', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                if (parts.Length != 2)
                    throw new ArgumentException($"Invalid key=value format: '{pair}'");

                return new KeyValuePair<string, string>(parts[0], parts[1]);
            }).ToArray() ?? [];

        private static ContractPackageGenerator CreateGenerator() =>
            new(new TemplateRenderer(), new FileSystem());

        private List<KeyValuePair<string, string>> GetNormalizedNswagOptions(bool hasOpenApi)
        {
            if (!hasOpenApi)
            {
                return [];
            }

            var parsedNswagOptions = ParseKeyValuePairs(NswagOptions);
            var normalizedNswagOptions = parsedNswagOptions
                .Select(kvp => new KeyValuePair<string, string>(NormalizePrefix("NSwag", kvp.Key), kvp.Value))
                .ToList();

            var stjOptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "NSwagJsonLibrary", "SystemTextJson" },
                { "NSwagJsonPolymorphicSerializationStyle", "SystemTextJson" }
            };

            if (!normalizedNswagOptions.Any(o => stjOptions.ContainsKey(o.Key)))
            {
                normalizedNswagOptions.AddRange(stjOptions);
            }

            if (!normalizedNswagOptions.Any(o => string.Equals("NSwagGenerateExceptionClasses", o.Key, StringComparison.OrdinalIgnoreCase)))
            {
                normalizedNswagOptions.Add(new("NSwagGenerateExceptionClasses", "true"));
            }

            return normalizedNswagOptions;
        }
    }
}