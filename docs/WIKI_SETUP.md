# GitHub Wiki Setup Guide

This guide explains how to publish the ConcordIO documentation to GitHub Wiki.

## Understanding the Setup

The documentation currently lives in the `docs/` directory of this repository. GitHub Wiki is a separate Git repository that needs to be populated with these files.

**Key Points:**
- Documentation in `docs/` folder: ✅ Versioned with code
- GitHub Wiki: Separate Git repository, easier to browse
- **Both can coexist**: Keep docs in repo for versioning, sync to Wiki for discoverability

## Prerequisites

1. **Enable GitHub Wiki** (if not already enabled):
   - Go to repository Settings → Features
   - Check "Wikis"
   - Save changes

2. **Required tools**:
   - Git command line
   - Access to push to the repository Wiki

## Option 1: Manual Setup (One-Time)

### Step 1: Clone the Wiki Repository

GitHub Wiki is a separate Git repository. Clone it:

```bash
# Clone the wiki (replace with your repo URL)
git clone https://github.com/LevDevIO/ConcordIO.wiki.git

cd ConcordIO.wiki
```

### Step 2: Copy Documentation Files

Copy the documentation from your main repository to the wiki:

```bash
# Assuming you're in the wiki directory and the main repo is at ../ConcordIO

# Copy all documentation
cp -r ../ConcordIO/docs/* .

# The wiki expects a Home.md file as the main page
cp ../ConcordIO/docs/README.md Home.md
```

### Step 3: Adjust Links for Wiki

Wiki uses different link structure. Update links in markdown files:

```bash
# Example: Links to other wiki pages don't need .md extension
# Before: [Quick Start](./getting-started/quick-start.md)
# After:  [Quick Start](getting-started/quick-start)
```

### Step 4: Commit and Push

```bash
git add .
git commit -m "docs: sync documentation to GitHub Wiki"
git push origin master
```

## Option 2: Automated Sync (Recommended)

Use the provided script to automate the sync process.

### One-Time Setup

1. Ensure the Wiki is enabled on GitHub
2. Clone both repositories side-by-side:

```bash
# Main repository
git clone https://github.com/LevDevIO/ConcordIO.git

# Wiki repository
git clone https://github.com/LevDevIO/ConcordIO.wiki.git
```

### Running the Sync Script

From the main repository:

```bash
./scripts/sync-to-wiki.sh
```

This script will:
- Copy all documentation from `docs/` to the wiki
- Convert `README.md` to `Home.md`
- Adjust internal links for Wiki format
- Commit and push changes

## Option 3: GitHub Actions (Continuous Sync)

For automatic syncing on every documentation change, use the provided GitHub Actions workflow.

### Setup

The workflow is already configured in `.github/workflows/sync-wiki.yml`. It will:

1. Trigger on pushes to `main` branch that modify `docs/**`
2. Clone the wiki repository
3. Sync documentation
4. Push changes to wiki

### Manual Trigger

You can also manually trigger the sync:

1. Go to Actions tab on GitHub
2. Select "Sync Documentation to Wiki" workflow
3. Click "Run workflow"

## Wiki Structure

After sync, the wiki will have this structure:

```
Home                                    (docs/README.md)
├── Getting Started
│   ├── Quick Start                    (docs/getting-started/quick-start.md)
│   ├── Installation                   (docs/getting-started/installation.md)
│   ├── When to Use ConcordIO          (docs/getting-started/when-to-use.md)
│   └── Core Concepts                  (docs/getting-started/concepts.md)
├── Tutorials
│   ├── Publishing Your First Contract (docs/tutorials/publishing-first-contract.md)
│   ├── Consuming a Contract           (docs/tutorials/consuming-contract.md)
│   └── CI/CD Setup                    (docs/tutorials/cicd-setup.md)
├── Examples                            (docs/examples/README.md)
├── AI Prompts                          (docs/ai-prompts/README.md)
└── Troubleshooting
    ├── FAQ                             (docs/troubleshooting/faq.md)
    ├── Common Issues                   (docs/troubleshooting/common-issues.md)
    └── Known Limitations               (docs/troubleshooting/known-limitations.md)
```

## Maintaining the Wiki

### When to Update

Update the wiki whenever documentation in `docs/` changes:

1. **Automatically**: If GitHub Actions workflow is enabled
2. **Manually**: Run `./scripts/sync-to-wiki.sh` after documentation changes
3. **On Release**: Sync as part of release process

### Best Practices

1. **Single Source of Truth**: Keep `docs/` in the main repo as the authoritative source
2. **Wiki as Mirror**: Treat wiki as a read-only mirror for easier browsing
3. **Version Documentation**: Major version changes should be noted in wiki
4. **Link Checking**: Verify links work after syncing

## Troubleshooting

### "Permission denied" when pushing to wiki

**Solution**: Ensure you have write access to the repository. Wiki access follows the same permissions as the main repository.

### Links are broken after sync

**Solution**: Wiki links don't include file extensions. Use the provided script which automatically converts links.

### Wiki not appearing on GitHub

**Solution**: 
1. Check that Wiki is enabled in repository settings
2. Ensure at least one page exists (Home.md is required)
3. Push changes to the wiki repository

### How to rollback wiki changes

```bash
cd ConcordIO.wiki
git log  # Find the commit to revert to
git reset --hard <commit-hash>
git push --force origin master
```

## Additional Resources

- [GitHub Wiki Documentation](https://docs.github.com/en/communities/documenting-your-project-with-wikis)
- [Markdown Guide](https://guides.github.com/features/mastering-markdown/)
- [ConcordIO Main Documentation](../README.md)

## Questions?

If you have questions about the wiki setup:
1. Check the [FAQ](troubleshooting/faq.md)
2. Open an issue on GitHub
3. Contact the maintainers
