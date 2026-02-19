# 📝 Tutorial: Publishing Your First API Contract

This step-by-step tutorial walks you through publishing an OpenAPI contract as a NuGet package using ConcordIO.

**Time Required**: 15 minutes  
**Prerequisites**: 
- .NET 10 SDK installed
- A NuGet feed (can be local for testing)
- Basic familiarity with OpenAPI

## What You'll Learn

- How to create an OpenAPI specification
- How to package it with ConcordIO
- How to publish to a NuGet feed
- How to version your contracts

## Step 1: Create Your API Specification

Let's create a simple API for managing books.

Create a file `books-api.yaml`:

```yaml
openapi: 3.0.0
info:
  title: Books API
  version: 1.0.0
  description: A simple API for managing books

servers:
  - url: https://api.bookstore.example.com
    description: Production server

paths:
  /books:
    get:
      operationId: listBooks
      summary: List all books
      parameters:
        - name: limit
          in: query
          schema:
            type: integer
            minimum: 1
            maximum: 100
            default: 20
      responses:
        '200':
          description: A list of books
          content:
            application/json:
              schema:
                type: object
                properties:
                  books:
                    type: array
                    items:
                      $ref: '#/components/schemas/Book'
                  total:
                    type: integer
                  
    post:
      operationId: createBook
      summary: Create a new book
      requestBody:
        required: true
        content:
          application/json:
            schema:
              $ref: '#/components/schemas/CreateBookRequest'
      responses:
        '201':
          description: Book created successfully
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/Book'
        '400':
          description: Invalid request

  /books/{bookId}:
    get:
      operationId: getBook
      summary: Get a book by ID
      parameters:
        - name: bookId
          in: path
          required: true
          schema:
            type: string
            format: uuid
      responses:
        '200':
          description: The requested book
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/Book'
        '404':
          description: Book not found
          
    put:
      operationId: updateBook
      summary: Update a book
      parameters:
        - name: bookId
          in: path
          required: true
          schema:
            type: string
            format: uuid
      requestBody:
        required: true
        content:
          application/json:
            schema:
              $ref: '#/components/schemas/UpdateBookRequest'
      responses:
        '200':
          description: Book updated successfully
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/Book'
        '404':
          description: Book not found
          
    delete:
      operationId: deleteBook
      summary: Delete a book
      parameters:
        - name: bookId
          in: path
          required: true
          schema:
            type: string
            format: uuid
      responses:
        '204':
          description: Book deleted successfully
        '404':
          description: Book not found

components:
  schemas:
    Book:
      type: object
      required:
        - id
        - title
        - author
      properties:
        id:
          type: string
          format: uuid
          description: Unique identifier for the book
        title:
          type: string
          minLength: 1
          maxLength: 200
          description: Book title
        author:
          type: string
          minLength: 1
          maxLength: 100
          description: Book author
        isbn:
          type: string
          pattern: '^[0-9]{3}-[0-9]{10}$'
          description: ISBN-13 format
          example: '978-0123456789'
        publicationYear:
          type: integer
          minimum: 1000
          maximum: 9999
          description: Year the book was published
        genre:
          type: string
          enum:
            - fiction
            - non-fiction
            - science
            - history
            - biography
          description: Book genre
        price:
          type: number
          format: decimal
          minimum: 0
          description: Book price in USD
          
    CreateBookRequest:
      type: object
      required:
        - title
        - author
      properties:
        title:
          type: string
          minLength: 1
          maxLength: 200
        author:
          type: string
          minLength: 1
          maxLength: 100
        isbn:
          type: string
          pattern: '^[0-9]{3}-[0-9]{10}$'
        publicationYear:
          type: integer
          minimum: 1000
          maximum: 9999
        genre:
          type: string
          enum:
            - fiction
            - non-fiction
            - science
            - history
            - biography
        price:
          type: number
          format: decimal
          minimum: 0
          
    UpdateBookRequest:
      type: object
      properties:
        title:
          type: string
          minLength: 1
          maxLength: 200
        author:
          type: string
          minLength: 1
          maxLength: 100
        isbn:
          type: string
          pattern: '^[0-9]{3}-[0-9]{10}$'
        publicationYear:
          type: integer
          minimum: 1000
          maximum: 9999
        genre:
          type: string
          enum:
            - fiction
            - non-fiction
            - science
            - history
            - biography
        price:
          type: number
          format: decimal
          minimum: 0
```

**Validation**: Before continuing, validate your OpenAPI spec:

```bash
# Using Spectral (optional)
npx @stoplight/spectral-cli lint books-api.yaml
```

## Step 2: Install ConcordIO

Install the ConcordIO CLI tool:

```bash
dotnet tool install --global ConcordIO.Tool
```

Verify installation:

```bash
concordio --version
```

You should see output like: `ConcordIO.Tool 0.x.x`

## Step 3: Generate Contract Packages

Now let's generate the contract and client packages:

```bash
concordio pack \
  --spec books-api.yaml \
  --package-id Contoso.BookStore.Api \
  --version 1.0.0 \
  --authors "BookStore Team" \
  --description "Books API contract for Contoso BookStore" \
  --package-properties "RepositoryUrl=https://github.com/contoso/bookstore"
```

