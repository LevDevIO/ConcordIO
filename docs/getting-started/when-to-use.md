# 🎯 When to Use ConcordIO

ConcordIO solves specific problems in API contract management for .NET teams. This guide helps you decide if ConcordIO is right for your project.

## ✅ Use ConcordIO When...

### You Need Contract-First Development

**Scenario**: Your team defines APIs before implementation (contract-first/design-first approach)

**Why ConcordIO**: 
- Package your OpenAPI, AsyncAPI, or Proto specs as NuGet packages
- Share contracts across teams and services
- Keep contract and implementation in sync

**Example**: A platform team maintains API contracts that multiple microservices must implement.

### You Want Automatic Client Generation

**Scenario**: Consuming services need strongly-typed clients for your APIs

**Why ConcordIO**:
- Automatic code generation at build time
- No manual code copying or duplication
- Always in sync with the contract

**Example**: A frontend team consumes backend API contracts and wants TypeScript/C# clients generated automatically.

### You Need Breaking Change Detection

**Scenario**: You want to prevent accidental breaking changes in APIs

**Why ConcordIO**:
- Compare new specs against published versions
- Detect breaking changes automatically
- Integrate with CI/CD for policy enforcement

**Example**: A public API team needs to ensure backward compatibility before releasing updates.

### You Have Multiple Spec Types

**Scenario**: Your service uses REST (OpenAPI), messaging (AsyncAPI), and gRPC (Proto)

**Why ConcordIO**:
- Single tool for all spec types
- Unified packaging and versioning
- Consistent workflow across protocols

**Example**: A microservice with REST endpoints, message queues, and gRPC calls needs to manage all contracts.

### You Want Centralized Contract Governance

**Scenario**: Multiple teams need to discover and consume contracts

**Why ConcordIO**:
- Contracts are versioned NuGet packages
- Use existing NuGet infrastructure (feeds, security, discovery)
- Standard .NET package management workflow

**Example**: An enterprise with dozens of services needs a centralized contract repository.

### You Need CI/CD Integration

**Scenario**: Automated pipelines should validate and publish contracts

**Why ConcordIO**:
- CLI tool for scripting
- Exit codes for automation
- Works with GitHub Actions, Azure DevOps, etc.

**Example**: Every PR should check for breaking changes before merging.

## ❌ Don't Use ConcordIO When...

### You Don't Use .NET

**Why**: ConcordIO is designed for .NET ecosystems
- MSBuild integration requires .NET projects
- Client generation assumes C# consumers

**Alternative**: 
- Use language-specific tools (Swagger Codegen, OpenAPI Generator)
- Continue with your existing toolchain

### You Have Simple, Single-Service APIs

**Why**: ConcordIO adds overhead for simple scenarios
- Solo projects may not need contract packages
- Local files might be simpler

**Alternative**:
- Keep specs in your service repo
- Use direct file references for code generation

### You Don't Need Client Generation

**Why**: If you manually write clients or don't consume your own APIs
- Contract packages are less valuable
- Standard spec hosting (Swagger UI, docs sites) might be enough

**Alternative**:
- Host specs on documentation sites
- Use API management platforms

### You Need Real-Time API Discovery

**Why**: NuGet packages are versioned and immutable
- Not designed for dynamic API catalogs
- Package discovery is manual (browse feed, search)

**Alternative**:
- Use API management platforms (Azure APIM, Kong)
- Use service mesh with dynamic discovery

### Your Team Doesn't Use NuGet

**Why**: ConcordIO leverages NuGet infrastructure
- Requires NuGet feeds
- Assumes familiarity with package management

**Alternative**:
- Use artifact repositories (Artifactory, Nexus)
- Use Git submodules for contracts

## 🎯 Ideal Use Cases

### Use Case 1: Microservices Platform

**Context**: 
- 20+ microservices
- Multiple teams
- REST + messaging + gRPC

**ConcordIO Benefits**:
- ✅ Centralized contract repository (NuGet feed)
- ✅ Automatic client generation across all services
- ✅ Breaking change detection in CI/CD
- ✅ Single tool for all protocols

### Use Case 2: Public API Provider

**Context**:
- External consumers
- SemVer versioning required
- Backward compatibility critical

**ConcordIO Benefits**:
- ✅ Breaking change detection before release
- ✅ Versioned contract packages for consumers
- ✅ Clear upgrade paths (package versions)
- ✅ CI/CD integration for quality gates

### Use Case 3: Multi-Team Enterprise

**Context**:
- Multiple product teams
- Shared platform services
- Contract governance needed

**ConcordIO Benefits**:
- ✅ Contract packages as NuGet artifacts
- ✅ Standard package management workflow
- ✅ Integration with existing NuGet infrastructure
- ✅ Automatic client generation eliminates SDK maintenance

### Use Case 4: AsyncAPI/MassTransit Messaging

**Context**:
- Event-driven architecture
- MassTransit message contracts
- Multiple consumers

**ConcordIO Benefits**:
- ✅ Generate AsyncAPI specs from .NET types
- ✅ Distribute message contracts as packages
- ✅ Automatic contract generation for consumers
- ✅ Type-safe messaging across services

## 🔄 Migration Scenarios

### Migrating from Manual Client Distribution

**Before**: Manually maintain and distribute client SDKs

**With ConcordIO**:
1. Package your OpenAPI specs with `concordio pack`
2. Publish contract + client packages
3. Consumers reference client package
4. Clients generate automatically on build

**Benefits**: No SDK maintenance, always in sync

### Migrating from Spec Files in Repos

**Before**: Copy spec files between repos, manual updates

**With ConcordIO**:
1. Create contract package from spec
2. Reference contract package instead of copying files
3. Update contract version to get updates

**Benefits**: Single source of truth, version control

### Adding to Existing NSwag Setup

**Before**: NSwag with local spec files

**With ConcordIO**:
1. Create contract package from your specs
2. Replace local `<OpenApiReference>` with package reference
3. Specs now come from NuGet, not local files

**Benefits**: Centralized contracts, easier to share

## 📊 Decision Matrix

| Factor | Use ConcordIO | Consider Alternatives |
|--------|---------------|----------------------|
| Team size | Multiple teams | Single team |
| Services | Multiple services | Monolith |
| Consumers | Multiple consumers | Internal only |
| Spec types | Mixed (REST/gRPC/Messaging) | Single type |
| Platform | .NET | Other languages |
| Infrastructure | NuGet feeds available | No NuGet |
| Versioning | Strict SemVer needed | Loose versioning |
| CI/CD | Automated pipelines | Manual deployments |
| Governance | Centralized | Decentralized |

## Next Steps

Ready to get started?

- [🚀 Quick Start Guide](./quick-start.md) - Install and try ConcordIO
- [🏗️ Core Concepts](./concepts.md) - Understand how it works
- [📝 Tutorial: Publishing Your First Contract](../tutorials/publishing-first-contract.md) - Step-by-step guide

Still have questions?

- [❓ FAQ](../troubleshooting/faq.md) - Common questions answered
- [GitHub Discussions](https://github.com/LevDevIO/ConcordIO/discussions) - Ask the community
