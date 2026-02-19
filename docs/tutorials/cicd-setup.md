# 🚦 Tutorial: Setting Up CI/CD with Breaking Change Detection

Learn how to automate contract publishing and enforce breaking change policies in your CI/CD pipelines.

**Time Required**: 20 minutes  
**Prerequisites**:
- GitHub repository (examples use GitHub Actions)
- Access to NuGet feed
- Basic CI/CD knowledge

## What You'll Learn

- Automate contract package publication
- Detect breaking changes in PRs
- Block merges when breaking changes are detected
- Implement semantic versioning automatically
- Set up multi-environment pipelines

## Architecture Overview

```
┌─────────────┐
│ Developer   │
│ Updates Spec│
└──────┬──────┘
       │
       ▼
┌─────────────────┐
│ Pull Request    │
│ ┌─────────────┐ │
│ │Check Breaking│ │ ──► Exit 1 = Fail PR
│ │Changes      │ │
│ └─────────────┘ │
└────────┬────────┘
         │ Approved & Merged
         ▼
┌─────────────────┐
│ Main Branch     │
│ ┌─────────────┐ │
│ │Auto Version │ │
│ │Generate Pkg │ │
│ │Publish      │ │
│ └─────────────┘ │
└─────────────────┘
```

## Part 1: Breaking Change Detection in PRs

### Step 1: Create PR Check Workflow

Create `.github/workflows/check-contracts.yml`:

```yaml
name: Check API Contracts

on:
  pull_request:
    paths:
      - 'specs/**'
      - '.github/workflows/check-contracts.yml'

jobs:
  check-breaking-changes:
    name: Check for Breaking Changes
    runs-on: ubuntu-latest
    
    steps:
      - name: Checkout code
        uses: actions/checkout@v4
        with:
          fetch-depth: 0  # Need full history for comparison
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      
      - name: Install ConcordIO
        run: dotnet tool install --global ConcordIO.Tool
      
      - name: Install NuGet CLI
        run: |
          curl -o nuget.exe https://dist.nuget.org/win-x86-commandline/latest/nuget.exe
          sudo mv nuget.exe /usr/local/bin/nuget
          sudo chmod +x /usr/local/bin/nuget
      
      - name: Check for breaking changes
        id: breaking
        run: |
          set +e  # Don't fail immediately
          
          # Run breaking change detection
          concordio breaking \
            --spec specs/api.yaml \
            --package-id Contoso.BookStore.Api \
            > breaking-output.txt 2>&1
          
          EXIT_CODE=$?
          echo "exit_code=$EXIT_CODE" >> $GITHUB_OUTPUT
          
          # Read output for comment
          echo 'output<<EOF' >> $GITHUB_OUTPUT
          cat breaking-output.txt >> $GITHUB_OUTPUT
          echo 'EOF' >> $GITHUB_OUTPUT
          
          exit 0  # Don't fail yet, handle in next step
      
      - name: Comment on PR
        uses: actions/github-script@v7
        with:
          script: |
            const exitCode = '${{ steps.breaking.outputs.exit_code }}';
            const output = `${{ steps.breaking.outputs.output }}`;
            
            let comment = '';
            if (exitCode === '1') {
              comment = `## ⚠️ Breaking Changes Detected

The changes in this PR introduce breaking changes to the API contract.

<details>
<summary>Details</summary>