**What happens**:
1. ConcordIO reads your OpenAPI spec
2. Generates a contract package with the spec file
3. Generates a client package for automatic code generation
4. Creates `.nupkg` files ready to publish

**Output**:
```
✓ Generated contract package structure
✓ Generated client package structure
✓ Created Contoso.BookStore.Api.1.0.0.nupkg
✓ Created Contoso.BookStore.Api.Client.1.0.0.nupkg
```

## Step 4: Inspect the Packages

Let's see what was created:

```bash
ls -la *.nupkg
```

You should see two files:
- `Contoso.BookStore.Api.1.0.0.nupkg` - Contract package
- `Contoso.BookStore.Api.Client.1.0.0.nupkg` - Client package

**Understanding the packages**:

**Contract Package** contains:
- `openapi/books-api.yaml` - Your spec file
- `build/Contoso.BookStore.Api.targets` - MSBuild integration
- Package metadata

**Client Package** contains:
- `.targets` file that wires to NSwag
- Dependency on contract package
- Dependency on NSwag.MSBuild

## Step 5: Set Up a NuGet Feed

For testing, let's create a local NuGet feed:

```bash
# Create local feed directory
mkdir ~/bookstore-nuget-feed

# Add as source
dotnet nuget add source ~/bookstore-nuget-feed --name bookstore-local
```

For production, you'd use:
- **NuGet.org** (public packages)
- **GitHub Packages** (private or org packages)
- **Azure Artifacts** (enterprise)
- **Self-hosted** (Artifactory, Nexus, etc.)

## Step 6: Publish the Packages

Publish to your local feed:

```bash
dotnet nuget push *.nupkg --source bookstore-local
```

For NuGet.org (after creating an account and getting an API key):

```bash
dotnet nuget push *.nupkg \
  --source https://api.nuget.org/v3/index.json \
  --api-key YOUR_API_KEY
```

For GitHub Packages:

```bash
dotnet nuget push *.nupkg \
  --source https://nuget.pkg.github.com/OWNER/index.json \
  --api-key $GITHUB_TOKEN
```

**Success!** Your packages are now published and ready to consume.

## Step 7: Verify Publication

List packages in your feed:

```bash
# Local feed
ls ~/bookstore-nuget-feed

# NuGet.org
# Visit https://www.nuget.org/packages/Contoso.BookStore.Api

# GitHub Packages
# Visit https://github.com/OWNER/REPO/packages
```

## Step 8: Version Your Contract

Let's say you want to add a new field to the Book schema. Update `books-api.yaml`:

```yaml
components:
  schemas:
    Book:
      type: object
      required:
        - id
        - title
        - author
      properties:
        # ... existing fields ...
        summary:  # NEW FIELD (non-breaking)
          type: string
          maxLength: 500
          description: Brief book summary
```

**Check for breaking changes**:

```bash
concordio breaking \
  --spec books-api.yaml \
  --package-id Contoso.BookStore.Api \
  --version 1.0.0
```

Expected output:
```
No breaking changes detected.
Exit code: 0
```

Since adding an optional field is non-breaking, we can release as **1.1.0** (minor version bump):

```bash
concordio pack \
  --spec books-api.yaml \
  --package-id Contoso.BookStore.Api \
  --version 1.1.0 \
  --authors "BookStore Team" \
  --description "Books API contract for Contoso BookStore"

dotnet nuget push *.nupkg --source bookstore-local
```

## Step 9: Test with a Breaking Change

Now let's try a breaking change. Remove a required field:

```yaml
components:
  schemas:
    Book:
      type: object
      required:
        - id
        - title
        # REMOVED: - author (BREAKING CHANGE!)
```

Check for breaking changes:

```bash
concordio breaking \
  --spec books-api.yaml \
  --package-id Contoso.BookStore.Api \
  --version 1.1.0
```

Expected output:
```
Breaking changes detected:
- Removed required property 'author' from schema 'Book'
Exit code: 1
```

Since this is a breaking change, we must release as **2.0.0** (major version bump):

```bash
# Revert the change or proceed with major version
concordio pack \
  --spec books-api.yaml \
  --package-id Contoso.BookStore.Api \
  --version 2.0.0 \
  --authors "BookStore Team" \
  --description "Books API contract for Contoso BookStore (BREAKING CHANGES)"
```

## Step 10: Automate with CI/CD

For production, automate this process. Example GitHub Actions workflow:

`.github/workflows/publish-contracts.yml`:

```yaml
name: Publish Contracts

on:
  push:
    branches: [main]
    paths:
      - 'specs/**'

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
          concordio breaking \
            --spec specs/books-api.yaml \
            --package-id Contoso.BookStore.Api || echo "BREAKING=true" >> $GITHUB_OUTPUT
        continue-on-error: true

      - name: Determine version
        id: version
        run: |
          # Use GitVersion or similar
          if [ "${{ steps.breaking.outputs.BREAKING }}" == "true" ]; then
            echo "VERSION=2.0.0" >> $GITHUB_OUTPUT
          else
            echo "VERSION=1.1.0" >> $GITHUB_OUTPUT
          fi

      - name: Pack contracts
        run: |
          concordio pack \
            --spec specs/books-api.yaml \
            --package-id Contoso.BookStore.Api \
            --version ${{ steps.version.outputs.VERSION }}

      - name: Publish to GitHub Packages
        run: |
          dotnet nuget push *.nupkg \
            --source https://nuget.pkg.github.com/${{ github.repository_owner }}/index.json \
            --api-key ${{ secrets.GITHUB_TOKEN }}
```

