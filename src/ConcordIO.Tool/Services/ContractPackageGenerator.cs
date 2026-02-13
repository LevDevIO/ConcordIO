namespace ConcordIO.Tool.Services;

/// <summary>
/// Service for generating contract NuGet packages from OpenAPI/Protobuf specifications.
/// </summary>
public class ContractPackageGenerator
{
    private readonly ITemplateRenderer _templateRenderer;
    private readonly IFileSystem _fileSystem;

    public ContractPackageGenerator(ITemplateRenderer templateRenderer, IFileSystem fileSystem)
    {
        _templateRenderer = templateRenderer;
        _fileSystem = fileSystem;
    }

    /// <summary>
    /// Generates the contract package files (.nuspec and .targets).
    /// </summary>
    public async Task<GeneratedPackage> GenerateContractPackageAsync(ContractPackageOptions options)
    {
        _fileSystem.CreateDirectory(options.OutputDirectory);

        var model = BuildContractModel(options);

        return await GeneratePackageAsync(
            options.PackageId,
            options.OutputDirectory,
            "Contract.Contract.nuspec",
            "Contract.Contracts.targets",
            model);
    }

    /// <summary>
    /// Generates the client package files (.nuspec and .targets).
    /// </summary>
    public async Task<GeneratedPackage> GenerateClientPackageAsync(ClientPackageOptions options)
    {
        _fileSystem.CreateDirectory(options.OutputDirectory);

        var model = BuildClientModel(options);

        return await GeneratePackageAsync(
            options.ClientPackageId,
            options.OutputDirectory,
            "Contract.Client.Contract.Client.nuspec",
            "Contract.Client.Contract.Client.targets",
            model);
    }

    /// <summary>
    /// Shared method for generating package files (.nuspec and .targets).
    /// </summary>
    private async Task<GeneratedPackage> GeneratePackageAsync(
        string packageId,
        string outputDir,
        string nuspecTemplate,
        string targetsTemplate,
        Dictionary<string, object> model)
    {
        // Generate nuspec
        var nuspecContent = await _templateRenderer.RenderAsync(nuspecTemplate, model);
        var nuspecPath = Path.Combine(outputDir, $"{packageId}.nuspec");
        await _fileSystem.WriteAllTextAsync(nuspecPath, nuspecContent);

        // Generate targets
        var targetsContent = await _templateRenderer.RenderAsync(targetsTemplate, model);
        var buildDir = Path.Combine(outputDir, "build");
        _fileSystem.CreateDirectory(buildDir);
        var targetsPath = Path.Combine(buildDir, $"{packageId}.targets");
        await _fileSystem.WriteAllTextAsync(targetsPath, targetsContent);

        return new GeneratedPackage
        {
            NuspecPath = nuspecPath,
            NuspecContent = nuspecContent,
            TargetsPath = targetsPath,
            TargetsContent = targetsContent
        };
    }

    private static Dictionary<string, object> BuildContractModel(ContractPackageOptions options)
    {
        var specsByKind = new Dictionary<string, List<string>>(options.SpecsByKind, StringComparer.OrdinalIgnoreCase);

        return new Dictionary<string, object>
        {
            ["package_id"] = options.PackageId,
            ["version"] = options.Version,
            ["authors"] = options.Authors,
            ["description"] = options.Description,
            ["package_properties"] = options.PackageProperties,
            ["specs_by_kind"] = specsByKind,
            ["has_openapi"] = specsByKind.ContainsKey(SpecKind.OpenApi),
            ["has_proto"] = specsByKind.ContainsKey(SpecKind.Proto),
            ["has_asyncapi"] = specsByKind.ContainsKey(SpecKind.AsyncApi)
        };
    }

    private static Dictionary<string, object> BuildClientModel(ClientPackageOptions options)
    {
        var specsByKind = options.SpecsByKind;
        var hasOpenApi = specsByKind.ContainsKey(SpecKind.OpenApi);
        var hasProto = specsByKind.ContainsKey(SpecKind.Proto);
        var hasAsyncApi = specsByKind.ContainsKey(SpecKind.AsyncApi);

        return new Dictionary<string, object>
        {
            ["client_package_id"] = options.ClientPackageId,
            ["version"] = options.Version,
            ["authors"] = options.Authors,
            ["description"] = options.Description,
            ["contract_package_id"] = options.ContractPackageId,
            ["contract_version"] = options.ContractVersion,
            ["package_properties"] = options.PackageProperties,
            ["nswag_client_class_name"] = options.NSwagClientClassName,
            ["nswag_output_path"] = options.NSwagOutputPath,
            ["nswag_options"] = options.NSwagOptions,
            ["client_options"] = options.ClientOptions,
            ["has_openapi"] = hasOpenApi,
            ["has_proto"] = hasProto,
            ["has_asyncapi"] = hasAsyncApi
        };
    }
}

/// <summary>
/// Base class for package generation options.
/// </summary>
public abstract class PackageOptionsBase
{
    public required string Version { get; init; }
    public required string Authors { get; init; }
    public required string Description { get; init; }
    public required string OutputDirectory { get; init; }
    public KeyValuePair<string, string>[] PackageProperties { get; init; } = [];
    public required Dictionary<string, List<string>> SpecsByKind { get; init; }
}

/// <summary>
/// Options for generating a contract package.
/// </summary>
public class ContractPackageOptions : PackageOptionsBase
{
    public required string PackageId { get; init; }
}

/// <summary>
/// Options for generating a client package.
/// </summary>
/// <remarks>
/// Client packages are development dependencies that wire contract specs to code generators.
/// They depend on a contract package and configure tools like NSwag (OpenAPI) or
/// ConcordIO.AsyncApi.Client (AsyncAPI) to generate code at build time.
/// </remarks>
public class ClientPackageOptions : PackageOptionsBase
{
    /// <summary>
    /// The NuGet package ID for the client package (e.g., "MyService.Contracts.Client").
    /// </summary>
    public required string ClientPackageId { get; init; }

    /// <summary>
    /// The NuGet package ID of the contract package this client depends on.
    /// </summary>
    public required string ContractPackageId { get; init; }

    /// <summary>
    /// The version of the contract package to depend on.
    /// </summary>
    public required string ContractVersion { get; init; }

    /// <summary>
    /// The class name for NSwag-generated HTTP clients (e.g., "PetStoreClient").
    /// </summary>
    public required string NSwagClientClassName { get; init; }

    /// <summary>
    /// The output path for NSwag-generated files (e.g., "Generated/").
    /// </summary>
    public required string NSwagOutputPath { get; init; }

    /// <summary>
    /// Additional NSwag configuration options as key-value pairs.
    /// </summary>
    public List<KeyValuePair<string, string>> NSwagOptions { get; init; } = [];

    /// <summary>
    /// Additional client package options as key-value pairs for custom template values.
    /// </summary>
    public List<KeyValuePair<string, string>> ClientOptions { get; init; } = [];
}

/// <summary>
/// Result of package generation.
/// </summary>
public class GeneratedPackage
{
    public required string NuspecPath { get; init; }
    public required string NuspecContent { get; init; }
    public required string TargetsPath { get; init; }
    public required string TargetsContent { get; init; }
}
