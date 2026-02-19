# ConcordIO

ConcordIO is an open source, **NuGet-first** contract management toolchain for **.NET**, focused on making API contracts easy to **publish**, **consume**, and **govern**.

> ⚠️ **Important**: ConcordIO is a **development-time and build-time toolchain only**. It consists of MSBuild tasks and a CLI tool that run during compilation and CI/CD. **ConcordIO does NOT get bundled into your runtime binaries or deployed to production**. It's a dev/build dependency that generates code at compile time but does not leak into your production code or deployments.

## 📚 Documentation

**[→ View Complete Documentation](./docs/README.md)** | **[📖 Browse on Wiki](https://github.com/LevDevIO/ConcordIO/wiki)**

> **Note**: Documentation is available both in the repository (`docs/` folder) and on GitHub Wiki for easier browsing. Both sources are kept in sync automatically.

### Quick Links
- [🚀 Quick Start Guide](./docs/getting-started/quick-start.md) - Get up and running in 5 minutes
- [📦 Installation](./docs/getting-started/installation.md) - Installation instructions
- [🎯 When to Use ConcordIO](./docs/getting-started/when-to-use.md) - Use cases and scenarios
- [💡 Examples](./docs/examples/README.md) - Complete working examples
- [🤖 AI Prompts](./docs/ai-prompts/README.md) - Ready-to-use prompts for AI assistants
- [❓ FAQ](./docs/troubleshooting/faq.md) - Frequently asked questions
- [🐛 Troubleshooting](./docs/troubleshooting/common-issues.md) - Common issues and solutions

## Vision

ConcordIO provides a CLI (distributed as a NuGet / .NET tool) and build integration that helps teams:

- **Package API contracts** into NuGet packages:
  - **OpenAPI** (JSON/YAML)
  - **Protocol Buffers** (`.proto`)
  - **AsyncAPI** (JSON/YAML)
- **Generate clients at build time** by consuming contract packages, using MSBuild integration (`.props` / `.targets`) so projects can produce strongly-typed clients without copying specs into each repo.
- **Detect contract changes** by comparing the current contract against an existing published NuGet package:
  - report **breaking vs non-breaking** changes
  - recommend an appropriate **SemVer bump** (major/minor/patch)
- **Integrate with CI/CD** (GitHub and Azure DevOps) to enforce policy, including requiring **additional manual approvals** when breaking changes are detected.

## Status

This project is in active development. See the [releases page](https://github.com/LevDevIO/ConcordIO/releases) for current status.

## Development

### Versioning and Releases

ConcordIO uses **Conventional Commits** and **GitVersion** for automated versioning and releases:

- **Version determination**: Versions are automatically calculated from commit messages
  - `feat:` → Minor version bump (e.g., 1.0.0 → 1.1.0)
  - `fix:`, `docs:`, `style:`, `refactor:`, `perf:`, `test:`, `chore:` → Patch version bump (e.g., 1.0.0 → 1.0.1)
  - `feat!:`, `fix!:` (with `!`) → Major version bump for breaking changes (e.g., 1.0.0 → 2.0.0)
  - `build:`, `ci:` → No version bump
- **Commit format**: Use [Conventional Commits](https://www.conventionalcommits.org/) format:
  ```
  <type>[optional scope]: <description>
  
  [optional body]
  
  [optional footer(s)]
  ```
- **Local development**: Projects use version `0.0.1-local` when building locally
- **CI/CD**: The release workflow automatically:
  1. Determines the version from commit history
  2. Builds and tests all packages with the calculated version
  3. Creates a GitHub Release with all packages
  4. **Publishes packages to GitHub Packages (levdevio)** by default
  5. Publishes to NuGet.org only via manual trigger (when ready for public release)

**Note**: Do not include version numbers in `.csproj` files. Versions are managed through GitVersion configuration in `GitVersion.yml`.

#### Package Sources

- **GitHub Packages**: `https://nuget.pkg.github.com/LevDevIO/index.json` (default, published on every push to main)
- **NuGet.org**: `https://api.nuget.org/v3/index.json` (manual trigger only)

### Code Formatting

This repository uses automated code formatting to ensure consistency:

- **EditorConfig**: `.editorconfig` at the repo root enforces tab-based indentation and C# formatting rules
- **CI Enforcement**: The `dotnet format check` workflow runs on all PRs and must pass before merging
- **Local Hook** (optional): Enable the pre-commit hook to auto-format code before commits:
  ```bash
  git config core.hooksPath .githooks
  ```

To manually format code:
```bash
dotnet format src/ConcordIO.Tool.sln
```

To check if code is properly formatted:
```bash
dotnet format src/ConcordIO.Tool.sln --verify-no-changes
```

### Testing

E2E tests generate temporary consumer projects that now multi-target **net8.0;net9.0;net10.0** for CLI OpenAPI flows and **net9.0;net10.0** for AsyncAPI flows to validate tool packages across supported frameworks. This ensures MSBuild tasks and generated outputs work under all supported runtimes during end-to-end flows.

## License

Licensed under the Apache License 2.0. See [LICENSE](LICENSE).
