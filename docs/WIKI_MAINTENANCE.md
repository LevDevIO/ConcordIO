# Wiki Maintenance Guide

Quick reference for maintaining the ConcordIO GitHub Wiki.

## Overview

The ConcordIO documentation is maintained in the `docs/` directory of the main repository and automatically synced to GitHub Wiki for easier browsing.

## How It Works

1. **Primary Source**: Documentation lives in `docs/` folder (versioned with code)
2. **Wiki Mirror**: GitHub Wiki is automatically synced via GitHub Actions
3. **Sync Trigger**: Any push to `main` branch that modifies `docs/**`

## Manual Sync

If you need to manually sync documentation to the wiki:

### Option 1: Run Script Locally

```bash
# From repository root
./scripts/sync-to-wiki.sh
```

**Prerequisites:**
- Wiki must be enabled on GitHub
- You must have write access to the repository
- Wiki repository must be cloned at `../ConcordIO.wiki`

### Option 2: Trigger GitHub Actions

1. Go to repository Actions tab
2. Select "Sync Documentation to Wiki" workflow
3. Click "Run workflow"
4. Select branch: `main`
5. Click "Run workflow" button

## First-Time Setup

If the wiki has never been synced before:

1. **Enable Wiki on GitHub:**
   - Go to repository Settings
   - Under "Features", enable "Wikis"
   - Save changes

2. **Run Initial Sync:**
   ```bash
   cd /path/to/ConcordIO
   ./scripts/sync-to-wiki.sh
   ```

3. **Verify:**
   - Visit: https://github.com/LevDevIO/ConcordIO/wiki
   - You should see the Home page and all documentation

## Updating Documentation

### Standard Workflow

1. Edit documentation in `docs/` folder
2. Commit changes to a feature branch
3. Create PR and merge to `main`
4. Wiki syncs automatically via GitHub Actions

### What Gets Synced

- All markdown files in `docs/`
- Directory structure is preserved
- `docs/README.md` becomes wiki `Home.md`
- Links are automatically converted for wiki format

### What Doesn't Get Synced

- Files outside `docs/` directory
- Binary files (images should be in docs folder)
- Hidden files (starting with `.`)

## Link Conversion

The sync process automatically converts links:

**Repository Format:**
```markdown
[Quick Start](./getting-started/quick-start.md)
[CLI Guide](../../src/ConcordIO.Tool/README.md)
```

**Wiki Format:**
```markdown
[Quick Start](getting-started/quick-start)
[CLI Guide](https://github.com/LevDevIO/ConcordIO/tree/main/src/ConcordIO.Tool/README)
```

## Troubleshooting

### Wiki Not Updating

**Check:**
1. Is the GitHub Actions workflow running? (Actions tab)
2. Did it complete successfully?
3. Are there any error messages in the workflow log?

**Common Causes:**
- Wiki not enabled in repository settings
- Workflow permissions not set correctly
- Network/GitHub issues

**Solution:**
- Run manual sync: `./scripts/sync-to-wiki.sh`
- Check workflow logs for errors
- Verify wiki is enabled in settings

### Links Broken on Wiki

**Cause:** Link conversion may have failed

**Solution:**
1. Check the sync script's link conversion logic
2. Test locally: `./scripts/sync-to-wiki.sh`
3. Verify links manually on wiki

### Permission Denied

**Cause:** No write access to wiki repository

**Solution:**
- Ensure you have write access to the main repository
- Wiki access follows main repository permissions
- Try running with correct credentials

## Wiki Structure

After sync, the wiki should have:

```
Home.md                     (from docs/README.md)
_Sidebar.md                 (navigation)
_Footer.md                  (footer links)
getting-started/
  ├── quick-start.md
  ├── installation.md
  ├── when-to-use.md
  └── concepts.md
tutorials/
  ├── publishing-first-contract.md
  ├── consuming-contract.md
  └── cicd-setup.md
troubleshooting/
  ├── faq.md
  ├── common-issues.md
  └── known-limitations.md
examples/
  └── README.md
ai-prompts/
  └── README.md
```

## Best Practices

1. **Single Source of Truth**: Always edit in `docs/`, never directly in wiki
2. **Test Locally**: Run sync script locally before pushing large changes
3. **Check Wiki**: After sync, verify important pages on wiki
4. **Link Carefully**: Use relative links that work both in repo and wiki
5. **Images**: Store images in `docs/` folder for proper sync

## Rollback

If you need to rollback wiki changes:

```bash
cd ../ConcordIO.wiki
git log  # Find commit to revert to
git reset --hard <commit-hash>
git push --force origin master
```

**Warning:** Force push to wiki - use with caution!

## Contact

For issues with wiki sync:
1. Check [WIKI_SETUP.md](./WIKI_SETUP.md) for detailed setup
2. Open an issue on GitHub
3. Contact repository maintainers

## Related Files

- `docs/WIKI_SETUP.md` - Detailed setup guide
- `scripts/sync-to-wiki.sh` - Sync script
- `.github/workflows/sync-wiki.yml` - GitHub Actions workflow
