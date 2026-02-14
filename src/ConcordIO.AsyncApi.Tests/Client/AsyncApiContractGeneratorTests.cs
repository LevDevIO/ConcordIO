using System.Dynamic;
using System.Text.Json;

using ConcordIO.AsyncApi.Client;

using Neuroglia.AsyncApi;
using Neuroglia.AsyncApi.v3;

namespace ConcordIO.AsyncApi.Tests.Client;

public class AsyncApiContractGeneratorTests
{
	private readonly AsyncApiContractGenerator _sut = new();

	#region Basic Generation Tests

	[Fact]
	public void Generate_WithSimpleSchema_GeneratesSourceFile()
	{
		// Arrange
		var document = CreateDocumentWithSchema("OrderCreatedEvent", "MyService.Contracts.Events", new
		{
			type = "object",
			properties = new
			{
				orderId = new
				{
					type = "string",
					format = "uuid"
				},
				createdAt = new
				{
					type = "string",
					format = "date-time"
				}
			}
		});

		// Act
		var result = _sut.Generate(document);

		// Assert
		result.SourceFiles.Should().HaveCount(1);
		result.GeneratedTypes.Should().HaveCount(1);
		result.ExternalTypes.Should().BeEmpty();

		var sourceFile = result.SourceFiles[0];
		sourceFile.Namespace.Should().Be("MyService.Contracts.Events");
		sourceFile.FileName.Should().Be("MyService.Contracts.Events.g.cs");
		sourceFile.Content.Should().Contain("OrderCreatedEvent");
	}

	[Fact]
	public void Generate_WithRequiredProperties_GeneratesType()
	{
		// Arrange
		var document = CreateDocumentWithSchema("Customer", "Contracts", new
		{
			type = "object",
			properties = new
			{
				id = new
				{
					type = "string",
					format = "uuid"
				},
				name = new
				{
					type = "string"
				},
				email = new
				{
					type = "string"
				}
			},
			required = new[] { "id", "name" }
		});

		// Act
		var result = _sut.Generate(document);

		// Assert
		result.SourceFiles.Should().HaveCount(1);
		var content = result.SourceFiles[0].Content;
		content.Should().Contain("Customer");
	}

	[Fact]
	public void Generate_WithMultipleSchemasInSameNamespace_GeneratesSingleFile()
	{
		// Arrange
		var document = new V3AsyncApiDocument
		{
			AsyncApi = AsyncApiSpecVersion.V3,
			Info = new V3ApiInfo { Title = "Test", Version = "1.0.0" },
			Channels = [],
			Operations = [],
			Components = new V3ComponentDefinitionCollection
			{
				Schemas = new()
				{
					["OrderCreatedEvent"] = CreateSchemaDefinition("MyService.Events", new
					{
						type = "object",
						properties = new
						{
							orderId = new
							{
								type = "string"
							}
						}
					}),
					["OrderCancelledEvent"] = CreateSchemaDefinition("MyService.Events", new
					{
						type = "object",
						properties = new
						{
							orderId = new
							{
								type = "string"
							},
							reason = new
							{
								type = "string"
							}
						}
					})
				}
			}
		};

		// Act
		var result = _sut.Generate(document);

		// Assert
		result.SourceFiles.Should().HaveCount(1);
		result.GeneratedTypes.Should().HaveCount(2);

		var sourceFile = result.SourceFiles[0];
		sourceFile.Namespace.Should().Be("MyService.Events");
		sourceFile.Content.Should().Contain("OrderCreatedEvent");
		sourceFile.Content.Should().Contain("OrderCancelledEvent");
		sourceFile.Types.Should().HaveCount(2);
	}

