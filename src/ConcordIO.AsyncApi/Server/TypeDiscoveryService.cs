using System.Reflection;

namespace ConcordIO.AsyncApi.Server;

/// <summary>
/// Discovers types from assemblies based on pattern matching.
/// Supports wildcards, interfaces, and base classes.
/// </summary>
public class TypeDiscoveryService
{
	/// <summary>
	/// Discovers types from assemblies based on the provided patterns.
	/// </summary>
	/// <param name="assemblies">The assemblies to search (typically the primary assembly and its referenced assemblies).</param>
	/// <param name="patterns">
	/// Patterns to match:
	/// - "Namespace.*" - all public non-abstract types in exact namespace
	/// - "Namespace.**" - all public non-abstract types in namespace and sub-namespaces
	/// - "IMyInterface" - all implementations of the interface
	/// - "MyBaseClass" - all subclasses of the base class
	/// - "MyConcreteType" - the specific type
	/// </param>
	/// <returns>Discovered types with their message kind.</returns>
	/// <remarks>
	/// This method searches across multiple assemblies to support scenarios where message types
	/// are defined in referenced class libraries rather than the primary assembly.
	/// Types are deduplicated by their full name to prevent duplicate matches.
	/// 
	/// The loop order (patterns outer, assemblies inner) is intentional: patterns are typically
	/// few (2-5), while assemblies can be many (10-20+). This order minimizes redundant work
	/// by processing each pattern against all assemblies in a single pass, rather than enumerating
	/// types from each assembly multiple times.
	/// </remarks>
	public IEnumerable<DiscoveredType> DiscoverTypes(
		IEnumerable<Assembly> assemblies,
		IEnumerable<MessageTypePattern> patterns)
	{
		ArgumentNullException.ThrowIfNull(assemblies);
		ArgumentNullException.ThrowIfNull(patterns);

		var discovered = new Dictionary<Type, MessageKind>();

		foreach (var pattern in patterns)
		{
			foreach (var assembly in assemblies)
			{
				foreach (var type in DiscoverTypesForPattern(assembly, pattern.Pattern))
				{
					// If type already discovered, keep the existing kind (first wins)
					discovered.TryAdd(type, pattern.Kind);
				}
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
			return GetLoadableTypes(assembly)
				.Where(t => t.IsPublic && !t.IsAbstract && !t.IsInterface &&
					   (t.Namespace == ns || t.Namespace?.StartsWith(ns + ".") == true));
		}

		if (pattern.EndsWith(".*"))
		{
			// Exact namespace wildcard
			var ns = pattern[..^2];
			return GetLoadableTypes(assembly)
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
			return GetLoadableTypes(assembly)
				.Where(t => t.IsPublic && !t.IsInterface && !t.IsAbstract &&
					   type.IsAssignableFrom(t));
		}

		if (type.IsAbstract || HasSubclasses(assembly, type))
		{
			// Find all subclasses
			return GetLoadableTypes(assembly)
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
		return GetLoadableTypes(assembly)
			.FirstOrDefault(t => t.FullName == typeName || t.Name == typeName);
	}

	private static bool HasSubclasses(Assembly assembly, Type type)
	{
		return GetLoadableTypes(assembly).Any(t => t.IsSubclassOf(type));
	}

	/// <summary>
	/// Safely loads all types from an assembly, handling cases where some types
	/// cannot be loaded due to missing referenced assemblies (e.g., shared framework types).
	/// </summary>
	/// <param name="assembly">The assembly to load types from.</param>
	/// <returns>All successfully loaded types from the assembly.</returns>
	/// <remarks>
	/// Some types may fail to load because they reference assemblies not present in the
	/// output directory (e.g., framework assemblies like System.Threading.RateLimiting
	/// that are provided by the shared framework at runtime but not copied to bin/).
	/// When this occurs, we return only the types that loaded successfully.
	/// </remarks>
	private static Type[] GetLoadableTypes(Assembly assembly)
	{
		try
		{
			return assembly.GetTypes();
		}
		catch (ReflectionTypeLoadException ex)
		{
			// Return only the types that loaded successfully
			return ex.Types.Where(t => t is not null).ToArray()!;
		}
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
