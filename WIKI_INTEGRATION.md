# GitHub Wiki Integration - Summary

This document summarizes the GitHub Wiki integration for ConcordIO documentation.

## What Was Added

### 1. Documentation Setup Guide
**File**: `docs/WIKI_SETUP.md`

Comprehensive guide covering:
- What is GitHub Wiki and how it relates to repository docs
- Three setup options (Manual, Script, GitHub Actions)
- Step-by-step instructions for each option
- Troubleshooting common issues
- Best practices for maintenance

### 2. Sync Script
**File**: `scripts/sync-to-wiki.sh`

Automated bash script that:
- Clones wiki repository if needed
- Copies all documentation from `docs/` to wiki
- Converts `README.md` to `Home.md` for wiki home page
- Converts markdown links to wiki format (removes `.md` extensions)
- Converts links to `src/` files to point to main repository
- Creates `_Sidebar.md` for wiki navigation
- Creates `_Footer.md` for wiki footer
- Commits and pushes changes to wiki repository

**Usage**: `./scripts/sync-to-wiki.sh`

### 3. GitHub Actions Workflow
**File**: `.github/workflows/sync-wiki.yml`

Automated workflow that:
- Triggers on pushes to `main` branch that modify `docs/**`
- Can be manually triggered from Actions tab
- Performs same operations as sync script
- Automatically keeps wiki in sync with repository

### 4. Maintenance Guide
**File**: `docs/WIKI_MAINTENANCE.md`

Quick reference for maintainers:
- How the sync works
- Manual sync instructions
- Troubleshooting tips
- Best practices
- Rollback procedures

### 5. README Updates
**Files**: `README.md`, `docs/README.md`

- Added link to GitHub Wiki
- Added note about automatic sync
- Clarified that docs are available in both locations

## How It Works

```
┌─────────────────────────────────────────┐
│  Developer Updates docs/ in Repository  │
└─────────────────┬───────────────────────┘
                  │
                  ▼
┌─────────────────────────────────────────┐
│   Commits and Pushes to main branch     │
└─────────────────┬───────────────────────┘
                  │
                  ▼
┌─────────────────────────────────────────┐
│  GitHub Actions Workflow Triggers       │
│  (.github/workflows/sync-wiki.yml)      │
└─────────────────┬───────────────────────┘
                  │
                  ▼
┌─────────────────────────────────────────┐
│  Sync Process:                          │
│  1. Clone wiki repository               │
│  2. Copy docs/ to wiki                  │
│  3. Convert links for wiki format       │
│  4. Create navigation (_Sidebar.md)     │
│  5. Commit and push to wiki             │
└─────────────────┬───────────────────────┘
                  │
                  ▼
┌─────────────────────────────────────────┐
│  Documentation Available on Wiki        │
│  https://github.com/LevDevIO/          │
│         ConcordIO/wiki                  │
└─────────────────────────────────────────┘
```

## Key Features

### Automatic Sync
- No manual intervention needed
- Docs stay in sync automatically
- Triggered by changes to `docs/` folder

### Link Conversion
- Repository links: `[text](./path/file.md)`
- Wiki links: `[text](path/file)`
- External links preserved
- Links to `src/` converted to GitHub URLs

### Navigation
- Sidebar with categorized links
- Footer with quick links to repo, issues, releases
- Home page from `docs/README.md`

### Flexibility
- Can sync manually with script
- Can trigger GitHub Actions manually
- Can disable auto-sync if needed

## Benefits

1. **Easier Discovery**: Wiki is more prominent than `docs/` folder
2. **Better Navigation**: Wiki has built-in navigation features
3. **Dual Access**: Available both in repo and wiki
4. **Version Control**: Docs still versioned in main repo
5. **Automatic Updates**: No manual wiki maintenance needed

## Next Steps

To enable the wiki integration:

1. **Enable Wiki on GitHub**:
   - Go to repository Settings
   - Enable "Wikis" under Features
   - Save changes

2. **Run Initial Sync**:
   - Option A: Trigger GitHub Actions workflow manually
   - Option B: Run `./scripts/sync-to-wiki.sh` locally

3. **Verify**:
   - Visit https://github.com/LevDevIO/ConcordIO/wiki
   - Check that all documentation appears correctly
   - Test navigation links

4. **Future Updates**:
   - Just commit changes to `docs/` folder
   - Wiki updates automatically

## Documentation

For detailed information, see:
- [WIKI_SETUP.md](docs/WIKI_SETUP.md) - Complete setup guide
- [WIKI_MAINTENANCE.md](docs/WIKI_MAINTENANCE.md) - Maintenance reference

## Questions?

- Check the setup guide: `docs/WIKI_SETUP.md`
- Open an issue on GitHub
- Contact repository maintainers
