# Contributing to ConcordIO

Thank you for your interest in contributing to ConcordIO!

## Getting Started

1. Fork the repository
2. Clone your fork
3. Create a new branch for your changes
4. Make your changes following our coding standards
5. Test your changes
6. Submit a pull request

## Commit Message Convention

We use [Conventional Commits](https://www.conventionalcommits.org/) for automated versioning and changelog generation. All commit messages must follow this format:

```
<type>[optional scope]: <description>

[optional body]

[optional footer(s)]
```

### Types

- **feat**: A new feature (triggers minor version bump)
- **fix**: A bug fix (triggers patch version bump)
- **docs**: Documentation only changes (triggers patch version bump)
- **style**: Changes that don't affect code meaning (formatting, whitespace) (triggers patch version bump)
- **refactor**: Code change that neither fixes a bug nor adds a feature (triggers patch version bump)
- **perf**: Performance improvements (triggers patch version bump)
- **test**: Adding or updating tests (triggers patch version bump)
- **chore**: Changes to build process or auxiliary tools (triggers patch version bump)
- **build**: Changes to build system (no version bump)
- **ci**: Changes to CI configuration (no version bump)

### Breaking Changes

To indicate a breaking change, add `!` after the type:

```
feat!: remove deprecated API endpoint

BREAKING CHANGE: The /api/v1/old endpoint has been removed. Use /api/v2/new instead.
```

This triggers a major version bump (e.g., 1.2.3 → 2.0.0).

### Examples

```
feat: add support for AsyncAPI 3.0 specification
```

```
fix: correct version handling in MSBuild task
```

```
docs: update README with installation instructions
```

```
feat!: change contract package structure

BREAKING CHANGE: Contract packages now use a different folder structure.
Consumers need to update their MSBuild imports.
```

## Code Style

- Use tabs for indentation (size 4)
- Follow the formatting rules in `.editorconfig`
- Run `dotnet format src/ConcordIO.Tool.sln` before committing
- Enable the pre-commit hook to auto-format: `git config core.hooksPath .githooks`

## Testing

- Write tests for all new features and bug fixes
- Run unit tests: `dotnet test src/ConcordIO.Tool.sln --filter "FullyQualifiedName!~E2E"`
- Run E2E tests: `dotnet test src/ConcordIO.Tool.sln --filter "FullyQualifiedName~E2E"`

## Documentation

Every code change must include corresponding documentation updates:

- XML documentation for all public APIs
- Update README files for user-facing changes
- Update ARCHITECTURE files for internal changes
- Add examples for complex features

## Pull Request Process

1. Ensure all tests pass
2. Ensure code is properly formatted
3. Update documentation
4. Use a conventional commit message for the PR title
5. Provide a clear description of changes in the PR body
6. Link any related issues

## Release Process

Releases are fully automated via GitHub Actions:

1. When changes are merged to `main`, GitVersion analyzes commit messages to determine the next version
2. If the version should be bumped (based on commit types), the release workflow:
   - Builds all packages with the new version
   - Runs all tests
   - Creates a GitHub Release with the packages
   - **Publishes packages to GitHub Packages (levdevio)** by default

### Publishing Destinations

ConcordIO packages are published to two NuGet sources:

1. **GitHub Packages (levdevio)** - Default destination
   - Published automatically on every push to `main`
   - Source: `https://nuget.pkg.github.com/LevDevIO/index.json`
   - Requires GitHub authentication to consume packages

2. **NuGet.org** - Public NuGet gallery
   - Published only via manual workflow trigger
   - Requires `NUGET_API_KEY` secret to be configured
   - Only stable versions (no pre-release tags) can be published

### Version Calculation

GitVersion uses mainline mode with these rules:

- Initial version starts at `0.1.0`
- Each `feat:` commit bumps the minor version
- Each `fix:`, `docs:`, etc. commit bumps the patch version
- Any commit with `!` (breaking change) bumps the major version
- `build:` and `ci:` commits don't bump the version

### Manual Releases

Maintainers can trigger releases manually with control over publish destinations:

1. Go to Actions → Release workflow
2. Click "Run workflow"
3. Select the `main` branch
4. Choose publishing options:
   - **Publish to GitHub Packages**: Enabled by default
   - **Publish to NuGet.org**: Disabled by default (enable when ready for public release)
5. Click "Run workflow"

**Note**: Publishing to NuGet.org requires the `NUGET_API_KEY` secret to be configured in repository settings.

## Questions?

If you have questions, please open an issue or discussion on GitHub.