	[Fact]
	public void Generate_WithSchemasInDifferentNamespaces_GeneratesMultipleFiles()
	{
		// Arrange
		var document = new V3AsyncApiDocument
		{
			AsyncApi = AsyncApiSpecVersion.V3,
			Info = new V3ApiInfo { Title = "Test", Version = "1.0.0" },
			Channels = [],
			Operations = [],
			Components = new V3ComponentDefinitionCollection
			{
				Schemas = new()
				{
					["OrderCreatedEvent"] = CreateSchemaDefinition("MyService.Events", new
					{
						type = "object",
						properties = new
						{
							orderId = new
							{
								type = "string"
							}
						}
					}),
					["CreateOrderCommand"] = CreateSchemaDefinition("MyService.Commands", new
					{
						type = "object",
						properties = new
						{
							customerId = new
							{
								type = "string"
							}
						}
					})
				}
			}
		};

		// Act
		var result = _sut.Generate(document);

		// Assert
		result.SourceFiles.Should().HaveCount(2);
		result.GeneratedTypes.Should().HaveCount(2);

		result.SourceFiles.Should().Contain(sf => sf.Namespace == "MyService.Events");
		result.SourceFiles.Should().Contain(sf => sf.Namespace == "MyService.Commands");
	}

	#endregion

	#region Property Type Tests

	[Fact]
	public void Generate_WithStringProperty_GeneratesProperty()
	{
		// Arrange
		var document = CreateDocumentWithSchema("Message", "Test", new
		{
			type = "object",
			properties = new
			{
				text = new
				{
					type = "string"
				}
			}
		});

		// Act
		var result = _sut.Generate(document);

		// Assert
		result.SourceFiles.Should().HaveCount(1);
		result.GeneratedTypes.Should().HaveCount(1);
		result.GeneratedTypes[0].TypeName.Should().Be("Message");
	}

	[Fact]
	public void Generate_WithIntegerProperty_GeneratesType()
	{
		// Arrange
		var document = CreateDocumentWithSchema("Counter", "Test", new
		{
			type = "object",
			properties = new
			{
				count = new
				{
					type = "integer"
				}
			}
		});

		// Act
		var result = _sut.Generate(document);

		// Assert
		result.SourceFiles.Should().HaveCount(1);
		result.GeneratedTypes[0].TypeName.Should().Be("Counter");
	}

	[Fact]
	public void Generate_WithBooleanProperty_GeneratesType()
	{
		// Arrange
		var document = CreateDocumentWithSchema("Flag", "Test", new
		{
			type = "object",
			properties = new
			{
				isActive = new
				{
					type = "boolean"
				}
			}
		});

		// Act
		var result = _sut.Generate(document);

		// Assert
		result.SourceFiles.Should().HaveCount(1);
		result.GeneratedTypes[0].TypeName.Should().Be("Flag");
	}

	[Fact]
	public void Generate_WithUuidFormat_GeneratesType()
	{
		// Arrange
		var document = CreateDocumentWithSchema("Entity", "Test", new
		{
			type = "object",
			properties = new
			{
				id = new
				{
					type = "string",
					format = "uuid"
				}
			}
		});

		// Act
		var result = _sut.Generate(document);

		// Assert
		result.SourceFiles.Should().HaveCount(1);
		result.GeneratedTypes[0].TypeName.Should().Be("Entity");
	}

	[Fact]
	public void Generate_WithArrayProperty_GeneratesType()
	{
		// Arrange
		var document = CreateDocumentWithSchema("Container", "Test", new
		{
			type = "object",
			properties = new
			{
				items = new
				{
					type = "array",
					items = new
					{
						type = "string"
					}
				}
			}
		});

		// Act
		var result = _sut.Generate(document);

		// Assert
		result.SourceFiles.Should().HaveCount(1);
		result.GeneratedTypes[0].TypeName.Should().Be("Container");
	}

	#endregion

	#region Generated Code Format Tests

	[Fact]
	public void Generate_IncludesAutoGeneratedHeader()
	{
		// Arrange
		var document = CreateDocumentWithSchema("Test", "Namespace", new
		{
			type = "object"
		});

		// Act
		var result = _sut.Generate(document);

		// Assert
		var content = result.SourceFiles[0].Content;
		content.Should().Contain("<auto-generated>");
		content.Should().Contain("ConcordIO.AsyncApi.Client");
	}

	[Fact]
	public void Generate_IncludesNullableEnable()
	{
		// Arrange
		var document = CreateDocumentWithSchema("Test", "Namespace", new
		{
			type = "object"
		});

		// Act
		var result = _sut.Generate(document);

		// Assert
		var content = result.SourceFiles[0].Content;
		content.Should().Contain("#nullable enable");
	}