## Best Practices

### 1. Use Semantic Versioning

- **Major** (X.0.0): Breaking changes
- **Minor** (x.X.0): New features, backward compatible
- **Patch** (x.x.X): Bug fixes

### 2. Validate Specs Before Publishing

```bash
# OpenAPI
npx @stoplight/spectral-cli lint api.yaml

# Then generate
concordio pack --spec api.yaml --package-id My.Api --version 1.0.0
```

### 3. Include Metadata

```bash
concordio pack \
  --spec api.yaml \
  --package-id My.Api \
  --version 1.0.0 \
  --package-properties "RepositoryUrl=https://github.com/my/repo" \
  --package-properties "Tags=api;rest;openapi" \
  --package-properties "ProjectUrl=https://docs.example.com"
```

### 4. Test Packages Locally First

Use a local feed for testing before publishing to production:

```bash
# Generate
concordio pack --spec api.yaml --package-id Test.Api --version 1.0.0-preview.1

# Publish to local feed
dotnet nuget push *.nupkg --source local

# Test consumption in a sample project

# Then publish to production with final version
```

### 5. Document Breaking Changes

When releasing major versions, include release notes:

```bash
# Create GitHub Release with notes
gh release create v2.0.0 \
  --title "v2.0.0 - Breaking Changes" \
  --notes "Breaking changes:
- Removed 'author' field from Book schema
- See MIGRATION.md for upgrade guide"
```

## Troubleshooting

### Issue: "Spec file not found"

**Solution**: Use full path or ensure you're in the correct directory:

```bash
concordio pack --spec ./specs/api.yaml --package-id My.Api --version 1.0.0
```

### Issue: "Failed to create package"

**Solution**: Check write permissions and disk space:

```bash
# Check directory permissions
ls -la

# Specify output directory
concordio pack --spec api.yaml --package-id My.Api --version 1.0.0 --output ./output
```

### Issue: "Package already exists"

**Solution**: NuGet packages are immutable. Use a new version:

```bash
concordio pack --spec api.yaml --package-id My.Api --version 1.0.1
```

Or use pre-release versions for iteration:

```bash
concordio pack --spec api.yaml --package-id My.Api --version 1.0.0-preview.1
```

## Next Steps

Now that you've published your first contract:

- [Tutorial: Consuming a Contract Package](./consuming-contract.md) - Learn how to use your published contract
- [Tutorial: CI/CD Setup](./cicd-setup.md) - Automate the process
- [**Example: Auto-Generate OpenAPI from ASP.NET Core**](../examples/README.md#auto-generating-openapi-from-aspnet-core-api) - Generate specs from API code (recommended)
- [**Example: Using Kiota for Client Generation**](../examples/README.md#using-kiota-for-client-generation-alternative-to-nswag) - Modern client alternative to NSwag
- [CLI Tool Guide](../../src/ConcordIO.Tool/README.md) - Complete command reference
- [AsyncAPI Server Package](../../src/ConcordIO.AsyncApi.Server/README.md) - Server-side AsyncAPI generation
- [Examples](../examples/README.md) - More complete examples

## Alternative Approach: Auto-Generate Specs from ASP.NET Core

Instead of manually writing OpenAPI YAML files, consider using **Microsoft.Extensions.ApiDescription.Server** to automatically generate OpenAPI specs from your ASP.NET Core API at build time:

**Benefits:**
- ✅ Specs always match your implementation
- ✅ No manual YAML/JSON writing
- ✅ XML comments become OpenAPI descriptions
- ✅ Automatic updates when controllers change

**Quick Setup:**

1. Add to your ASP.NET Core API project:
   ```xml
   <PackageReference Include="Microsoft.Extensions.ApiDescription.Server" Version="8.0.0">
     <PrivateAssets>all</PrivateAssets>
   </PackageReference>
   ```

2. Build generates `obj/Debug/net10.0/YourApi.json`

3. Package the generated spec:
   ```bash
   concordio pack \
     --spec obj/Debug/net10.0/YourApi.json \
     --package-id Your.Api \
     --version 1.0.0
   ```

**See the complete example**: [Auto-Generating OpenAPI from ASP.NET Core API](../examples/README.md#auto-generating-openapi-from-aspnet-core-api)

## Summary

You've learned how to:
- ✅ Create an OpenAPI specification
- ✅ Package it with ConcordIO
- ✅ Publish to a NuGet feed
- ✅ Version contracts with SemVer
- ✅ Detect breaking changes
- ✅ Automate with CI/CD
- ✅ Explore automatic spec generation from code

**Congratulations!** You're now ready to manage API contracts with ConcordIO.
