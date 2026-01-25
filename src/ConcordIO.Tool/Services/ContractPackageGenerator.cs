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

        // Generate nuspec
        var nuspecContent = await _templateRenderer.RenderAsync("Contract.Contract.nuspec", model);
        var nuspecPath = Path.Combine(options.OutputDirectory, $"{options.PackageId}.nuspec");
        await _fileSystem.WriteAllTextAsync(nuspecPath, nuspecContent);

        // Generate targets
        var targetsContent = await _templateRenderer.RenderAsync("Contract.Contracts.targets", model);
        var buildDir = Path.Combine(options.OutputDirectory, "build");
        _fileSystem.CreateDirectory(buildDir);
        var targetsPath = Path.Combine(buildDir, $"{options.PackageId}.targets");
        await _fileSystem.WriteAllTextAsync(targetsPath, targetsContent);

        return new GeneratedPackage
        {
            NuspecPath = nuspecPath,
            NuspecContent = nuspecContent,
            TargetsPath = targetsPath,
            TargetsContent = targetsContent
        };
    }

    /// <summary>
    /// Generates the client package files (.nuspec and .targets).
    /// </summary>
    public async Task<GeneratedPackage> GenerateClientPackageAsync(ClientPackageOptions options)
    {
        _fileSystem.CreateDirectory(options.OutputDirectory);

        var model = BuildClientModel(options);

        // Generate client nuspec
        var nuspecContent = await _templateRenderer.RenderAsync("Contract.Client.Contract.Client.nuspec", model);
        var nuspecPath = Path.Combine(options.OutputDirectory, $"{options.ClientPackageId}.nuspec");
        await _fileSystem.WriteAllTextAsync(nuspecPath, nuspecContent);

        // Generate client targets
        var targetsContent = await _templateRenderer.RenderAsync("Contract.Client.Contract.Client.targets", model);
        var buildDir = Path.Combine(options.OutputDirectory, "build");
        _fileSystem.CreateDirectory(buildDir);
        var targetsPath = Path.Combine(buildDir, $"{options.ClientPackageId}.targets");
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
        return new Dictionary<string, object>
        {
            ["package_id"] = options.PackageId,
            ["version"] = options.Version,
            ["authors"] = options.Authors,
            ["description"] = options.Description,
            ["spec_file"] = options.SpecFileName,
            ["contract_kind"] = options.Kind,
            ["package_properties"] = options.PackageProperties
        };
    }

    private static Dictionary<string, object> BuildClientModel(ClientPackageOptions options)
    {
        return new Dictionary<string, object>
        {
            ["client_package_id"] = options.ClientPackageId,
            ["version"] = options.Version,
            ["authors"] = options.Authors,
            ["description"] = options.Description,
            ["contract_package_id"] = options.ContractPackageId,
            ["contract_version"] = options.ContractVersion,
            ["contract_kind"] = options.Kind,
            ["package_properties"] = options.PackageProperties,
            ["nswag_client_class_name"] = options.NSwagClientClassName,
            ["nswag_output_path"] = options.NSwagOutputPath,
            ["nswag_options"] = options.NSwagOptions
        };
    }
}

/// <summary>
/// Options for generating a contract package.
/// </summary>
public class ContractPackageOptions
{
    public required string PackageId { get; init; }
    public required string Version { get; init; }
    public required string Authors { get; init; }
    public required string Description { get; init; }
    public required string SpecFileName { get; init; }
    public required string Kind { get; init; }
    public required string OutputDirectory { get; init; }
    public KeyValuePair<string, string>[] PackageProperties { get; init; } = [];
}

/// <summary>
/// Options for generating a client package.
/// </summary>
public class ClientPackageOptions
{
    public required string ClientPackageId { get; init; }
    public required string ContractPackageId { get; init; }
    public required string ContractVersion { get; init; }
    public required string Version { get; init; }
    public required string Authors { get; init; }
    public required string Description { get; init; }
    public required string Kind { get; init; }
    public required string OutputDirectory { get; init; }
    public required string NSwagClientClassName { get; init; }
    public required string NSwagOutputPath { get; init; }
    public KeyValuePair<string, string>[] PackageProperties { get; init; } = [];
    public List<KeyValuePair<string, string>> NSwagOptions { get; init; } = [];
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