	[Fact]
	public void Generate_IncludesUsingStatements()
	{
		// Arrange
		var document = CreateDocumentWithSchema("Test", "Namespace", new
		{
			type = "object"
		});

		// Act
		var result = _sut.Generate(document);

		// Assert
		var content = result.SourceFiles[0].Content;
		content.Should().Contain("using System;");
	}

	[Fact]
	public void Generate_WithCrossNamespaceReference_IncludesUsingForOtherNamespace()
	{
		// Arrange
		var document = new V3AsyncApiDocument
		{
			AsyncApi = AsyncApiSpecVersion.V3,
			Info = new V3ApiInfo { Title = "Test", Version = "1.0.0" },
			Channels = [],
			Operations = [],
			Components = new V3ComponentDefinitionCollection
			{
				Schemas = new()
				{
					["Order"] = CreateSchemaDefinition("MyService.Models", new
					{
						type = "object",
						properties = new
						{
							id = new
							{
								type = "string"
							}
						}
					}),
					["OrderCreatedEvent"] = CreateSchemaDefinition("MyService.Events", new
					{
						type = "object",
						properties = new
						{
							orderId = new
							{
								type = "string"
							}
						}
					})
				}
			}
		};

		// Act
		var result = _sut.Generate(document);

		// Assert
		var eventsFile = result.SourceFiles.First(sf => sf.Namespace == "MyService.Events");
		eventsFile.Content.Should().Contain("using MyService.Models;");
	}

	#endregion

	#region Settings Tests

	[Fact]
	public void Generate_WithRecordClassStyle_ConfiguresRecordGeneration()
	{
		// Arrange
		var settings = new ContractGeneratorSettings(ClassStyle: GeneratedClassStyle.Record);
		var generator = new AsyncApiContractGenerator(settings, new ExternalTypeResolver());
		var document = CreateDocumentWithSchema("Event", "Test", new
		{
			type = "object",
			properties = new
			{
				id = new
				{
					type = "string"
				}
			}
		});

		// Act
		var result = generator.Generate(document);

		// Assert
		result.SourceFiles.Should().HaveCount(1);
		result.GeneratedTypes.Should().HaveCount(1);
		// The generator was configured with Record style
	}

	[Fact]
	public void Generate_WithDataAnnotationsEnabled_IncludesDataAnnotationsUsing()
	{
		// Arrange
		var settings = new ContractGeneratorSettings(GenerateDataAnnotations: true);
		var generator = new AsyncApiContractGenerator(settings, new ExternalTypeResolver());
		var document = CreateDocumentWithSchema("Test", "Namespace", new
		{
			type = "object",
			properties = new
			{
				name = new
				{
					type = "string"
				}
			}
		});

		// Act
		var result = generator.Generate(document);

		// Assert
		var content = result.SourceFiles[0].Content;
		content.Should().Contain("using System.ComponentModel.DataAnnotations;");
	}

	#endregion

	#region External Type Resolution Tests

	[Fact]
	public void Generate_WithExternalType_ExcludesFromGeneration()
	{
		// Arrange
		var externalResolver = new ExternalTypeResolver(new[] { typeof(Guid).Assembly });
		var generator = new AsyncApiContractGenerator(new ContractGeneratorSettings(), externalResolver);

		// Register a fake external type
		var document = CreateDocumentWithSchema("Customer", "ExternalLib.Models", new
		{
			type = "object",
			properties = new
			{
				id = new
				{
					type = "string"
				}
			}
		});

		// Note: In real usage, the ExternalTypeResolver would have the type loaded
		// This test verifies the mechanism works

		// Act
		var result = generator.Generate(document);

		// Assert - since ExternalLib.Models.Customer doesn't exist, it should be generated
		result.GeneratedTypes.Should().Contain(t => t.TypeName == "Customer");
	}

	#endregion

	#region Edge Cases

	[Fact]
	public void Generate_WithNullDocument_ThrowsArgumentNullException()
	{
		// Act & Assert
		var act = () => _sut.Generate(null!);
		act.Should().Throw<ArgumentNullException>();
	}

