// Test types for validating bug fixes (#22-#28)
// Note: Product types are in separate files to test namespace collision handling

namespace ConcordIO.AsyncApi.Tests.TestTypes.BugFixes;

using ConcordIO.AsyncApi.Tests.TestTypes.Namespace1;
using Namespace2Product = ConcordIO.AsyncApi.Tests.TestTypes.Namespace2.Product;


// Issue #25: Missing simple types - modern .NET types
public class ModernTypesEvent
{
    public DateOnly EventDate { get; set; }
    public TimeOnly EventTime { get; set; }
    public byte[] BinaryData { get; set; } = Array.Empty<byte>();
    public Half SmallNumber { get; set; }
    public Int128 LargeNumber { get; set; }
}

// Issue #28: Dictionary key type ignored
public class CustomKey
{
    public string Tenant { get; set; } = string.Empty;
    public Guid ResourceId { get; set; }
}

public class CustomValue
{
    public string Data { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

public class DictionaryEvent
{
    public Dictionary<CustomKey, CustomValue> Metadata { get; set; } = new();
    public Dictionary<string, CustomValue> SimpleKeyDictionary { get; set; } = new();
}

// Collision test event - uses Product from both namespaces
public class OrderEvent
{
    public Product Product1 { get; set; } = null!;
    public Namespace2Product Product2 { get; set; } = null!;
}

// Fix (PR review): Schema reference vs. key encoding mismatch
// A message type that is itself a nested class has '+' in its FullName.
// e.g., "ConcordIO.AsyncApi.Tests.TestTypes.BugFixes.NestedMessageContainer+NestedEvent"
// Before the fix, the payload $ref used Uri.EscapeDataString → '+' became '%2B',
// creating a mismatch with the schema dictionary key which used the raw FullName.
public class NestedMessageContainer
{
    public class NestedEvent
    {
        public string Data { get; set; } = string.Empty;
        public int Count { get; set; }
    }
}

// Issue #49: Support all generic types with arbitrary number of type parameters
public class GenericTypesEvent
{
    // KeyValuePair<K,V>
    public KeyValuePair<CustomKey, CustomValue> SinglePair { get; set; }
    
    // Tuple types
    public Tuple<CustomKey, CustomValue> TupleOfCustomTypes { get; set; } = null!;
    public ValueTuple<CustomKey, CustomValue> ValueTupleOfCustomTypes { get; set; }
    
    // Tuple with 3+ parameters
    public Tuple<CustomKey, CustomValue, string> TripleTuple { get; set; } = null!;
    
    // Custom multi-parameter generic (simulated with common System type)
    public Dictionary<CustomKey, CustomValue> CustomGeneric { get; set; } = new();
}
