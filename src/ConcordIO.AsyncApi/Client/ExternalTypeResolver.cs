using System.Reflection;
using System.Runtime.Loader;

namespace ConcordIO.AsyncApi.Client;

/// <summary>
/// Resolves types from external assemblies to determine if they should be generated
/// or referenced from existing assemblies.
/// </summary>
/// <remarks>
/// <para>
/// This resolver is essential for avoiding duplicate type definitions when generating
/// contracts. It scans referenced assemblies to identify types that already exist,
/// allowing the generator to reference them instead of creating new definitions.
/// </para>
/// <para>
/// The resolver uses a separate <see cref="System.Runtime.Loader.AssemblyLoadContext"/>
/// to load assemblies, which can be unloaded when the resolver is disposed.
/// </para>
/// <para>
/// Common scenarios:
/// </para>
/// <list type="bullet">
/// <item><description>Shared contracts between services</description></item>
/// <item><description>Base types from common libraries</description></item>
/// <item><description>Integration with existing domain models</description></item>
/// </list>
/// </remarks>
/// <example>
/// <para>Basic usage with assembly paths:</para>
/// <code>
/// using var resolver = new ExternalTypeResolver();
/// resolver.LoadAssemblies(new[] { 
///     "SharedContracts.dll",
///     "DomainModels.dll"
/// });
/// 
/// // Check if a type exists externally
/// if (resolver.TypeExists("MyService.Contracts.OrderCreatedEvent"))
/// {
///     Console.WriteLine("Type already exists in referenced assembly");
/// }
/// </code>
/// <para>Usage with pre-loaded assemblies:</para>
/// <code>
/// var assemblies = AppDomain.CurrentDomain.GetAssemblies()
///     .Where(a => a.FullName?.StartsWith("MyCompany") == true);
/// 
/// using var resolver = new ExternalTypeResolver(assemblies);
/// var typeInfo = resolver.GetExternalTypeInfo("MyCompany.Shared.CustomerDto");
/// </code>
/// </example>
public class ExternalTypeResolver : IDisposable
{
    private readonly Dictionary<string, Type> _typeCache = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Assembly> _loadedAssemblies = new(StringComparer.OrdinalIgnoreCase);
    private readonly AssemblyLoadContext _alc = new("ConcordIO-ExternalTypeResolver", isCollectible: true);
    private bool _disposed;

    /// <summary>
    /// Creates a new instance of the external type resolver.
    /// </summary>
    public ExternalTypeResolver()
    {
    }

    /// <summary>
    /// Creates a new instance of the external type resolver with pre-loaded assemblies.
    /// </summary>
    /// <param name="assemblies">Assemblies to scan for existing types.</param>
    public ExternalTypeResolver(IEnumerable<Assembly> assemblies)
    {
        foreach (var assembly in assemblies)
        {
            LoadAssembly(assembly);
        }
    }

    /// <summary>
    /// Loads assemblies from file paths and indexes their exported types.
    /// </summary>
    /// <param name="assemblyPaths">Paths to assembly files (.dll) to load and scan.</param>
    /// <remarks>
    /// <para>
    /// This method safely handles common loading issues:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Native DLLs are silently skipped (<see cref="BadImageFormatException"/>)</description></item>
    /// <item><description>Version mismatches are silently skipped (<see cref="FileLoadException"/>)</description></item>
    /// <item><description>Missing files are silently skipped</description></item>
    /// <item><description>Already loaded assemblies are not reloaded</description></item>
    /// </list>
    /// <para>
    /// Unexpected errors are wrapped in <see cref="InvalidOperationException"/> and rethrown.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown when an unexpected error occurs during assembly loading.</exception>
    /// <example>
    /// <code>
    /// var resolver = new ExternalTypeResolver();
    /// 
    /// // Load from specific paths
    /// resolver.LoadAssemblies(new[] { 
    ///     @"C:\libs\SharedContracts.dll",
    ///     @"C:\libs\DomainModels.dll"
    /// });
    /// 
    /// // Or load all DLLs from a directory
    /// resolver.LoadAssemblies(Directory.GetFiles("libs", "*.dll"));
    /// </code>
    /// </example>
    public void LoadAssemblies(IEnumerable<string> assemblyPaths)
    {
        foreach (var path in assemblyPaths)
        {
            try
            {
                if (File.Exists(path) && !_loadedAssemblies.ContainsKey(path))
                {
                    var assembly = _alc.LoadFromAssemblyPath(path);
                    LoadAssembly(assembly);
                    _loadedAssemblies[path] = assembly;
                }
            }
            catch (BadImageFormatException)
            {
                // Expected for native DLLs - silently ignore
            }
            catch (FileLoadException)
            {
                // Expected for version mismatches or locked files - silently ignore
            }
            catch (Exception ex)
            {
                // Unexpected exception - this might indicate a real problem
                // but we don't have access to Log here, so we re-throw for caller to handle
                throw new InvalidOperationException($"Unexpected error loading assembly {path}: {ex.Message}", ex);
            }
        }
    }