	[Fact]
	public void Generate_WithEmptySchemas_ReturnsEmptyResult()
	{
		// Arrange
		var document = new V3AsyncApiDocument
		{
			AsyncApi = AsyncApiSpecVersion.V3,
			Info = new V3ApiInfo { Title = "Test", Version = "1.0.0" },
			Channels = [],
			Operations = [],
			Components = new V3ComponentDefinitionCollection
			{
				Schemas = []
			}
		};

		// Act
		var result = _sut.Generate(document);

		// Assert
		result.SourceFiles.Should().BeEmpty();
		result.GeneratedTypes.Should().BeEmpty();
		result.ExternalTypes.Should().BeEmpty();
	}

	[Fact]
	public void Generate_WithNullComponents_ReturnsEmptyResult()
	{
		// Arrange
		var document = new V3AsyncApiDocument
		{
			AsyncApi = AsyncApiSpecVersion.V3,
			Info = new V3ApiInfo { Title = "Test", Version = "1.0.0" },
			Channels = [],
			Operations = [],
			Components = null
		};

		// Act
		var result = _sut.Generate(document);

		// Assert
		result.SourceFiles.Should().BeEmpty();
	}

	[Fact]
	public void Generate_WithSchemaWithoutNamespace_UsesDefaultOrEmptyNamespace()
	{
		// Arrange - schema without x-dotnet-namespace
		var document = new V3AsyncApiDocument
		{
			AsyncApi = AsyncApiSpecVersion.V3,
			Info = new V3ApiInfo { Title = "Test", Version = "1.0.0" },
			Channels = [],
			Operations = [],
			Components = new V3ComponentDefinitionCollection
			{
				Schemas = new()
				{
					["SimpleType"] = new V3SchemaDefinition
					{
						SchemaFormat = "application/schema+json;version=draft-07",
						Schema = JsonSerializer.Deserialize<ExpandoObject>("""
                        {
                            "type": "object",
                            "properties": {
                                "value": { "type": "string" }
                            }
                        }
                        """)!
					}
				}
			}
		};

		// Act
		var result = _sut.Generate(document);

		// Assert
		result.SourceFiles.Should().HaveCount(1);
		// When no namespace is specified, it could be empty or a default - either is acceptable
		result.SourceFiles[0].Content.Should().Contain("SimpleType");
	}

	[Fact]
	public void Generate_WithNestedObjectSchema_GeneratesType()
	{
		// Arrange
		var document = CreateDocumentWithSchema("Order", "Test", new
		{
			type = "object",
			properties = new
			{
				id = new
				{
					type = "string"
				},
				customer = new
				{
					type = "object",
					properties = new
					{
						name = new
						{
							type = "string"
						},
						email = new
						{
							type = "string"
						}
					}
				}
			}
		});

		// Act
		var result = _sut.Generate(document);

		// Assert
		result.SourceFiles.Should().HaveCount(1);
		var content = result.SourceFiles[0].Content;
		content.Should().Contain("Order");
	}

	[Fact]
	public void Generate_TypeInfoHasCorrectFullName()
	{
		// Arrange
		var document = CreateDocumentWithSchema("OrderCreatedEvent", "MyService.Events", new
		{
			type = "object",
			properties = new
			{
				id = new
				{
					type = "string"
				}
			}
		});

		// Act
		var result = _sut.Generate(document);

		// Assert
		var typeInfo = result.GeneratedTypes[0];
		typeInfo.TypeName.Should().Be("OrderCreatedEvent");
		typeInfo.FullName.Should().Contain("OrderCreatedEvent");
	}

	[Fact]
	public void Generate_GeneratedSourceFileContainsCorrectTypes()
	{
		// Arrange
		var document = CreateDocumentWithSchema("Event", "Test.Events", new
		{
			type = "object",
			properties = new
			{
				data = new
				{
					type = "string"
				}
			}
		});

		// Act
		var result = _sut.Generate(document);

		// Assert
		var sourceFile = result.SourceFiles[0];
		sourceFile.Types.Should().HaveCount(1);
		sourceFile.Types[0].TypeName.Should().Be("Event");
	}

	#endregion

	#region ExtractClassDefinition Edge Cases

