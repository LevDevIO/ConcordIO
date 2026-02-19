# ConcordIO Documentation

Welcome to the ConcordIO documentation! This comprehensive guide will help you understand, install, and use ConcordIO's contract management toolchain for .NET.

> 💡 **Tip**: This documentation is also available on [GitHub Wiki](https://github.com/LevDevIO/ConcordIO/wiki) for easier browsing. The Wiki is automatically synced with the repository.

> ⚠️ **Important: Development-Time Toolchain Only**  
> ConcordIO is strictly a **development and build-time dependency**. It consists of:
> - **CLI tool** for generating contract packages
> - **MSBuild tasks** that run during compilation to generate code
> 
> **ConcordIO does NOT**:
> - ❌ Get bundled into your application binaries
> - ❌ Deploy to production environments
> - ❌ Run at application runtime
> - ❌ Leak into your production code
> 
> All code generation happens at build time, and only the generated output (client code, types) is compiled into your application—not ConcordIO itself.

## 📚 Table of Contents

### Getting Started
- [🚀 Quick Start Guide](./getting-started/quick-start.md) - Get up and running in 5 minutes
- [📦 Installation](./getting-started/installation.md) - Detailed installation instructions
- [🎯 When to Use ConcordIO](./getting-started/when-to-use.md) - Use cases and scenarios
- [🏗️ Core Concepts](./getting-started/concepts.md) - Understanding contract packages, clients, and breaking changes

### User Guides
- [🛠️ CLI Tool Guide](../src/ConcordIO.Tool/README.md) - Complete reference for the `concordio` CLI
- [📄 AsyncAPI Client Package](../src/ConcordIO.AsyncApi.Client/README.md) - MSBuild task for generating C# from AsyncAPI specs
- [⚡ AsyncAPI Server Package](../src/ConcordIO.AsyncApi.Server/README.md) - MSBuild task for generating AsyncAPI specs from .NET types
- [📚 AsyncAPI Library](../src/ConcordIO.AsyncApi/README.md) - Shared AsyncAPI generation library

### Tutorials
- [📝 Tutorial: Publishing Your First API Contract](./tutorials/publishing-first-contract.md)
- [🔄 Tutorial: Consuming a Contract Package](./tutorials/consuming-contract.md)
- [🚦 Tutorial: Setting Up CI/CD with Breaking Change Detection](./tutorials/cicd-setup.md)

### Examples
- [💡 Example Projects](./examples/README.md) - Complete working examples
  - REST API with OpenAPI
  - Messaging with AsyncAPI
  - gRPC Service with Protocol Buffers
  - Multi-Protocol Service

### AI & Automation
- [🤖 AI Prompts](./ai-prompts/README.md) - Ready-to-use prompts for AI assistants
  - Adding ConcordIO to Projects
  - Generating Contracts
  - Troubleshooting Issues
  - Code Reviews

### Architecture & Design
- [🏗️ CLI Tool Architecture](../src/ConcordIO.Tool/ARCHITECTURE.md) - Internal design of the CLI tool
- [🏗️ AsyncAPI Library Architecture](../src/ConcordIO.AsyncApi/ARCHITECTURE.md) - Core AsyncAPI generation design
- [🏗️ AsyncAPI Client Architecture](../src/ConcordIO.AsyncApi.Client/ARCHITECTURE.md) - MSBuild task design for client generation
- [🏗️ AsyncAPI Server Architecture](../src/ConcordIO.AsyncApi.Server/ARCHITECTURE.md) - MSBuild task design for spec generation

### Troubleshooting
- [❓ FAQ](./troubleshooting/faq.md) - Frequently asked questions
- [🐛 Common Issues](./troubleshooting/common-issues.md) - Known problems and solutions
- [⚠️ Known Limitations](./troubleshooting/known-limitations.md) - Current limitations and workarounds

### Contributing
- [🤝 Contributing Guide](../CONTRIBUTING.md) - How to contribute to ConcordIO

## 🔗 Quick Links

- [GitHub Repository](https://github.com/LevDevIO/ConcordIO)
- [Report Issues](https://github.com/LevDevIO/ConcordIO/issues)
- [Release Notes](https://github.com/LevDevIO/ConcordIO/releases)

## 📖 Documentation Format

This documentation is organized to help you:
- **Learn** the basics quickly with getting started guides
- **Reference** specific commands and options when needed
- **Solve** problems with troubleshooting guides
- **Extend** your usage with advanced topics
- **Automate** with AI prompts and templates

Each guide includes:
- ✅ **Prerequisites** - What you need before starting
- 📝 **Step-by-step instructions** - Clear, actionable steps
- 💡 **Examples** - Real-world code samples
- ⚠️ **Common pitfalls** - What to watch out for
- 🔗 **Related topics** - Where to learn more

## 🆘 Getting Help

If you can't find what you're looking for:

1. Check the [FAQ](./troubleshooting/faq.md)
2. Search existing [GitHub Issues](https://github.com/LevDevIO/ConcordIO/issues)
3. Open a new issue with the `documentation` label

## 📄 License

This documentation is part of the ConcordIO project and is licensed under the Apache License 2.0.
