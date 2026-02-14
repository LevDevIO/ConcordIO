// Issue #24: Schema key collision test - Product type in Namespace2

namespace ConcordIO.AsyncApi.Tests.TestTypes.Namespace2;

public class Product
{
	public int ProductCode
	{
		get; set;
	}
	public decimal Price
	{
		get; set;
	}
}
