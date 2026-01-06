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

## License

Licensed under the Apache License 2.0. See [LICENSE](LICENSE).