    private void LoadAssembly(Assembly assembly)
    {
        try
        {
            foreach (var type in assembly.GetExportedTypes())
            {
                if (type.FullName is not null && !_typeCache.ContainsKey(type.FullName))
                {
                    _typeCache[type.FullName] = type;
                }
            }
        }
        catch (ReflectionTypeLoadException ex)
        {
            // Partial type load - process the types that did load successfully
            foreach (var type in ex.Types)
            {
                if (type?.FullName is not null && !_typeCache.ContainsKey(type.FullName))
                {
                    _typeCache[type.FullName] = type;
                }
            }
        }
        catch (FileNotFoundException)
        {
            // Expected when assembly has missing dependencies - silently ignore
        }
        catch (Exception)
        {
            // Other reflection errors (e.g., security exceptions) - silently ignore
            // The assembly simply won't contribute types to the resolver
        }
    }

    /// <summary>
    /// Checks if a type with the given full name exists in any loaded assembly.
    /// </summary>
    /// <param name="fullTypeName">The fully qualified type name (e.g., "MyService.Contracts.Events.OrderCreatedEvent").</param>
    /// <returns><c>true</c> if the type exists in a loaded assembly; otherwise, <c>false</c>.</returns>
    /// <example>
    /// <code>
    /// if (resolver.TypeExists("MyService.Contracts.CustomerDto"))
    /// {
    ///     // Skip generation, type already exists
    /// }
    /// </code>
    /// </example>
    public bool TypeExists(string fullTypeName)
    {
        return _typeCache.ContainsKey(fullTypeName);
    }

    /// <summary>
    /// Gets the <see cref="Type"/> with the given full name if it exists.
    /// </summary>
    /// <param name="fullTypeName">The fully qualified type name (e.g., "MyService.Contracts.Events.OrderCreatedEvent").</param>
    /// <returns>The <see cref="Type"/> if found in a loaded assembly; otherwise, <c>null</c>.</returns>
    /// <example>
    /// <code>
    /// var type = resolver.GetType("MyService.Contracts.CustomerDto");
    /// if (type != null)
    /// {
    ///     Console.WriteLine($"Found: {type.FullName} in {type.Assembly.GetName().Name}");
    /// }
    /// </code>
    /// </example>
    public Type? GetType(string fullTypeName)
    {
        return _typeCache.TryGetValue(fullTypeName, out var type) ? type : null;
    }

    /// <summary>
    /// Gets information about an external type if it exists in a loaded assembly.
    /// </summary>
    /// <param name="fullTypeName">The fully qualified type name (e.g., "MyService.Contracts.Events.OrderCreatedEvent").</param>
    /// <returns>
    /// A <see cref="TypeInfo"/> with <see cref="TypeInfo.IsExternal"/> set to <c>true</c> if the type
    /// was found in a loaded assembly; otherwise, <c>null</c>.
    /// </returns>
    /// <remarks>
    /// The returned <see cref="TypeInfo"/> includes:
    /// <list type="bullet">
    /// <item><description><see cref="TypeInfo.TypeName"/> - The simple type name</description></item>
    /// <item><description><see cref="TypeInfo.Namespace"/> - The type's namespace</description></item>
    /// <item><description><see cref="TypeInfo.ExternalAssembly"/> - The assembly containing the type</description></item>
    /// </list>
    /// </remarks>
    /// <example>
    /// <code>
    /// var typeInfo = resolver.GetExternalTypeInfo("MyService.Contracts.CustomerDto");
    /// if (typeInfo != null)
    /// {
    ///     Console.WriteLine($"External type {typeInfo.TypeName}");
    ///     Console.WriteLine($"  Namespace: {typeInfo.Namespace}");
    ///     Console.WriteLine($"  Assembly: {typeInfo.ExternalAssembly}");
    /// }
    /// </code>
    /// </example>
    public TypeInfo? GetExternalTypeInfo(string fullTypeName)
    {
        if (_typeCache.TryGetValue(fullTypeName, out var type))
        {
            var typeName = type.Name;
            var ns = type.Namespace ?? string.Empty;
            var assemblyName = type.Assembly.GetName().Name;

            return new TypeInfo(typeName, ns, IsExternal: true, ExternalAssembly: assemblyName);
        }

        return null;
    }

    /// <summary>
    /// Gets all fully qualified type names from loaded assemblies.
    /// </summary>
    /// <returns>An enumerable of fully qualified type names.</returns>
    /// <example>
    /// <code>
    /// foreach (var typeName in resolver.GetLoadedTypeNames())
    /// {
    ///     Console.WriteLine(typeName);
    /// }
    /// </code>
    /// </example>
    public IEnumerable<string> GetLoadedTypeNames() => _typeCache.Keys;

    /// <summary>
    /// Disposes the resolver and unloads the <see cref="System.Runtime.Loader.AssemblyLoadContext"/>.
    /// </summary>
    /// <remarks>
    /// After disposal, the assemblies loaded by this resolver become eligible for garbage collection.
    /// Any types resolved before disposal remain valid but should not be used for further reflection.
    /// </remarks>
    public void Dispose()
    {
        if (!_disposed)
        {
            _alc.Unload();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
