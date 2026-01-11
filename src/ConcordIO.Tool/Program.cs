using System.Reflection;
using DotMake.CommandLine;
using Scriban;

namespace ConcordIO.Tool;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        return await Cli.RunAsync<RootCommand>(args, new CliSettings { EnableDefaultExceptionHandler = true });
    }
}

[CliCommand(Description = "ConcordIO - CLI tool for generating OpenAPI and Protobuf contract packages")]
public class RootCommand
{
    [CliCommand(Name = "generate", Description = "Generate contract NuGet packages from OpenAPI or Protobuf specifications")]
    public class GenerateCommand
    {
        [CliOption(Description = "Path to the OpenAPI/Protobuf specification file", Required = true)]
        public required string Spec { get; set; }

        [CliOption(Description = "Package ID for the generated NuGet package", Required = true)]
        public required string PackageId { get; set; }

        [CliOption(Description = "Package version", Required = true)]
        public required string Version { get; set; }

        [CliOption(Description = "Package authors", Required = false)]
        public string Authors { get; set; } = "ConcordIO";

        [CliOption(Description = "Package description", Required = false)]
        public string? Description { get; set; }

        [CliOption(Description = "Contract kind: openapi or proto", Required = false)]
        public string Kind { get; set; } = "openapi";

        [CliOption(Description = "Output directory for generated files", Required = false)]
        public string Output { get; set; } = ".";

        [CliOption(Description = "Also generate client package", Required = false)]
        public bool Client { get; set; } = true;

        [CliOption(Description = "Client package ID (defaults to PackageId.Client)", Required = false)]
        public string? ClientPackageId { get; set; }

        [CliOption(Description = "Client class name (for client generation)", Required = false)]
        public string? ClientClassName { get; set; }

        [CliOption(Description = "Additional NSwag options in key=value format (can be specified multiple times)", Required = false)]
        public string[]? NswagOptions { get; set; }

        [CliOption(Description = "Additional package properties in key=value format (can be specified multiple times)", Required = false)]
        public string[]? PackageProperties { get; set; }

        public async Task<int> RunAsync()
        {
            var specFileName = Path.GetFileName(Spec);
            var description = Description ?? $"{Kind} specification for {PackageId}";

            // Generate Contract package
            await GenerateContractPackageAsync(specFileName, description);

            // Generate Client package if requested
            if (Client)
            {
                await GenerateClientPackageAsync(specFileName, description);
            }

            Console.WriteLine($"Successfully generated package(s) in: {Path.GetFullPath(Output)}");
            return 0;
        }

        private async Task GenerateContractPackageAsync(string specFileName, string description)
        {
            Directory.CreateDirectory(Output);

            var model = new Dictionary<string, object>
            {
                ["package_id"] = PackageId,
                ["version"] = Version,
                ["authors"] = Authors,
                ["description"] = description,
                ["spec_file"] = specFileName,
                ["contract_kind"] = Kind
            };

            // Generate nuspec
            var nuspecContent = await RenderTemplateAsync("Contract.Contract.nuspec", model);
            var nuspecPath = Path.Combine(Output, $"{PackageId}.nuspec");
            await File.WriteAllTextAsync(nuspecPath, nuspecContent);
            Console.WriteLine($"Generated: {nuspecPath}");

            // Generate targets
            var targetsContent = await RenderTemplateAsync("Contract.Contracts.targets", model);
            var buildDir = Path.Combine(Output, "build");
            Directory.CreateDirectory(buildDir);
            var targetsPath = Path.Combine(buildDir, $"{PackageId}.targets");
            await File.WriteAllTextAsync(targetsPath, targetsContent);
            Console.WriteLine($"Generated: {targetsPath}");
        }

        private async Task GenerateClientPackageAsync(string specFileName, string description)
        {
            var clientClass = ClientClassName ?? $"{SanitizeClassName(PackageId)}Client";
            var clientPackageId = ClientPackageId ?? $"{PackageId}.Client";

            var model = new Dictionary<string, object>
            {
                ["client_package_id"] = clientPackageId,
                ["version"] = Version,
                ["authors"] = Authors,
                ["description"] = $"Client generator for {PackageId}. Adds {Kind} specification for code generation.",
                ["contract_package_id"] = PackageId,
                ["contract_version"] = Version,
                ["contract_kind"] = Kind,
                ["package_properties"] = ParseKeyValuePairs(PackageProperties),
                ["nswag_client_class_name"] = clientClass,
                ["nswag_output_path"] = clientClass,
                ["nswag_generate_exception_classes"] = "true",
                ["nswag_options"] = ParseKeyValuePairs(NswagOptions)
            };

            // Generate client nuspec
            var nuspecContent = await RenderTemplateAsync("Contract.Client.Contract.Client.nuspec", model);
            var nuspecPath = Path.Combine(Output, $"{clientPackageId}.nuspec");
            await File.WriteAllTextAsync(nuspecPath, nuspecContent);
            Console.WriteLine($"Generated: {nuspecPath}");

            // Generate client targets
            var targetsContent = await RenderTemplateAsync("Contract.Client.Contract.Client.targets", model);
            var buildDir = Path.Combine(Output, "build");
            Directory.CreateDirectory(buildDir);
            var targetsPath = Path.Combine(buildDir, $"{clientPackageId}.targets");
            await File.WriteAllTextAsync(targetsPath, targetsContent);
            Console.WriteLine($"Generated: {targetsPath}");
        }

        private static string SanitizeClassName(string name) =>
            string.Concat(name.Split('.').Select(part =>
                    char.ToUpperInvariant(part[0]) + part[1..]));

        private static KeyValuePair<string, string>[] ParseKeyValuePairs(string[]? pairs) =>
            pairs?.Select(pair =>
            {
                var parts = pair.Split('=', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                if (parts.Length != 2)
                    throw new ArgumentException($"Invalid key=value format: '{pair}'");

                return new KeyValuePair<string, string>(parts[0], parts[1]);
            }).ToArray() ?? [];

        private static async Task<string> RenderTemplateAsync(string templateName, Dictionary<string, object> model)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = $"ConcordIO.Tool.Templates.{templateName}";

            using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Template not found: {templateName}");
            using var reader = new StreamReader(stream);
            var templateContent = await reader.ReadToEndAsync();

            var template = Template.Parse(templateContent);
            if (template.HasErrors)
            {
                throw new InvalidOperationException($"Template parse error: {string.Join(", ", template.Messages)}");
            }

            return template.Render(model);
        }
    }
}
