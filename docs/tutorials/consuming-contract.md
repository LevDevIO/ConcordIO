# 🔄 Tutorial: Consuming a Contract Package

Learn how to consume published contract packages and use automatically generated clients in your .NET projects.

**Time Required**: 10 minutes  
**Prerequisites**:
- .NET 10 SDK installed
- Access to a NuGet feed with contract packages
- Basic .NET project knowledge

## What You'll Learn

- How to add contract package references
- How to use generated clients
- How to customize client generation
- How to troubleshoot common issues

## Scenario

We'll consume the Books API contract we published in the [previous tutorial](./publishing-first-contract.md).

## Step 1: Create a Consumer Project

Create a new console application:

```bash
mkdir BookStore.Consumer
cd BookStore.Consumer
dotnet new console -n BookStore.Consumer
cd BookStore.Consumer
```

## Step 2: Add Package Source (If Needed)

If using a custom feed, add it:

```bash
# Local feed
dotnet nuget add source ~/bookstore-nuget-feed --name bookstore-local

# GitHub Packages
dotnet nuget add source https://nuget.pkg.github.com/OWNER/index.json --name github

# Azure Artifacts
dotnet nuget add source https://pkgs.dev.azure.com/ORG/_packaging/FEED/nuget/v3/index.json --name azure
```

Or create `NuGet.config`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
    <add key="bookstore-local" value="~/bookstore-nuget-feed" />
  </packageSources>
</configuration>
```

## Step 3: Add the Client Package

Add the **client package** (not the contract package):

```bash
dotnet add package Contoso.BookStore.Api.Client --version 1.0.0
```

This updates your `.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Contoso.BookStore.Api.Client" Version="1.0.0" />
  </ItemGroup>
</Project>
```

**Important**: Reference the `.Client` package, not the contract package!
- ✅ `Contoso.BookStore.Api.Client` - Generates code
- ❌ `Contoso.BookStore.Api` - Only provides spec files

## Step 4: Build the Project

Build to trigger code generation:

```bash
dotnet build
```

During build, ConcordIO:
1. Downloads the contract package (transitive dependency)
2. Extracts the OpenAPI spec
3. Runs NSwag to generate the client
4. Compiles the generated code

**Expected output**:
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

The generated client is now available!

## Step 5: Use the Generated Client

Update `Program.cs`:

```csharp
using Contoso.BookStore.Api;

// Configure HttpClient
var httpClient = new HttpClient
{
    BaseAddress = new Uri("https://api.bookstore.example.com")
};

// Create client instance
var client = new BooksApiClient(httpClient);

try
{
    // List all books
    Console.WriteLine("Fetching books...");
    var response = await client.ListBooksAsync(limit: 10);
    
    Console.WriteLine($"Found {response.Total} total books");
    foreach (var book in response.Books)
    {
        Console.WriteLine($"- {book.Title} by {book.Author}");
        if (book.Price.HasValue)
        {
            Console.WriteLine($"  Price: ${book.Price:F2}");
        }
    }

    // Get a specific book
    var bookId = response.Books.First().Id;
    Console.WriteLine($"\nFetching book {bookId}...");
    var book = await client.GetBookAsync(bookId);
    Console.WriteLine($"Got: {book.Title}");
    
    // Create a new book
    Console.WriteLine("\nCreating a new book...");
    var newBook = await client.CreateBookAsync(new CreateBookRequest
    {
        Title = "The Pragmatic Programmer",
        Author = "Andrew Hunt and David Thomas",
        Isbn = "978-0135957059",
        PublicationYear = 2019,
        Genre = "non-fiction",
        Price = 49.99m
    });
    Console.WriteLine($"Created book with ID: {newBook.Id}");
    
    // Update the book
    Console.WriteLine($"\nUpdating book {newBook.Id}...");
    var updated = await client.UpdateBookAsync(newBook.Id, new UpdateBookRequest
    {
        Price = 39.99m  // Price reduced!
    });
    Console.WriteLine($"Updated price to: ${updated.Price:F2}");
    
    // Delete the book
    Console.WriteLine($"\nDeleting book {newBook.Id}...");
    await client.DeleteBookAsync(newBook.Id);
    Console.WriteLine("Book deleted successfully");
}
catch (ApiException ex)
{
    Console.WriteLine($"API Error: {ex.StatusCode} - {ex.Message}");
    Console.WriteLine($"Response: {ex.Response}");
}
catch (HttpRequestException ex)
{
    Console.WriteLine($"Network Error: {ex.Message}");
}
```

## Step 6: Run the Application

```bash
dotnet run
```

**Note**: You'll need a running API instance at the base URL for this to work with real data.

## Step 7: Configure HttpClient with Dependency Injection

For production use, register the client in DI. Update your project:

```bash
# Add ASP.NET Core packages for DI
dotnet add package Microsoft.Extensions.DependencyInjection
dotnet add package Microsoft.Extensions.Http
```

Create a service:

`BookService.cs`:

```csharp
using Contoso.BookStore.Api;

