namespace ConcordIO.AsyncApi.Client;

/// <summary>
/// Settings for the AsyncAPI contract generator.
/// </summary>
/// <param name="GenerateDataAnnotations">Whether to generate System.ComponentModel.DataAnnotations attributes.</param>
/// <param name="GenerateNullableReferenceTypes">Whether to generate nullable reference type annotations.</param>
/// <param name="GenerateRequiredProperties">Whether to generate 'required' keyword for required properties (C# 11+).</param>
/// <param name="ClassStyle">The style of generated classes (Poco, Record, etc.).</param>
/// <param name="DateType">The .NET type to use for date values.</param>
/// <param name="DateTimeType">The .NET type to use for date-time values.</param>
/// <param name="TimeType">The .NET type to use for time values.</param>
/// <param name="TimeSpanType">The .NET type to use for duration values.</param>
/// <param name="ArrayType">The generic type to use for arrays.</param>
/// <param name="DictionaryType">The generic type to use for dictionaries.</param>
public record ContractGeneratorSettings(
	bool GenerateDataAnnotations = true,
	bool GenerateNullableReferenceTypes = true,
	bool GenerateRequiredProperties = false,
	GeneratedClassStyle ClassStyle = GeneratedClassStyle.Poco,
	string DateType = "System.DateOnly",
	string DateTimeType = "System.DateTimeOffset",
	string TimeType = "System.TimeOnly",
	string TimeSpanType = "System.TimeSpan",
	string ArrayType = "System.Collections.Generic.List",
	string DictionaryType = "System.Collections.Generic.Dictionary"
);

/// <summary>
/// Specifies the style of generated classes.
/// </summary>
public enum GeneratedClassStyle
{
	/// <summary>
	/// Generate plain old C# objects with get/set properties.
	/// </summary>
	Poco,

	/// <summary>
	/// Generate record types (immutable by default).
	/// </summary>
	Record
}