\`\`\`
${output}
\`\`\`

</details>

### Required Actions:
1. Review the breaking changes above
2. Update the package version to a **major** version (e.g., 1.x.x → 2.0.0)
3. Document the breaking changes in \`CHANGELOG.md\`
4. Provide a migration guide for consumers

Breaking changes require approval from API maintainers before merging.
`;
            } else {
              comment = `## ✅ No Breaking Changes

The changes in this PR do not introduce breaking changes to the API contract.

The next version can be a **minor** or **patch** version bump.
`;
            }
            
            // Post comment
            await github.rest.issues.createComment({
              issue_number: context.issue.number,
              owner: context.repo.owner,
              repo: context.repo.repo,
              body: comment
            });
      
      - name: Fail if breaking changes and not labeled
        if: steps.breaking.outputs.exit_code == '1'
        run: |
          # Check if PR has 'breaking-change-approved' label
          LABELS=$(gh pr view ${{ github.event.pull_request.number }} --json labels --jq '.labels[].name')
          
          if echo "$LABELS" | grep -q "breaking-change-approved"; then
            echo "Breaking changes approved via label"
            exit 0
          else
            echo "Breaking changes detected but not approved"
            echo "Add 'breaking-change-approved' label to proceed"
            exit 1
          fi
        env:
          GH_TOKEN: ${{ github.token }}
```

### Step 2: Add Label-Based Approval

Create `.github/workflows/require-approval.yml`:

```yaml
name: Require Approval for Breaking Changes

on:
  pull_request:
    types: [labeled, unlabeled, synchronize]

jobs:
  check-approval:
    name: Check Breaking Change Approval
    runs-on: ubuntu-latest
    if: contains(github.event.pull_request.labels.*.name, 'breaking-change')
    
    steps:
      - name: Check for approval label
        run: |
          if echo '${{ toJson(github.event.pull_request.labels.*.name) }}' | grep -q "breaking-change-approved"; then
            echo "Breaking change approved"
            exit 0
          else
            echo "Breaking change not approved"
            echo "Requires 'breaking-change-approved' label from maintainer"
            exit 1
          fi
```

### Step 3: Add Required Reviewers

In GitHub repository settings:
1. Go to **Settings** → **Branches**
2. Add branch protection rule for `main`
3. Enable "Require pull request reviews before merging"
4. Set number of required approvals to 2 for breaking changes
5. Enable "Require status checks to pass"
6. Select "Check API Contracts" as required check

## Part 2: Automated Publishing on Main

### Step 4: Create Publish Workflow

Create `.github/workflows/publish-contracts.yml`:

```yaml
name: Publish API Contracts

on:
  push:
    branches: [main]
    paths:
      - 'specs/**'
      - '.github/workflows/publish-contracts.yml'

