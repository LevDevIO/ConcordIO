# 💡 ConcordIO Examples

Complete working examples demonstrating ConcordIO usage patterns.

## Quick Reference

| Example | Spec Type | Use Case | Complexity |
|---------|-----------|----------|------------|
| [REST API with OpenAPI](#rest-api-with-openapi) | OpenAPI | HTTP REST clients | ⭐ Beginner |
| [Messaging with AsyncAPI](#messaging-with-asyncapi) | AsyncAPI | Event-driven architecture | ⭐⭐ Intermediate |
| [gRPC Service](#grpc-service-with-protocol-buffers) | Protocol Buffers | gRPC communication | ⭐⭐ Intermediate |
| [Multi-Protocol Service](#multi-protocol-service) | Mixed | Complex microservice | ⭐⭐⭐ Advanced |
| [CI/CD Integration](#cicd-integration) | Any | Automated pipelines | ⭐⭐ Intermediate |
| [Multi-Team Workflow](#multi-team-workflow) | Any | Enterprise setup | ⭐⭐⭐ Advanced |

## REST API with OpenAPI

### Scenario

A REST API team wants to:
1. Package their OpenAPI spec
2. Generate strongly-typed clients for consumers
3. Detect breaking changes before releases

### Project Structure

```
PetStoreApi/
├── src/
│   ├── PetStore.Api/              # API implementation
│   │   ├── Controllers/
│   │   └── PetStore.Api.csproj
│   ├── PetStore.Contracts/        # Spec packaging
│   │   ├── specs/
│   │   │   └── petstore.yaml
│   │   └── PetStore.Contracts.csproj
│   └── PetStore.Client.Example/   # Consumer example
│       └── PetStore.Client.Example.csproj
└── .github/
    └── workflows/
        └── publish-contracts.yml
```

### Step 1: Define OpenAPI Spec

`src/PetStore.Contracts/specs/petstore.yaml`:

```yaml
openapi: 3.0.0
info:
  title: PetStore API
  version: 1.0.0
  description: A simple pet store API

servers:
  - url: https://api.petstore.example.com

paths:
  /pets:
    get:
      operationId: getPets
      summary: List all pets
      responses:
        '200':
          description: A list of pets
          content:
            application/json:
              schema:
                type: array
                items:
                  $ref: '#/components/schemas/Pet'

  /pets/{petId}:
    get:
      operationId: getPetById
      summary: Get a pet by ID
      parameters:
        - name: petId
          in: path
          required: true
          schema:
            type: string
            format: uuid
      responses:
        '200':
          description: The requested pet
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/Pet'
        '404':
          description: Pet not found

    post:
      operationId: createPet
      summary: Create a new pet
      requestBody:
        required: true
        content:
          application/json:
            schema:
              $ref: '#/components/schemas/CreatePetRequest'
      responses:
        '201':
          description: Pet created
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/Pet'

components:
  schemas:
    Pet:
      type: object
      required:
        - id
        - name
      properties:
        id:
          type: string
          format: uuid
        name:
          type: string
        species:
          type: string
          enum: [dog, cat, bird, fish]
        age:
          type: integer
          minimum: 0

    CreatePetRequest:
      type: object
      required:
        - name
      properties:
        name:
          type: string
          minLength: 1
          maxLength: 100
        species:
          type: string
          enum: [dog, cat, bird, fish]
        age:
          type: integer
          minimum: 0
```

### Step 2: Install ConcordIO Tool

```bash
cd src/PetStore.Contracts
dotnet new tool-manifest
dotnet tool install ConcordIO.Tool
```

### Step 3: Generate and Pack Contracts

```bash
dotnet concordio pack \
  --spec specs/petstore.yaml \
  --package-id Contoso.PetStore.Contracts \
  --version 1.0.0 \
  --authors "PetStore Team" \
  --description "PetStore API contracts"
```

Output:
```
✓ Contoso.PetStore.Contracts.1.0.0.nupkg
✓ Contoso.PetStore.Contracts.Client.1.0.0.nupkg
```

### Step 4: Publish to NuGet Feed

```bash
dotnet nuget push *.nupkg --source https://your-feed.com/nuget --api-key $API_KEY
```

### Step 5: Consume in Client Project

`src/PetStore.Client.Example/PetStore.Client.Example.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Contoso.PetStore.Contracts.Client" Version="1.0.0" />
  </ItemGroup>
</Project>
```

### Step 6: Use Generated Client

`src/PetStore.Client.Example/Program.cs`:

```csharp
using Contoso.PetStore.Contracts;

var httpClient = new HttpClient
{
    BaseAddress = new Uri("https://api.petstore.example.com")
};

var client = new PetStoreClient(httpClient);

// List all pets
var pets = await client.GetPetsAsync();
foreach (var pet in pets)
{
    Console.WriteLine($"{pet.Name} ({pet.Species}, {pet.Age} years old)");
}

// Get specific pet
var petId = Guid.NewGuid();
try
{
    var pet = await client.GetPetByIdAsync(petId);
    Console.WriteLine($"Found: {pet.Name}");
}
catch (ApiException ex) when (ex.StatusCode == 404)
{
    Console.WriteLine("Pet not found");
}

// Create new pet
var newPet = await client.CreatePetAsync(new CreatePetRequest
{
    Name = "Fluffy",
    Species = "cat",
    Age = 3
});
Console.WriteLine($"Created pet with ID: {newPet.Id}");
```

### Step 7: Detect Breaking Changes

Before releasing version 2.0.0:

```bash
# Check for breaking changes
dotnet concordio breaking \
  --spec specs/petstore-v2.yaml \
  --package-id Contoso.PetStore.Contracts \
  --version 1.0.0

# Exit code 0 = no breaking changes
# Exit code 1 = breaking changes detected
```

### CI/CD Integration

`.github/workflows/publish-contracts.yml`:

```yaml
name: Publish Contracts

on:
  push:
    branches: [main]
    paths:
      - 'src/PetStore.Contracts/specs/**'

jobs:
  publish:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
        with:
          fetch-depth: 0  # For version calculation

      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '10.0.x'

      - name: Install ConcordIO
        run: dotnet tool install --global ConcordIO.Tool

      - name: Check for breaking changes
        id: breaking
        run: |
          cd src/PetStore.Contracts
          concordio breaking \
            --spec specs/petstore.yaml \
            --package-id Contoso.PetStore.Contracts \
            || echo "BREAKING=true" >> $GITHUB_OUTPUT

      - name: Determine version
        id: version
        run: |
          # Use GitVersion or similar tool
          if [ "${{ steps.breaking.outputs.BREAKING }}" == "true" ]; then
            echo "VERSION=2.0.0" >> $GITHUB_OUTPUT
          else
            echo "VERSION=1.1.0" >> $GITHUB_OUTPUT
          fi

      - name: Pack contracts
        run: |
          cd src/PetStore.Contracts
          concordio pack \
            --spec specs/petstore.yaml \
            --package-id Contoso.PetStore.Contracts \
            --version ${{ steps.version.outputs.VERSION }}

      - name: Publish to NuGet
        run: |
          cd src/PetStore.Contracts
          dotnet nuget push *.nupkg \
            --source https://api.nuget.org/v3/index.json \
            --api-key ${{ secrets.NUGET_API_KEY }}
```

---

## Messaging with AsyncAPI

### Scenario

A microservices platform uses MassTransit for messaging. Teams want to:
1. Generate AsyncAPI specs from .NET message types
2. Share message contracts as packages
3. Auto-generate types in consumers

### Project Structure

```
OrderService/
├── src/
│   ├── OrderService.Contracts/     # Message definitions
│   │   ├── Events/
│   │   ├── Commands/
│   │   └── OrderService.Contracts.csproj
│   ├── OrderService.Publisher/     # Produces events
│   │   └── OrderService.Publisher.csproj
│   └── OrderService.Consumer/      # Consumes events
│       └── OrderService.Consumer.csproj
```

### Step 1: Define Message Contracts

`src/OrderService.Contracts/Events/OrderCreatedEvent.cs`:

```csharp
namespace OrderService.Contracts.Events;

public record OrderCreatedEvent
{
    public Guid OrderId { get; init; }
    public Guid CustomerId { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public decimal TotalAmount { get; init; }
    public List<OrderItem> Items { get; init; } = new();
}

public record OrderItem
{
    public Guid ProductId { get; init; }
    public int Quantity { get; init; }
    public decimal UnitPrice { get; init; }
}
```

`src/OrderService.Contracts/Commands/CreateOrderCommand.cs`:

```csharp
namespace OrderService.Contracts.Commands;

public record CreateOrderCommand
{
    public Guid CustomerId { get; init; }
    public List<OrderLineItem> Items { get; init; } = new();
}

public record OrderLineItem
{
    public Guid ProductId { get; init; }
    public int Quantity { get; init; }
}
```

### Step 2: Configure AsyncAPI Server Package

`src/OrderService.Contracts/OrderService.Contracts.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Version>1.0.0</Version>

    <!-- AsyncAPI generation -->
    <ConcordIOEventTypes>OrderService.Contracts.Events.*</ConcordIOEventTypes>
    <ConcordIOCommandTypes>OrderService.Contracts.Commands.*</ConcordIOCommandTypes>
    <ConcordIOAsyncApiOutputFormat>json</ConcordIOAsyncApiOutputFormat>
    <ConcordIOIncludeAsyncApiInPackage>true</ConcordIOIncludeAsyncApiInPackage>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="ConcordIO.AsyncApi.Server" Version="0.1.0">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
  </ItemGroup>
</Project>
```

### Step 3: Build and Pack

```bash
cd src/OrderService.Contracts
dotnet build  # Generates AsyncAPI spec
dotnet pack   # Creates NuGet package with spec
```

Generated spec at `obj/Debug/net10.0/asyncapi/OrderService.Contracts.json`:

```json
{
  "asyncapi": "3.0.0",
  "info": {
    "title": "OrderService.Contracts",
    "version": "1.0.0"
  },
  "channels": {
    "OrderService.Contracts.Events.OrderCreatedEvent": {
      "address": "urn:message:OrderService.Contracts.Events:OrderCreatedEvent",
      "messages": {
        "OrderCreatedEvent": {
          "$ref": "#/components/messages/OrderCreatedEvent"
        }
      }
    }
  },
  "components": {
    "messages": {
      "OrderCreatedEvent": {
        "payload": {
          "$ref": "#/components/schemas/OrderCreatedEvent"
        }
      }
    },
    "schemas": {
      "OrderCreatedEvent": {
        "type": "object",
        "properties": {
          "orderId": { "type": "string", "format": "uuid" },
          "customerId": { "type": "string", "format": "uuid" },
          "createdAt": { "type": "string", "format": "date-time" },
          "totalAmount": { "type": "number" },
          "items": {
            "type": "array",
            "items": { "$ref": "#/components/schemas/OrderItem" }
          }
        },
        "x-dotnet-namespace": "OrderService.Contracts.Events",
        "x-dotnet-type": "OrderService.Contracts.Events.OrderCreatedEvent"
      }
    }
  }
}
```

### Step 4: Publish Contract Package

```bash
dotnet nuget push bin/Debug/OrderService.Contracts.1.0.0.nupkg \
  --source https://your-feed.com/nuget \
  --api-key $API_KEY
```

### Step 5: Consume in Another Service

`src/NotificationService/NotificationService.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <!-- Contract package for types -->
    <PackageReference Include="OrderService.Contracts" Version="1.0.0" />
    
    <!-- Client for code generation -->
    <PackageReference Include="ConcordIO.AsyncApi.Client" Version="0.1.0">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>

    <!-- MassTransit -->
    <PackageReference Include="MassTransit" Version="8.x.x" />
  </ItemGroup>
</Project>
```

### Step 6: Use Generated Types with MassTransit

`src/NotificationService/OrderCreatedConsumer.cs`:

```csharp
using MassTransit;
using OrderService.Contracts.Events;  // Generated from AsyncAPI

public class OrderCreatedConsumer : IConsumer<OrderCreatedEvent>
{
    private readonly ILogger<OrderCreatedConsumer> _logger;

    public OrderCreatedConsumer(ILogger<OrderCreatedConsumer> logger)
    {
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<OrderCreatedEvent> context)
    {
        var order = context.Message;
        _logger.LogInformation(
            "Order {OrderId} created for customer {CustomerId} with {ItemCount} items",
            order.OrderId,
            order.CustomerId,
            order.Items.Count
        );

        // Send notification
        await SendEmailAsync(order);
    }

    private Task SendEmailAsync(OrderCreatedEvent order)
    {
        // Implementation
        return Task.CompletedTask;
    }
}
```

`src/NotificationService/Program.cs`:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<OrderCreatedConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host("rabbitmq://localhost");
        cfg.ConfigureEndpoints(context);
    });
});

var app = builder.Build();
app.Run();
```

---

## gRPC Service with Protocol Buffers

### Scenario

A gRPC service team wants to distribute `.proto` files as NuGet packages.

### Step 1: Define Proto File

`src/UserService.Contracts/protos/user_service.proto`:

```protobuf
syntax = "proto3";

package userservice.v1;

option csharp_namespace = "UserService.Contracts.V1";

service UserService {
  rpc GetUser (GetUserRequest) returns (GetUserResponse);
  rpc CreateUser (CreateUserRequest) returns (CreateUserResponse);
  rpc ListUsers (ListUsersRequest) returns (ListUsersResponse);
}

message GetUserRequest {
  string user_id = 1;
}

message GetUserResponse {
  User user = 1;
}

message CreateUserRequest {
  string email = 1;
  string name = 2;
}

message CreateUserResponse {
  User user = 1;
}

message ListUsersRequest {
  int32 page_size = 1;
  string page_token = 2;
}

message ListUsersResponse {
  repeated User users = 1;
  string next_page_token = 2;
}

message User {
  string user_id = 1;
  string email = 2;
  string name = 3;
  int64 created_at = 4;
}
```

### Step 2: Create Contract Package

```bash
concordio pack \
  --spec protos/user_service.proto:proto \
  --package-id Contoso.UserService.Contracts \
  --version 1.0.0 \
  --client false  # No automatic client generation for Proto
```

### Step 3: Consume in Client

`ClientApp/ClientApp.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Contoso.UserService.Contracts" Version="1.0.0" />
    <PackageReference Include="Grpc.Tools" Version="2.x.x" PrivateAssets="All" />
    <PackageReference Include="Grpc.Net.Client" Version="2.x.x" />
  </ItemGroup>

  <ItemGroup>
    <!-- Wire ConcordIO contracts to Grpc.Tools -->
    <Protobuf Include="@(ConcordIOContract)" 
              Condition="'%(ConcordIOContract.Kind)' == 'proto'"
              GrpcServices="Client" />
  </ItemGroup>
</Project>
```

### Step 4: Use Generated Client

```csharp
using Grpc.Net.Client;
using UserService.Contracts.V1;

var channel = GrpcChannel.ForAddress("https://localhost:5001");
var client = new UserService.UserServiceClient(channel);

var response = await client.GetUserAsync(new GetUserRequest
{
    UserId = "user123"
});

Console.WriteLine($"User: {response.User.Name} ({response.User.Email})");
```

---

## Multi-Protocol Service

### Scenario

A complex service exposes:
- REST API (OpenAPI)
- Message bus (AsyncAPI)
- gRPC endpoints (Proto)

All contracts in one package.

### Project Structure

```
ComplexService/
└── src/
    ├── ComplexService.Api/
    ├── ComplexService.Contracts/
    │   ├── specs/
    │   │   ├── rest-api.yaml      # OpenAPI
    │   │   ├── events.yaml        # AsyncAPI
    │   │   └── grpc-service.proto # Protocol Buffers
    │   └── ComplexService.Contracts.csproj
    └── ComplexService.Consumer/
```

### Generate Multi-Spec Package

```bash
cd src/ComplexService.Contracts

concordio pack \
  --spec specs/rest-api.yaml:openapi \
  --spec specs/events.yaml:asyncapi \
  --spec specs/grpc-service.proto:proto \
  --package-id Contoso.ComplexService.Contracts \
  --version 1.0.0 \
  --authors "Platform Team" \
  --description "Complete contract catalog for ComplexService"
```

Output:
```
✓ Contoso.ComplexService.Contracts.1.0.0.nupkg (contains all 3 specs)
✓ Contoso.ComplexService.Contracts.Client.1.0.0.nupkg (REST + AsyncAPI clients)
```

### Consume All Protocols

```xml
<ItemGroup>
  <!-- Get all contracts -->
  <PackageReference Include="Contoso.ComplexService.Contracts.Client" Version="1.0.0" />
  
  <!-- For gRPC -->
  <PackageReference Include="Grpc.Tools" Version="2.x.x" PrivateAssets="All" />
  
  <!-- For messaging -->
  <PackageReference Include="MassTransit" Version="8.x.x" />
</ItemGroup>

<ItemGroup>
  <!-- Wire Proto to Grpc.Tools -->
  <Protobuf Include="@(ConcordIOContract)"
            Condition="'%(ConcordIOContract.Kind)' == 'proto'"
            GrpcServices="Client" />
</ItemGroup>
```

---

## CI/CD Integration

See [Tutorial: CI/CD Setup](../tutorials/cicd-setup.md) for complete examples.

---

## Multi-Team Workflow

Multi-team workflows involve coordinating contract updates across multiple development teams. Key patterns include:

- Centralized contract repositories
- Team-specific NuGet feeds or feed views
- Breaking change approval workflows
- Automated notifications

---

## Running Examples Locally

All examples above can be adapted to run locally:

1. Install ConcordIO:
```bash
dotnet tool install --global ConcordIO.Tool
```

2. Set up local NuGet feed:
```bash
mkdir ~/local-nuget-feed
dotnet nuget add source ~/local-nuget-feed --name local
```

3. Generate and publish locally:
```bash
concordio pack --spec api.yaml --package-id Test.Api --version 1.0.0
dotnet nuget push *.nupkg --source local
```

4. Consume in test projects:
```xml
<PackageReference Include="Test.Api.Client" Version="1.0.0" />
```

## Next Steps

- [📝 Tutorial: Publishing Your First Contract](../tutorials/publishing-first-contract.md)
- [🔄 Tutorial: Consuming a Contract Package](../tutorials/consuming-contract.md)
- [🚦 Tutorial: CI/CD Setup](../tutorials/cicd-setup.md)
- [🤖 AI Prompts](../ai-prompts/README.md) - Use AI to generate examples