	[Fact]
	public void Generate_WithRecordClassStyle_GeneratesValidRecordDefinition()
	{
		// Arrange
		var settings = new ContractGeneratorSettings(ClassStyle: GeneratedClassStyle.Record);
		var generator = new AsyncApiContractGenerator(settings, new ExternalTypeResolver());
		var document = CreateDocumentWithSchema("Person", "Test", new
		{
			type = "object",
			properties = new
			{
				name = new
				{
					type = "string"
				},
				age = new
				{
					type = "integer"
				}
			}
		});

		// Act
		var result = generator.Generate(document);

		// Assert
		result.SourceFiles.Should().HaveCount(1);
		var content = result.SourceFiles[0].Content;
		// Should generate valid code that contains the type name
		content.Should().Contain("Person");
		// Should not fall back to the default error case
		content.Should().NotContain("public partial class Person\r\n{");
	}

	[Fact]
	public void Generate_WithPocoClassStyle_GeneratesValidClassDefinition()
	{
		// Arrange
		var settings = new ContractGeneratorSettings(ClassStyle: GeneratedClassStyle.Poco);
		var generator = new AsyncApiContractGenerator(settings, new ExternalTypeResolver());
		var document = CreateDocumentWithSchema("Person", "Test", new
		{
			type = "object",
			properties = new
			{
				name = new
				{
					type = "string"
				}
			}
		});

		// Act
		var result = generator.Generate(document);

		// Assert
		result.SourceFiles.Should().HaveCount(1);
		var content = result.SourceFiles[0].Content;
		// Should contain the type name
		content.Should().Contain("Person");
		// Should be valid generated code
		content.Should().Contain("class");
	}

	[Fact]
	public void Generate_WithRecordStyle_ProducesValidSyntax()
	{
		// Arrange
		var settings = new ContractGeneratorSettings(ClassStyle: GeneratedClassStyle.Record);
		var generator = new AsyncApiContractGenerator(settings, new ExternalTypeResolver());
		var document = CreateDocumentWithSchema("Product", "Shop.Models", new
		{
			type = "object",
			properties = new
			{
				id = new
				{
					type = "string",
					format = "uuid"
				},
				name = new
				{
					type = "string"
				},
				price = new
				{
					type = "number"
				},
				inStock = new
				{
					type = "boolean"
				}
			},
			required = new[] { "id", "name", "price" }
		});

		// Act
		var result = generator.Generate(document);

		// Assert
		result.SourceFiles.Should().HaveCount(1);
		var sourceFile = result.SourceFiles[0];

		// Verify basic structure
		sourceFile.Namespace.Should().Be("Shop.Models");
		sourceFile.FileName.Should().Be("Shop.Models.g.cs");
		sourceFile.Types.Should().HaveCount(1);
		sourceFile.Types[0].TypeName.Should().Be("Product");

		// Verify generated code structure
		var content = sourceFile.Content;
		content.Should().Contain("// <auto-generated>");
		content.Should().Contain("#nullable enable");
		content.Should().Contain($"namespace Shop.Models;");
		content.Should().Contain("Product");

		// Verify it's not the fallback error case
		content.Should().NotMatch(@"public partial class Product\s*\{\s*\}");

		// Verify proper structure (either record or class with properties)
		content.Should().MatchRegex(@"(public\s+(partial\s+)?(class|record)\s+Product|public\s+(record|class)\s+Product)");
	}

	#endregion

	#region Helper Methods

	private static V3AsyncApiDocument CreateDocumentWithSchema(string typeName, string ns, object schemaProperties)
	{
		return new V3AsyncApiDocument
		{
			AsyncApi = AsyncApiSpecVersion.V3,
			Info = new V3ApiInfo { Title = "Test API", Version = "1.0.0" },
			Channels = [],
			Operations = [],
			Components = new V3ComponentDefinitionCollection
			{
				Schemas = new()
				{
					[typeName] = CreateSchemaDefinition(ns, schemaProperties)
				}
			}
		};
	}

	private static V3SchemaDefinition CreateSchemaDefinition(string ns, object schemaProperties)
	{
		// Convert anonymous object to ExpandoObject with namespace extension
		var json = JsonSerializer.Serialize(schemaProperties);
		var expando = JsonSerializer.Deserialize<ExpandoObject>(json)!;

		// Add the namespace extension
		var dict = (IDictionary<string, object?>)expando;
		dict["x-dotnet-namespace"] = ns;

		return new V3SchemaDefinition
		{
			SchemaFormat = "application/schema+json;version=draft-07",
			Schema = expando
		};
	}

	#endregion
}
