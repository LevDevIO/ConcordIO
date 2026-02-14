# ConcordIO

ConcordIO is an open source, **NuGet-first** contract management toolchain for **.NET**, focused on making API contracts easy to **publish**, **consume**, and **govern**.

## Vision

ConcordIO will provide a CLI (distributed as a NuGet / .NET tool) and build integration that helps teams:

- **Package API contracts** into NuGet packages:
  - **OpenAPI** (JSON/YAML)
  - **Protocol Buffers** (`.proto`)
- **Generate clients at build time** by consuming contract packages, using MSBuild integration (`.props` / `.targets`) so projects can produce strongly-typed clients without copying specs into each repo.
- **Detect contract changes** by comparing the current contract against an existing published NuGet package:
  - report **breaking vs non-breaking** changes
  - recommend an appropriate **SemVer bump** (major/minor/patch)
- **Integrate with CI/CD** (GitHub and Azure DevOps) to enforce policy, including requiring **additional manual approvals** when breaking changes are detected.

## Status

This project is in early design / prototype stage.

## Development

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

## License

Licensed under the Apache License 2.0. See [LICENSE](LICENSE).