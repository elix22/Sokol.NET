#!/bin/bash
# Add a project to VS Code tasks.json and launch.json configurations.
# Usage: ./add-project-to-vscode-config.sh <project-name>

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

print_error() {
    echo -e "${RED}Error: $1${NC}" >&2
}

print_success() {
    echo -e "${GREEN}$1${NC}"
}

print_info() {
    echo -e "${YELLOW}$1${NC}"
}

# Validate arguments
if [ $# -lt 1 ]; then
    echo "Usage: $0 <project-name>"
    echo ""
    echo "Example:"
    echo "  $0 myapp"
    exit 1
fi

PROJECT_NAME="$1"

# Get script directory and workspace root
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
WORKSPACE_ROOT="$(dirname "$SCRIPT_DIR")"

echo "Adding project '$PROJECT_NAME' to VS Code configuration..."

# Check if Python is available for JSON manipulation
if ! command -v python3 &> /dev/null; then
    print_error "python3 is required but not installed. Please install Python 3."
    exit 1
fi

# Use Python to update the JSON files
python3 "$SCRIPT_DIR/add-project-to-vscode-config.py" "$PROJECT_NAME"