public interface IBookService
{
    Task<ICollection<Book>> GetBooksAsync(int limit = 20);
    Task<Book> GetBookByIdAsync(Guid id);
    Task<Book> CreateBookAsync(CreateBookRequest request);
}

public class BookService : IBookService
{
    private readonly BooksApiClient _client;
    private readonly ILogger<BookService> _logger;

    public BookService(BooksApiClient client, ILogger<BookService> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<ICollection<Book>> GetBooksAsync(int limit = 20)
    {
        try
        {
            var response = await _client.ListBooksAsync(limit);
            return response.Books;
        }
        catch (ApiException ex)
        {
            _logger.LogError(ex, "Failed to fetch books");
            throw;
        }
    }

    public async Task<Book> GetBookByIdAsync(Guid id)
    {
        try
        {
            return await _client.GetBookAsync(id);
        }
        catch (ApiException ex) when (ex.StatusCode == 404)
        {
            _logger.LogWarning("Book {BookId} not found", id);
            throw;
        }
    }

    public async Task<Book> CreateBookAsync(CreateBookRequest request)
    {
        return await _client.CreateBookAsync(request);
    }
}
```

Register in DI (`Program.cs` for ASP.NET Core):

```csharp
var builder = WebApplication.CreateBuilder(args);

// Register HttpClient for the API client
builder.Services.AddHttpClient<BooksApiClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["BooksApi:BaseUrl"]!);
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Register the service
builder.Services.AddScoped<IBookService, BookService>();

var app = builder.Build();

// Use in endpoints
app.MapGet("/books", async (IBookService bookService) =>
{
    var books = await bookService.GetBooksAsync();
    return Results.Ok(books);
});

app.MapGet("/books/{id:guid}", async (Guid id, IBookService bookService) =>
{
    try
    {
        var book = await bookService.GetBookByIdAsync(id);
        return Results.Ok(book);
    }
    catch (ApiException ex) when (ex.StatusCode == 404)
    {
        return Results.NotFound();
    }
});

app.Run();
```

## Step 8: Customize Client Generation

You can customize how the client is generated using MSBuild targets.

Add to your `.csproj`:

```xml
<Target Name="CustomizeBooksApiClient" AfterTargets="ConcordIOAddOpenApiReferenceForNSwag">
  <ItemGroup>
    <OpenApiReference Update="@(OpenApiReference)">
      <!-- Change namespace -->
      <Namespace>BookStore.Consumer.ApiClients</Namespace>
      
      <!-- Enable nullable reference types -->
      <NSwagGenerateNullableReferenceTypes>true</NSwagGenerateNullableReferenceTypes>
      
      <!-- Generate client interfaces for easier mocking -->
      <NSwagGenerateClientInterfaces>true</NSwagGenerateClientInterfaces>
      
      <!-- Use HttpClient injection -->
      <NSwagInjectHttpClient>true</NSwagInjectHttpClient>
      
      <!-- Use Newtonsoft.Json instead of System.Text.Json -->
      <NSwagJsonLibrary>NewtonsoftJson</NSwagJsonLibrary>
    </OpenApiReference>
  </ItemGroup>
