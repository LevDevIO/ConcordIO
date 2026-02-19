#!/bin/bash
set -e

# ConcordIO Documentation to Wiki Sync Script
# This script syncs documentation from docs/ folder to GitHub Wiki

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
DOCS_DIR="$REPO_ROOT/docs"
WIKI_DIR="$REPO_ROOT/../ConcordIO.wiki"

echo "========================================="
echo "ConcordIO Wiki Sync Script"
echo "========================================="
echo

# Check if docs directory exists
if [ ! -d "$DOCS_DIR" ]; then
    echo "❌ Error: docs directory not found at $DOCS_DIR"
    exit 1
fi

# Check if wiki repository is cloned
if [ ! -d "$WIKI_DIR" ]; then
    echo "📥 Wiki repository not found. Cloning..."
    cd "$REPO_ROOT/.."
    git clone https://github.com/LevDevIO/ConcordIO.wiki.git
    
    if [ ! -d "$WIKI_DIR" ]; then
        echo "❌ Error: Failed to clone wiki repository"
        echo "Please ensure:"
        echo "  1. GitHub Wiki is enabled for the repository"
        echo "  2. You have permission to access the wiki"
        exit 1
    fi
    echo "✅ Wiki repository cloned successfully"
fi

cd "$WIKI_DIR"

# Pull latest changes from wiki
echo "📥 Pulling latest changes from wiki..."
if git ls-remote --exit-code --heads origin master >/dev/null 2>&1; then
    git pull origin master
elif git ls-remote --exit-code --heads origin main >/dev/null 2>&1; then
    git pull origin main
else
    echo "⚠️  Warning: Neither 'master' nor 'main' branch exists on the wiki remote"
    echo "This is normal for a new wiki. Continuing with sync..."
fi

# Clean the wiki directory (except .git)
echo "🧹 Cleaning wiki directory..."
find . -mindepth 1 -maxdepth 1 ! -name '.git' -exec rm -rf {} +

# Copy documentation files
echo "📋 Copying documentation files..."
cp -r "$DOCS_DIR"/* .

# Create Home.md from README.md
echo "🏠 Creating Home page..."
if [ -f "README.md" ]; then
    cp README.md Home.md
    echo "✅ Home.md created from README.md"
fi

# Function to convert markdown links for wiki format
convert_links() {
    local file="$1"
    echo "  Converting links in: $file"
    
    # Detect OS for sed compatibility
    if [[ "$OSTYPE" == "darwin"* ]]; then
        SED_CMD="sed -i ''"
    else
        SED_CMD="sed -i"
    fi
    
    # Convert relative .md links to wiki format (remove .md extension)
    # Handle anchors first: [text](file.md#section) -> [text](file#section)
    $SED_CMD 's|\(\[[^]]*\](\)\.\./\([^)#]*\)\.md#\([^)]*\))|\1\2#\3)|g' "$file"
    $SED_CMD 's|\(\[[^]]*\](\)\./\([^)#]*\)\.md#\([^)]*\))|\1\2#\3)|g' "$file"
    $SED_CMD 's|\(\[[^]]*\](\)\([^/ )#]*\)\.md#\([^)]*\))|\1\2#\3)|g' "$file"
    
    # Then handle links without anchors: [text](./path/file.md) -> [text](path/file)
    $SED_CMD 's|\(\[[^]]*\](\)\.\./\([^)]*\)\.md)|\1\2)|g' "$file"
    $SED_CMD 's|\(\[[^]]*\](\)\./\([^)]*\)\.md)|\1\2)|g' "$file"
    $SED_CMD 's|\(\[[^]]*\](\)\([^/)]*\)\.md)|\1\2)|g' "$file"
    
    # Convert links to files in src/ to point to main repository
    # [text](../../src/...) -> [text](https://github.com/LevDevIO/ConcordIO/tree/main/src/...)
    $SED_CMD 's|\(\[[^]]*\](\)\.\./\.\./src/\([^)]*\))|\1https://github.com/LevDevIO/ConcordIO/tree/main/src/\2)|g' "$file"
    $SED_CMD 's|\(\[[^]]*\](\)\.\./src/\([^)]*\))|\1https://github.com/LevDevIO/ConcordIO/tree/main/src/\2)|g' "$file"
}

# Convert links in all markdown files
echo "🔗 Converting links for wiki format..."
find . -name "*.md" -type f | while read -r file; do
    convert_links "$file"
done

# Create a _Sidebar.md for better navigation
echo "📑 Creating sidebar..."
cat > _Sidebar.md << 'EOF'
## ConcordIO Documentation

### Getting Started
* [Quick Start](getting-started/quick-start)
* [Installation](getting-started/installation)
* [When to Use](getting-started/when-to-use)
* [Core Concepts](getting-started/concepts)

### Tutorials
* [Publishing First Contract](tutorials/publishing-first-contract)
* [Consuming Contract](tutorials/consuming-contract)
* [CI/CD Setup](tutorials/cicd-setup)

### Examples
* [Example Projects](examples/README)

### AI & Automation
* [AI Prompts](ai-prompts/README)

### Troubleshooting
* [FAQ](troubleshooting/faq)
* [Common Issues](troubleshooting/common-issues)
* [Known Limitations](troubleshooting/known-limitations)

### Resources
* [Main Repository](https://github.com/LevDevIO/ConcordIO)
* [Contributing](https://github.com/LevDevIO/ConcordIO/blob/main/CONTRIBUTING.md)
EOF

echo "✅ Sidebar created"

# Create _Footer.md
echo "📄 Creating footer..."
cat > _Footer.md << 'EOF'
---
**ConcordIO** | [GitHub](https://github.com/LevDevIO/ConcordIO) | [Issues](https://github.com/LevDevIO/ConcordIO/issues) | [Releases](https://github.com/LevDevIO/ConcordIO/releases)
EOF

echo "✅ Footer created"

# Check if there are changes to commit
if [ -z "$(git status --porcelain)" ]; then
    echo "✅ No changes detected. Wiki is already up to date."
    exit 0
fi

# Show what changed
echo
echo "📝 Changes to be committed:"
git status --short

# Commit changes
echo
echo "💾 Committing changes..."
git add .
git commit -m "docs: sync documentation from main repository

Updated: $(date +"%Y-%m-%d %H:%M:%S")
Source: ConcordIO/docs/"

# Push to wiki
echo "⬆️  Pushing to GitHub Wiki..."
git push origin master || git push origin main

echo
echo "========================================="
echo "✅ Wiki sync completed successfully!"
echo "========================================="
echo
echo "View the wiki at: https://github.com/LevDevIO/ConcordIO/wiki"
echo
