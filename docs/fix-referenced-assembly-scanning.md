# Fix: TypeDiscoveryService should scan referenced assemblies, not just the primary assembly

## Problem

`TypeDiscoveryService.DiscoverTypes()` only searches the single `Assembly` passed to it (the project's `$(TargetPath)`). When a host/executable project references a class library that defines the message types, FQTNs and namespace wildcards fail to match because the types don't exist in the host assembly — they exist in referenced assemblies that are already loaded into the `ConcordIOAssemblyLoadContext`.

**Reproduction scenario:**
- `Cypress.Host.csproj` (executable) references `Cypress.Application.csproj` (class library)
- Message types like `Cypress.Application.RateSync.Dhl.Messages.StartDhlRateSync` are defined in `Cypress.Application.dll`
- ConcordIO.AsyncApi.Server is installed in `Cypress.Host.csproj`
- `$(TargetPath)` resolves to `Cypress.Host.dll`
- `GenerateAsyncApiTask` loads `Cypress.Host.dll` and passes it to `TypeDiscoveryService`
- `assembly.GetType("Cypress.Application.RateSync.Dhl.Messages.StartDhlRateSync")` returns `null`
- `assembly.GetTypes()` only contains types defined in `Cypress.Host.dll`
- Result: **"No message types found matching the specified patterns"**

## Root Cause

In `TypeDiscoveryService.cs`:
- `ResolveType(Assembly assembly, string typeName)` calls `assembly.GetType(typeName)` then falls back to iterating `assembly.GetTypes()` — both only search the primary assembly
- Namespace wildcard patterns (`.*`, `.**`) iterate `assembly.GetTypes()` — same limitation
- Interface/base-class patterns also only search `assembly.GetTypes()` for implementations/subclasses

In `GenerateAsyncApiTask.cs`:
- Only loads the single `$(TargetPath)` assembly and passes it to `DiscoverTypes()`
- The `ConcordIOAssemblyLoadContext` **does** resolve dependencies on demand (it probes the output directory), so referenced assemblies *can* be loaded — they're just never explicitly scanned

## Proposed Fix

Expand the search scope to include the primary assembly's referenced assemblies. The assemblies are already resolvable via the ALC — they just need to be loaded and scanned.

**Option A — Eager: Load all referenced assemblies upfront**

In `GenerateAsyncApiTask.Execute()`, after loading the primary assembly, recursively load its referenced assemblies and pass the full set to `DiscoverTypes()`:

```csharp
var assembly = alc.LoadFromAssemblyPath(AssemblyPath);
var allAssemblies = LoadReferencedAssemblies(alc, assembly);
var discoveredTypes = discoveryService.DiscoverTypes(allAssemblies, patterns);
```

Where `LoadReferencedAssemblies` walks `assembly.GetReferencedAssemblies()`, loads each via `alc.LoadFromAssemblyPath()` (probing the output directory), and recurses. Filter to only project/user assemblies by skipping well-known framework prefixes (`System.`, `Microsoft.`, `MassTransit.`, `Newtonsoft.`, etc.) to avoid scanning the entire dependency graph.

**Option B — Lazy: Resolve types across the ALC's loaded assemblies (preferred)**

Change `ResolveType` to try `Type.GetType()` with assembly-qualified probing, or iterate `alc.Assemblies` after the primary assembly fails. Since the ALC already resolves dependencies on demand when `assembly.GetTypes()` triggers type loading, many referenced assemblies may already be loaded by the time a concrete FQTN pattern is evaluated.

Concretely, change `TypeDiscoveryService` to accept an `IEnumerable<Assembly>` (or the ALC itself) and search across all assemblies:

```csharp
public IEnumerable<DiscoveredType> DiscoverTypes(
    IEnumerable<Assembly> assemblies,
    IEnumerable<MessageTypePattern> patterns)
```

## Changes Required

### 1. `TypeDiscoveryService.cs`

Change signature to accept multiple assemblies:

```csharp
public IEnumerable<DiscoveredType> DiscoverTypes(
    IEnumerable<Assembly> assemblies,
    IEnumerable<MessageTypePattern> patterns)
{
    foreach (var pattern in patterns)
    {
        foreach (var assembly in assemblies)
        {
            foreach (var type in DiscoverTypesForPattern(assembly, pattern.Pattern))
            {
                yield return new DiscoveredType(type, pattern.Kind);
            }
        }
    }
}
```

Update `ResolveType` to search across assemblies:

```csharp
private static Type? ResolveType(IEnumerable<Assembly> assemblies, string typeName)
{
    foreach (var assembly in assemblies)
    {
        var type = assembly.GetType(typeName);
        if (type is not null) return type;
    }
    
    // Fallback: iterate all types across all assemblies
    foreach (var assembly in assemblies)
    {
        var type = assembly.GetTypes()
            .FirstOrDefault(t => t.FullName == typeName || t.Name == typeName);
        if (type is not null) return type;
    }
    
    return null;
}
```

Same for namespace wildcards — iterate across all assemblies' `GetTypes()`.

Same for `HasSubclasses` and interface implementation scanning.

**Deduplication**: Since the same type could theoretically be yielded from multiple assemblies (it won't in practice, but defensively), deduplicate by `Type.FullName` in the caller or use a `HashSet<Type>`.

### 2. `GenerateAsyncApiTask.cs`

After loading the primary assembly, load referenced assemblies and pass them all:

```csharp
var assembly = alc.LoadFromAssemblyPath(AssemblyPath);
var assemblies = GetSearchableAssemblies(alc, assembly, assemblyDir);
var discoveredTypes = discoveryService.DiscoverTypes(assemblies, patterns);
```

Where `GetSearchableAssemblies`:

```csharp
private static List<Assembly> GetSearchableAssemblies(
    AssemblyLoadContext alc, Assembly primary, string probeDir)
{
    var result = new List<Assembly> { primary };
    
    foreach (var refName in primary.GetReferencedAssemblies())
    {
        // Skip framework/runtime assemblies — they won't contain user message types
        if (IsFrameworkAssembly(refName.Name)) continue;
        
        var path = Path.Combine(probeDir, refName.Name + ".dll");
        if (File.Exists(path))
        {
            try
            {
                result.Add(alc.LoadFromAssemblyPath(path));
            }
            catch { /* skip unloadable assemblies */ }
        }
    }
    
    return result;
}

private static bool IsFrameworkAssembly(string? name) =>
    name is null ||
    name.StartsWith("System", StringComparison.Ordinal) ||
    name.StartsWith("Microsoft", StringComparison.Ordinal) ||
    name.StartsWith("netstandard", StringComparison.Ordinal) ||
    name.StartsWith("mscorlib", StringComparison.Ordinal);
```

**Note**: Only need to go one level deep on `GetReferencedAssemblies()` from the primary assembly — this covers the common case (host → class library with message types). Recursive scanning is overkill and risks loading the entire dependency graph.

### 3. Logging improvements

Add a diagnostic message listing which assemblies are being scanned, so users can troubleshoot:

```csharp
Log.LogMessage(MessageImportance.Normal, 
    $"ConcordIO: Scanning {assemblies.Count} assemblies for message types: " +
    $"{string.Join(", ", assemblies.Select(a => a.GetName().Name))}");
```

## Test Cases

1. **FQTN in primary assembly** — should still work (no regression)
2. **FQTN in referenced assembly** — the fix: `Cypress.Application.RateSync.Dhl.Messages.StartDhlRateSync` found in `Cypress.Application.dll`
3. **Namespace wildcard in referenced assembly** — `Cypress.Application.RateSync.Dhl.Messages.*` matches types in `Cypress.Application.dll`
4. **Recursive namespace wildcard in referenced assembly** — `Cypress.Application.RateSync.**` matches types in sub-namespaces
5. **Interface in referenced assembly** — interface defined in one assembly, implementations in another
6. **No duplicates** — type found in one assembly isn't yielded twice
7. **Framework assemblies skipped** — `System.Object` isn't scanned

## Backward Compatibility

This is a non-breaking change. Projects where all message types are in the primary assembly will behave identically — the primary assembly is searched first, and referenced assemblies are only probed additionally.