</Target>
```

Rebuild to apply changes:

```bash
dotnet build
```

Now you can use the interface for testing:

```csharp
// In tests
public class BookServiceTests
{
    [Fact]
    public async Task GetBooksAsync_ReturnsBooks()
    {
        // Arrange
        var mockClient = new Mock<IBooksApiClient>();
        mockClient.Setup(c => c.ListBooksAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListBooksResponse
            {
                Books = new List<Book>
                {
                    new Book { Id = Guid.NewGuid(), Title = "Test Book", Author = "Test Author" }
                },
                Total = 1
            });

        var service = new BookService(mockClient.Object, Mock.Of<ILogger<BookService>>());

        // Act
        var books = await service.GetBooksAsync();

        // Assert
        Assert.Single(books);
    }
}
```

## Step 9: Handle API Versioning

If the API releases a new version, update the package reference:

```bash
# Check for new versions
dotnet list package --outdated

# Update to specific version
dotnet add package Contoso.BookStore.Api.Client --version 1.1.0

# Or update all packages
dotnet add package Contoso.BookStore.Api.Client
```

**For major version changes** (e.g., 1.x → 2.x), review the breaking changes:

1. Check the package release notes
2. Review API migration guide
3. Update your code accordingly
4. Test thoroughly before deploying

## Step 10: Multiple API Contracts

If you consume multiple APIs, add multiple client packages:

```xml
<ItemGroup>
  <PackageReference Include="Contoso.BookStore.Api.Client" Version="1.0.0" />
  <PackageReference Include="Contoso.UserService.Api.Client" Version="2.0.0" />
  <PackageReference Include="Contoso.OrderService.Api.Client" Version="1.5.0" />
</ItemGroup>
```

Each generates its own client with unique namespaces.

## Troubleshooting

### Issue: Generated Client Types Not Found

**Symptom**: `The type or namespace name 'BooksApiClient' could not be found`

**Solutions**:

1. **Verify correct package**:
   ```bash
   # Check what's installed
   dotnet list package
   
   # Should show the .Client package
   # Contoso.BookStore.Api.Client    1.0.0
   ```

2. **Rebuild project**:
   ```bash
   dotnet clean
   dotnet build
   ```

3. **Check for multi-targeting** (known issue):
   ```xml
   <!-- If you have this -->
   <TargetFrameworks>net8.0;net9.0;net10.0</TargetFrameworks>
   
   <!-- Change to single target -->
   <TargetFramework>net10.0</TargetFramework>
   ```

4. **Enable verbose logging**:
   ```bash
   dotnet build -v n
   # Look for NSwag or OpenApiReference messages
   ```

### Issue: Compilation Errors in Generated Code

**Symptom**: Build fails with errors in generated client code

**Solutions**:

1. **Clear package cache**:
   ```bash
   dotnet nuget locals all --clear
   dotnet restore
   dotnet build
   ```

2. **Check package version compatibility**:
   ```bash
   # Update to latest
   dotnet add package Contoso.BookStore.Api.Client
   ```

3. **Check NSwag version conflicts**:
   ```xml
   <!-- Explicitly reference NSwag if needed -->
   <PackageReference Include="NSwag.MSBuild" Version="14.0.7" />
   ```

### Issue: Runtime Errors (404, 401, etc.)

**Symptom**: Client compiles but throws exceptions at runtime

**Solutions**:

1. **Verify base URL**:
   ```csharp
   var httpClient = new HttpClient
   {
       BaseAddress = new Uri("https://api.bookstore.example.com")  // Correct URL?
   };
   ```

2. **Check authentication** (if API requires it):
   ```csharp
   httpClient.DefaultRequestHeaders.Authorization = 
       new AuthenticationHeaderValue("Bearer", token);
   ```

3. **Enable detailed logging**:
   ```csharp
   builder.Services.AddHttpClient<BooksApiClient>()
       .AddLogger()  // Logs all HTTP requests/responses
       .AddHttpMessageHandler<LoggingHandler>();
   ```

### Issue: Slow Build Times

**Symptom**: Builds take significantly longer after adding contract packages

**Solution**: Use code file caching:

```xml
<Target Name="CacheBooksApiClient" AfterTargets="ConcordIOAddOpenApiReferenceForNSwag">
  <ItemGroup>
    <OpenApiReference Update="@(OpenApiReference)">
      <OutputPath>Generated\BooksApiClient.g.cs</OutputPath>
    </OpenApiReference>
  </ItemGroup>
