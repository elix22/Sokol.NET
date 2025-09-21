#!/bin/bash

# Clear iOS Development Team ID Cache Script
# Usage: clear-ios-cache.sh

CACHE_DIR="$HOME/.sokol-charp-cache"

echo "🧹 Clearing iOS Development Team ID Cache..."
echo ""

if [ -d "$CACHE_DIR" ]; then
    echo "📁 Found cache directory: $CACHE_DIR"

    # List what's being removed
    echo "📋 Cached team IDs:"
    for file in "$CACHE_DIR"/*.teamid; do
        if [ -f "$file" ]; then
            filename=$(basename "$file" .teamid)
            echo "   • $filename"
        fi
    done

    # Remove the cache directory
    rm -rf "$CACHE_DIR"
    echo ""
    echo "✅ Cache cleared successfully!"
    echo "   Team IDs will be prompted again on next iOS build."
else
    echo "ℹ️  No cache directory found at $CACHE_DIR"
    echo "   Nothing to clear."
fi

echo ""
echo "💡 Tip: You can also manually delete cache files:"
echo "   rm -rf $CACHE_DIR"
echo "   or"
echo "   rm $CACHE_DIR/project-name.teamid"