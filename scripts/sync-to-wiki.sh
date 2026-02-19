#!/bin/bash
set -e

# ConcordIO Documentation to Wiki Sync Script
# This script syncs documentation from docs/ folder to GitHub Wiki

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
DOCS_DIR="$REPO_ROOT/docs"
WIKI_DIR="${WIKI_DIR:-$REPO_ROOT/../ConcordIO.wiki}"
PYTHON_BIN="${PYTHON_BIN:-}"

echo "========================================="
echo "ConcordIO Wiki Sync Script"
echo "========================================="
echo

# Check if docs directory exists
if [ ! -d "$DOCS_DIR" ]; then
    echo "❌ Error: docs directory not found at $DOCS_DIR"
    exit 1
fi

# Resolve Python executable before modifying the wiki directory
if [ -z "$PYTHON_BIN" ]; then
    if command -v python3 >/dev/null 2>&1; then
        PYTHON_BIN="python3"
    elif command -v python >/dev/null 2>&1; then
        PYTHON_BIN="python"
    else
        echo "❌ Error: Python is required but was not found (python3 or python)"
        exit 1
    fi
fi

# Check if wiki repository is cloned
if [ ! -d "$WIKI_DIR" ]; then
    echo "📥 Wiki repository not found. Cloning..."
    mkdir -p "$(dirname "$WIKI_DIR")"
    cd "$(dirname "$WIKI_DIR")"
    git clone https://github.com/LevDevIO/ConcordIO.wiki.git "$(basename "$WIKI_DIR")"
    
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

# Flatten documentation files to wiki root and convert links
# GitHub Wiki does not support subdirectory pages; all pages must be at the root.
# Subdirectory paths are flattened: getting-started/quick-start.md -> getting-started-quick-start.md
echo "📋 Flattening and copying documentation files with link conversion..."
"$PYTHON_BIN" - "$DOCS_DIR" "$WIKI_DIR" << 'PYEOF'
import os
import re
import sys

docs_dir = os.path.abspath(sys.argv[1])
wiki_dir = os.path.abspath(sys.argv[2])
repo_root = os.path.dirname(docs_dir)
github_repo = 'LevDevIO/ConcordIO'

# Build mapping: abs path -> wiki page name (without .md extension)
path_to_wiki = {}
for root, dirs, files in os.walk(docs_dir):
    for fname in files:
        if fname.endswith('.md'):
            abs_path = os.path.abspath(os.path.join(root, fname))
            rel = os.path.relpath(abs_path, docs_dir)
            # Flatten subdirectory separators with hyphens
            wiki_name = rel.replace(os.sep, '-').replace('/', '-')[:-3]
            path_to_wiki[abs_path] = wiki_name


def convert_link(src_abs, link):
    """Resolve a relative markdown link and return its wiki-format target."""
    if link.startswith(('http://', 'https://', '#', 'mailto:')):
        return link
    anchor = ''
    if '#' in link:
        link, anchor = link.split('#', 1)
        anchor = '#' + anchor
    if not link:
        return link + anchor
    candidate = link
    if not link.endswith('.md') and '.' not in os.path.basename(link):
        candidate = link + '.md'
    if not candidate.endswith('.md'):
        return link + anchor
    src_dir = os.path.dirname(src_abs)
    abs_target = os.path.abspath(os.path.join(src_dir, candidate))
    # Link points into the docs tree: use flattened wiki page name
    if abs_target in path_to_wiki:
        return path_to_wiki[abs_target] + anchor
    # Link points elsewhere in the repo: convert to GitHub blob URL
    if abs_target.startswith(repo_root + os.sep) or abs_target == repo_root:
        github_rel = os.path.relpath(abs_target, repo_root).replace(os.sep, '/')
        return f'https://github.com/{github_repo}/blob/main/{github_rel}{anchor}'
    return link + anchor


for abs_path, wiki_name in sorted(path_to_wiki.items()):
    with open(abs_path, 'r', encoding='utf-8') as f:
        content = f.read()

    def replace_match(m, _src=abs_path):
        return '[' + m.group(1) + '](' + convert_link(_src, m.group(2)) + ')'

    content = re.sub(r'\[([^\]]*)\]\(([^)]+)\)', replace_match, content)
    dest = os.path.join(wiki_dir, wiki_name + '.md')
    with open(dest, 'w', encoding='utf-8') as f:
        f.write(content)
    print(f'  Wrote: {wiki_name}.md')

print(f'Processed {len(path_to_wiki)} files')

wiki_pages = set(path_to_wiki.values()) | {'Home', '_Sidebar', '_Footer'}
broken_links = []
link_pattern = re.compile(r'\[([^\]]*)\]\(([^)]+)\)')

def is_external(target):
    return target.startswith(('http://', 'https://', 'mailto:', '#'))

def has_extension(target):
    return '.' in os.path.basename(target)

for abs_path, wiki_name in sorted(path_to_wiki.items()):
    dest = os.path.join(wiki_dir, wiki_name + '.md')
    with open(dest, 'r', encoding='utf-8') as f:
        content = f.read()
    for match in link_pattern.finditer(content):
        target = match.group(2)
        if '#' in target:
            target = target.split('#', 1)[0]
        if not target or is_external(target) or has_extension(target):
            continue
        if target not in wiki_pages:
            broken_links.append((wiki_name, match.group(2)))

if broken_links:
    print('❌ Broken wiki links detected:')
    for page, link in broken_links:
        print(f'  {page}.md -> {link}')
    raise SystemExit(1)
PYEOF

# Create Home.md from the flattened README
echo "🏠 Creating Home page..."
if [ -f "README.md" ]; then
    cp README.md Home.md
    echo "✅ Home.md created from README.md"
fi

# Create a _Sidebar.md for better navigation
echo "📑 Creating sidebar..."
cat > _Sidebar.md << 'EOF'
## ConcordIO Documentation

### Getting Started
* [Quick Start](getting-started-quick-start)
* [Installation](getting-started-installation)
* [When to Use](getting-started-when-to-use)
* [Core Concepts](getting-started-concepts)

### Tutorials
* [Publishing First Contract](tutorials-publishing-first-contract)
* [Consuming Contract](tutorials-consuming-contract)
* [CI/CD Setup](tutorials-cicd-setup)

### Examples
* [Example Projects](examples-README)

### AI & Automation
* [AI Prompts](ai-prompts-README)

### Troubleshooting
* [FAQ](troubleshooting-faq)
* [Common Issues](troubleshooting-common-issues)
* [Known Limitations](troubleshooting-known-limitations)

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