</Target>
```

NSwag will skip regeneration if the spec hasn't changed.

## Best Practices

### 1. Use Dependency Injection

```csharp
// ✅ Good: Use DI
builder.Services.AddHttpClient<BooksApiClient>(client =>
{
    client.BaseAddress = new Uri(configuration["BooksApi:BaseUrl"]);
});

// ❌ Bad: Create instances directly
var client = new BooksApiClient(new HttpClient());
```

### 2. Configure Timeouts and Retry Policies

```csharp
builder.Services.AddHttpClient<BooksApiClient>(client =>
{
    client.BaseAddress = new Uri(configuration["BooksApi:BaseUrl"]);
    client.Timeout = TimeSpan.FromSeconds(30);
})
.AddTransientHttpErrorPolicy(policy =>
    policy.WaitAndRetryAsync(3, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt))))
.AddTransientHttpErrorPolicy(policy =>
    policy.CircuitBreakerAsync(5, TimeSpan.FromSeconds(30)));
```

### 3. Handle Errors Gracefully

```csharp
try
{
    var book = await client.GetBookAsync(id);
    return Results.Ok(book);
}
catch (ApiException ex) when (ex.StatusCode == 404)
{
    return Results.NotFound();
}
catch (ApiException ex) when (ex.StatusCode == 401)
{
    return Results.Unauthorized();
}
catch (HttpRequestException ex)
{
    logger.LogError(ex, "Network error calling Books API");
    return Results.Problem("Service unavailable");
}
```

### 4. Abstract API Clients Behind Services

```csharp
// Don't expose the generated client directly to controllers
// Wrap it in a service interface

public interface IBookService
{
    Task<ICollection<Book>> GetBooksAsync();
}

public class BookService : IBookService
{
    private readonly BooksApiClient _client;

    public BookService(BooksApiClient client)
    {
        _client = client;
    }

    public async Task<ICollection<Book>> GetBooksAsync()
    {
        var response = await _client.ListBooksAsync();
        return response.Books;
    }
}
```

### 5. Pin Package Versions in Production

```xml
<!-- Development: Use floating versions -->
<PackageReference Include="Contoso.BookStore.Api.Client" Version="*" />

<!-- Production: Pin exact versions -->
<PackageReference Include="Contoso.BookStore.Api.Client" Version="1.0.0" />
```

## Next Steps

- [Tutorial: CI/CD Setup](./cicd-setup.md) - Automate contract updates
- [Client Customization Guide](../user-guides/client-customization.md) - Advanced client configuration
- [Troubleshooting Guide](../troubleshooting/common-issues.md) - More solutions
- [Examples](../examples/README.md) - Complete working examples

## Summary

You've learned how to:
- ✅ Add contract client packages to projects
- ✅ Use generated API clients
- ✅ Configure HttpClient with DI
- ✅ Customize client generation
- ✅ Handle errors and edge cases
- ✅ Follow best practices

**Congratulations!** You're now consuming API contracts effectively with ConcordIO.