jobs:
  publish:
    name: Publish Contract Packages
    runs-on: ubuntu-latest
    permissions:
      contents: write
      packages: write
    
    steps:
      - name: Checkout code
        uses: actions/checkout@v4
        with:
          fetch-depth: 0  # Need for version calculation
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      
      - name: Install ConcordIO
        run: dotnet tool install --global ConcordIO.Tool
      
      - name: Install GitVersion
        uses: gittools/actions/gitversion/setup@v0
        with:
          versionSpec: '5.x'
      
      - name: Determine version
        id: version
        uses: gittools/actions/gitversion/execute@v0
        with:
          useConfigFile: true
          configFilePath: GitVersion.yml
      
      - name: Generate contract packages
        run: |
          concordio pack \
            --spec specs/api.yaml \
            --package-id Contoso.BookStore.Api \
            --version ${{ steps.version.outputs.semVer }} \
            --authors "BookStore Team" \
            --description "Books API contract for Contoso BookStore" \
            --package-properties "RepositoryUrl=https://github.com/${{ github.repository }}" \
            --package-properties "CommitHash=${{ github.sha }}"
      
      - name: Publish to GitHub Packages
        run: |
          dotnet nuget push *.nupkg \
            --source https://nuget.pkg.github.com/${{ github.repository_owner }}/index.json \
            --api-key ${{ secrets.GITHUB_TOKEN }} \
            --skip-duplicate
      
      - name: Create GitHub Release
        uses: actions/create-release@v1
        env:
          GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
        with:
          tag_name: v${{ steps.version.outputs.semVer }}
          release_name: v${{ steps.version.outputs.semVer }}
          body: |
            ## Contoso.BookStore.Api v${{ steps.version.outputs.semVer }}
            
            ### Packages Published
            - Contoso.BookStore.Api.${{ steps.version.outputs.semVer }}.nupkg
            - Contoso.BookStore.Api.Client.${{ steps.version.outputs.semVer }}.nupkg
            
            ### Installation
            ```bash
            dotnet add package Contoso.BookStore.Api.Client --version ${{ steps.version.outputs.semVer }}
            ```
            
            [View on GitHub Packages](https://github.com/${{ github.repository }}/packages)
          draft: false
          prerelease: ${{ steps.version.outputs.preReleaseTag != '' }}
      
      - name: Upload packages as artifacts
        uses: actions/upload-artifact@v4
        with:
          name: nuget-packages
          path: '*.nupkg'
```

### Step 5: Configure GitVersion

Create `GitVersion.yml` in repository root:

```yaml
mode: ContinuousDeployment
branches:
  main:
    regex: ^main$
    mode: ContinuousDeployment
    tag: ''
    increment: Patch
    prevent-increment-of-merged-branch-version: true
    track-merge-target: false
    source-branches: ['feature', 'hotfix']
  
  feature:
    regex: ^features?[/-]
    mode: ContinuousDeployment
    tag: preview
    increment: Minor
    source-branches: ['main']
  
  hotfix:
    regex: ^hotfix(es)?[/-]
    mode: ContinuousDeployment
    tag: beta
    increment: Patch
    source-branches: ['main']

major-version-bump-message: '\+semver:\s?(breaking|major)'
minor-version-bump-message: '\+semver:\s?(feature|minor)'
patch-version-bump-message: '\+semver:\s?(fix|patch)'
```

### Step 6: Use Conventional Commits

Update commit messages to indicate version bumps:

```bash
# Patch version bump
git commit -m "fix: correct validation for ISBN format"

# Minor version bump
git commit -m "feat: add book rating endpoint"

# Major version bump (breaking change)
git commit -m "feat!: remove deprecated author field

BREAKING CHANGE: The 'author' field has been removed from the Book schema.
Use 'authors' (array) instead."
```

## Part 3: Multi-Environment Pipeline

### Step 7: Deploy to Development First

Create `.github/workflows/deploy-contracts.yml`:

```yaml
name: Deploy API Contracts

on:
  push:
    branches: [main]
    paths:
      - 'specs/**'

jobs:
  deploy-dev:
    name: Deploy to Development
    runs-on: ubuntu-latest
    environment:
      name: development
      url: https://dev-nuget.example.com
    
    steps:
      - name: Checkout code
        uses: actions/checkout@v4
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      
      - name: Install ConcordIO
        run: dotnet tool install --global ConcordIO.Tool
      
      - name: Generate packages
        run: |
          concordio pack \
            --spec specs/api.yaml \
            --package-id Contoso.BookStore.Api \
            --version 1.0.0-dev.${{ github.run_number }} \
            --authors "BookStore Team"
      
      - name: Publish to Dev feed
        run: |
          dotnet nuget push *.nupkg \
            --source ${{ secrets.DEV_NUGET_FEED }} \
            --api-key ${{ secrets.DEV_NUGET_API_KEY }}
  
  deploy-staging:
    name: Deploy to Staging
    runs-on: ubuntu-latest
    needs: deploy-dev
    environment:
      name: staging
      url: https://staging-nuget.example.com
    
    steps:
      - name: Checkout code
        uses: actions/checkout@v4
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      
      - name: Install ConcordIO
        run: dotnet tool install --global ConcordIO.Tool
      
      - name: Generate packages
        run: |
          concordio pack \
            --spec specs/api.yaml \
            --package-id Contoso.BookStore.Api \
            --version 1.0.0-rc.${{ github.run_number }} \
            --authors "BookStore Team"
      
      - name: Publish to Staging feed
        run: |
          dotnet nuget push *.nupkg \
            --source ${{ secrets.STAGING_NUGET_FEED }} \
            --api-key ${{ secrets.STAGING_NUGET_API_KEY }}
  
  deploy-production:
    name: Deploy to Production
    runs-on: ubuntu-latest
    needs: deploy-staging
    environment:
      name: production
      url: https://nuget.example.com
    
    steps:
      - name: Checkout code
        uses: actions/checkout@v4
        with:
          fetch-depth: 0
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      
      - name: Install ConcordIO
        run: dotnet tool install --global ConcordIO.Tool
      
      - name: Determine version
        id: version
        uses: gittools/actions/gitversion/execute@v0
      
      - name: Generate packages
        run: |
          concordio pack \
            --spec specs/api.yaml \
            --package-id Contoso.BookStore.Api \
            --version ${{ steps.version.outputs.semVer }} \
            --authors "BookStore Team"
      
      - name: Publish to Production feed
        run: |
          dotnet nuget push *.nupkg \
            --source https://api.nuget.org/v3/index.json \
            --api-key ${{ secrets.NUGET_ORG_API_KEY }}
```

### Step 8: Configure Environment Protection

In GitHub repository settings:
1. Go to **Settings** → **Environments**
2. Create environments: `development`, `staging`, `production`
3. For `production`:
   - Enable "Required reviewers" (select team/users)
   - Add wait timer (e.g., 10 minutes)
   - Enable "Prevent self-review"

## Part 4: Azure DevOps Pipeline

For Azure DevOps, create `azure-pipelines.yml`:

```yaml
trigger:
  branches:
    include:
      - main
  paths:
    include:
      - specs/**

pr:
  branches:
    include:
      - main
  paths:
    include:
      - specs/**

pool:
  vmImage: 'ubuntu-latest'

variables:
  packageId: 'Contoso.BookStore.Api'
  specPath: 'specs/api.yaml'

stages:
  - stage: CheckPR
    condition: eq(variables['Build.Reason'], 'PullRequest')
    displayName: 'Check Breaking Changes'
    jobs:
      - job: BreakingChanges
        displayName: 'Detect Breaking Changes'
        steps:
          - task: UseDotNet@2
            inputs:
              version: '10.0.x'
          
          - script: |
              dotnet tool install --global ConcordIO.Tool
            displayName: 'Install ConcordIO'
          
          - script: |
              concordio breaking \
                --spec $(specPath) \
                --package-id $(packageId) \
                > breaking-output.txt 2>&1
              
              EXIT_CODE=$?
              echo "##vso[task.setvariable variable=BreakingExitCode]$EXIT_CODE"
              
              if [ $EXIT_CODE -eq 1 ]; then
                echo "##vso[task.logissue type=warning]Breaking changes detected"
                cat breaking-output.txt
                exit 1
              fi
            displayName: 'Check for Breaking Changes'
            continueOnError: true
          
          - task: PublishBuildArtifacts@1
            inputs:
              pathToPublish: 'breaking-output.txt'
              artifactName: 'BreakingChangesReport'

  - stage: Publish
    condition: and(succeeded(), eq(variables['Build.SourceBranch'], 'refs/heads/main'))
    displayName: 'Publish Packages'
    jobs:
      - job: PublishNuGet
        displayName: 'Publish to NuGet Feed'
        steps:
          - task: UseDotNet@2
            inputs:
              version: '10.0.x'
          
          - task: GitVersion@5
            inputs:
              runtime: 'core'
              configFilePath: 'GitVersion.yml'
          
          - script: |
              dotnet tool install --global ConcordIO.Tool
            displayName: 'Install ConcordIO'
          
          - script: |
              concordio pack \
                --spec $(specPath) \
                --package-id $(packageId) \
                --version $(GitVersion.SemVer) \
                --authors "BookStore Team"
            displayName: 'Generate Packages'
          
          - task: NuGetCommand@2
            inputs:
              command: 'push'
              packagesToPush: '*.nupkg'
              nuGetFeedType: 'internal'
              publishVstsFeed: 'YourFeedId'
```

## Part 5: Slack/Teams Notifications

### Step 9: Add Slack Notifications

Update `.github/workflows/publish-contracts.yml`:

```yaml
      - name: Notify Slack on Success
        if: success()
        uses: slackapi/slack-github-action@v1
        with:
          payload: |
            {
              "text": "✅ Contract Published: Contoso.BookStore.Api v${{ steps.version.outputs.semVer }}",
              "blocks": [
                {
                  "type": "section",
                  "text": {
                    "type": "mrkdwn",
                    "text": "*Contract Published*\n\nPackage: Contoso.BookStore.Api\nVersion: ${{ steps.version.outputs.semVer }}\n\n<https://github.com/${{ github.repository }}/releases/tag/v${{ steps.version.outputs.semVer }}|View Release>"
                  }
                }
              ]
            }
        env:
          SLACK_WEBHOOK_URL: ${{ secrets.SLACK_WEBHOOK_URL }}
      
      - name: Notify Slack on Failure
        if: failure()
        uses: slackapi/slack-github-action@v1
        with:
          payload: |
            {
              "text": "❌ Contract Publish Failed for Contoso.BookStore.Api"
            }
        env:
          SLACK_WEBHOOK_URL: ${{ secrets.SLACK_WEBHOOK_URL }}
```

## Best Practices

### 1. Use Separate Feeds per Environment

```bash
# Development
dotnet nuget push *.nupkg --source https://dev-feed.example.com

# Staging
dotnet nuget push *.nupkg --source https://staging-feed.example.com

# Production
dotnet nuget push *.nupkg --source https://api.nuget.org/v3/index.json
```

### 2. Pin Dependencies in CI

```yaml
- name: Install ConcordIO
  run: dotnet tool install --global ConcordIO.Tool --version 0.8.0  # Pin version
```

### 3. Cache NuGet Packages

```yaml
- name: Cache NuGet packages
  uses: actions/cache@v4
  with:
    path: ~/.nuget/packages
    key: ${{ runner.os }}-nuget-${{ hashFiles('**/*.csproj') }}
    restore-keys: |
      ${{ runner.os }}-nuget-
```

### 4. Validate Specs Before Publishing

```yaml
- name: Validate OpenAPI Spec
  run: npx @stoplight/spectral-cli lint specs/api.yaml
```

### 5. Generate Changelog Automatically

```yaml
- name: Generate Changelog
  uses: conventional-changelog/standard-version@v9
```

## Troubleshooting

### Issue: "nuget: command not found" in CI

**Solution**: Install NuGet CLI in pipeline:

```yaml
- name: Install NuGet
  run: |
    curl -o nuget.exe https://dist.nuget.org/win-x86-commandline/latest/nuget.exe
    sudo mv nuget.exe /usr/local/bin/nuget
    sudo chmod +x /usr/local/bin/nuget
```

### Issue: Authentication Failures

**Solution**: Configure feed credentials:

```yaml
- name: Authenticate to feed
  run: |
    dotnet nuget add source https://feed.example.com/nuget \
      --name myfeed \
      --username ${{ secrets.NUGET_USERNAME }} \
      --password ${{ secrets.NUGET_PASSWORD }} \
      --store-password-in-clear-text
```

### Issue: Version Conflicts

**Solution**: Use `--skip-duplicate` flag:

```yaml
- name: Publish packages
  run: |
    dotnet nuget push *.nupkg \
      --source https://api.nuget.org/v3/index.json \
      --api-key ${{ secrets.NUGET_API_KEY }} \
      --skip-duplicate
```

## Summary

You've learned how to:
- ✅ Detect breaking changes in PRs automatically
- ✅ Block merges when breaking changes are detected
- ✅ Automate package publishing on main
- ✅ Implement semantic versioning with GitVersion
- ✅ Set up multi-environment deployments
- ✅ Add notifications to team chat

## Next Steps

- [Package Versioning Strategy](../advanced/versioning-strategy.md) - Deep dive into versioning
- [Multi-Team Workflows](../advanced/multi-team.md) - Enterprise setups
- [Security Best Practices](../advanced/security.md) - Secure your pipelines

**Congratulations!** Your contract management is now fully automated!
