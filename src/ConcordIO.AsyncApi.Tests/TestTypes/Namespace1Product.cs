// Issue #24: Schema key collision test - Product type in Namespace1

namespace ConcordIO.AsyncApi.Tests.TestTypes.Namespace1;

public class Product
{
	public string Id { get; set; } = string.Empty;
	public string Name { get; set; } = string.Empty;
}
