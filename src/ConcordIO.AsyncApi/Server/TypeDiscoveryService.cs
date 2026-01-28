using System.Reflection;

namespace ConcordIO.AsyncApi.Server;

/// <summary>
/// Discovers types from assemblies based on pattern matching.
/// Supports wildcards, interfaces, and base classes.
/// </summary>
public class TypeDiscoveryService
{
    /// <summary>
    /// Discovers types from an assembly based on the provided patterns.
    /// </summary>
    /// <param name="assembly">The assembly to search.</param>
    /// <param name="patterns">
    /// Patterns to match:
    /// - "Namespace.*" - all public non-abstract types in exact namespace
    /// - "Namespace.**" - all public non-abstract types in namespace and sub-namespaces
    /// - "IMyInterface" - all implementations of the interface
    /// - "MyBaseClass" - all subclasses of the base class
    /// - "MyConcreteType" - the specific type
    /// </param>
    /// <returns>Discovered types with their message kind.</returns>
    public IEnumerable<DiscoveredType> DiscoverTypes(
        Assembly assembly,
        IEnumerable<MessageTypePattern> patterns)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentNullException.ThrowIfNull(patterns);

        var discovered = new Dictionary<Type, MessageKind>();

        foreach (var pattern in patterns)
        {
            foreach (var type in DiscoverTypesForPattern(assembly, pattern.Pattern))
            {
                // If type already discovered, keep the existing kind (first wins)
                discovered.TryAdd(type, pattern.Kind);
            }
        }

        return discovered.Select(kvp => new DiscoveredType(kvp.Key, kvp.Value));
    }

    private static IEnumerable<Type> DiscoverTypesForPattern(Assembly assembly, string pattern)
    {
        if (pattern.EndsWith(".**"))
        {
            // Recursive wildcard: namespace and all sub-namespaces
            var ns = pattern[..^3];
            return assembly.GetTypes()
                .Where(t => t.IsPublic && !t.IsAbstract && !t.IsInterface &&
                       (t.Namespace == ns || t.Namespace?.StartsWith(ns + ".") == true));
        }

        if (pattern.EndsWith(".*"))
        {
            // Exact namespace wildcard
            var ns = pattern[..^2];
            return assembly.GetTypes()
                .Where(t => t.IsPublic && !t.IsAbstract && !t.IsInterface && t.Namespace == ns);
        }

        // Try to resolve as a specific type
        var type = ResolveType(assembly, pattern);
        if (type is null)
        {
            return [];
        }

        if (type.IsInterface)
        {
            // Find all implementations
            return assembly.GetTypes()
                .Where(t => t.IsPublic && !t.IsInterface && !t.IsAbstract &&
                       type.IsAssignableFrom(t));
        }

        if (type.IsAbstract || HasSubclasses(assembly, type))
        {
            // Find all subclasses
            return assembly.GetTypes()
                .Where(t => t.IsPublic && !t.IsAbstract && t.IsSubclassOf(type));
        }

        // Concrete type - return just this type
        return [type];
    }

    private static Type? ResolveType(Assembly assembly, string typeName)
    {
        // Try exact match first
        var type = assembly.GetType(typeName);
        if (type is not null)
        {
            return type;
        }

        // Try to find by full name match
        return assembly.GetTypes()
            .FirstOrDefault(t => t.FullName == typeName || t.Name == typeName);
    }

    private static bool HasSubclasses(Assembly assembly, Type type)
    {
        return assembly.GetTypes().Any(t => t.IsSubclassOf(type));
    }
}

/// <summary>
/// Represents a discovered message type with its kind.
/// </summary>
/// <param name="Type">The discovered .NET type.</param>
/// <param name="Kind">Whether this is an event or command.</param>
public record DiscoveredType(Type Type, MessageKind Kind);

/// <summary>
/// Represents a pattern for discovering message types.
/// </summary>
/// <param name="Pattern">The type pattern (supports wildcards).</param>
/// <param name="Kind">The message kind for matched types.</param>
public record MessageTypePattern(string Pattern, MessageKind Kind);

/// <summary>
/// Indicates the kind of message for AsyncAPI operation semantics.
/// </summary>
public enum MessageKind
{
    /// <summary>
    /// An event message (publish/subscribe semantics).
    /// </summary>
    Event,

    /// <summary>
    /// A command message (send/receive semantics).
    /// </summary>
    Command
}
