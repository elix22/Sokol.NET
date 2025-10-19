#!/bin/bash
# Create a new Sokol C# project from the 'clear' template.
# Usage: ./create-project-from-template.sh <project-name> [target-folder]

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
    echo "Usage: $0 <project-name> [target-folder]"
    echo ""
    echo "Examples:"
    echo "  $0 myapp"
    echo "  $0 myapp /path/to/projects"
    exit 1
fi

PROJECT_NAME="$1"
TARGET_FOLDER="$2"
TEMPLATE_NAME="clear"

# Validate project name
if ! [[ "$PROJECT_NAME" =~ ^[a-zA-Z][a-zA-Z0-9_-]*$ ]]; then
    print_error "Project name must start with a letter and contain only letters, numbers, hyphens, and underscores"
    exit 1
fi

# Get script directory and workspace root
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
WORKSPACE_ROOT="$(dirname "$SCRIPT_DIR")"

# Template source
TEMPLATE_DIR="$WORKSPACE_ROOT/examples/$TEMPLATE_NAME"

# Validate template exists
if [ ! -d "$TEMPLATE_DIR" ]; then
    print_error "Template '$TEMPLATE_NAME' not found at $TEMPLATE_DIR"
    exit 1
fi

# Determine target directory
if [ -n "$TARGET_FOLDER" ]; then
    TARGET_DIR="$TARGET_FOLDER/$PROJECT_NAME"
else
    TARGET_DIR="$WORKSPACE_ROOT/examples/$PROJECT_NAME"
fi

# Check if target already exists
if [ -d "$TARGET_DIR" ]; then
    print_error "Target directory already exists: $TARGET_DIR"
    read -p "Do you want to overwrite it? (yes/no): " response
    if [[ ! "$response" =~ ^[Yy]([Ee][Ss])?$ ]]; then
        echo "Aborted."
        exit 0
    fi
    rm -rf "$TARGET_DIR"
fi

echo "Creating new project '$PROJECT_NAME'..."
echo "  Template: $TEMPLATE_DIR"
echo "  Target:   $TARGET_DIR"

# Copy template to target (exclude build artifacts)
rsync -a --exclude='bin' --exclude='obj' --exclude='.DS_Store' --exclude='*.user' \
    "$TEMPLATE_DIR/" "$TARGET_DIR/"

print_success "  ✓ Copied template files"

# Function to update file content
update_file_content() {
    local file="$1"
    
    # Use different sed syntax for macOS vs Linux
    if [[ "$OSTYPE" == "darwin"* ]]; then
        # macOS
        sed -i '' "s/${TEMPLATE_NAME}/${PROJECT_NAME}/g" "$file"
        sed -i '' "s/${TEMPLATE_NAME^}/${PROJECT_NAME^}/g" "$file"
        sed -i '' "s/${TEMPLATE_NAME^^}/${PROJECT_NAME^^}/g" "$file"
    else
        # Linux
        sed -i "s/${TEMPLATE_NAME}/${PROJECT_NAME}/g" "$file"
        sed -i "s/${TEMPLATE_NAME^}/${PROJECT_NAME^}/g" "$file"
        sed -i "s/${TEMPLATE_NAME^^}/${PROJECT_NAME^^}/g" "$file"
    fi
}

# Update file contents
echo "  Updating file contents..."
find "$TARGET_DIR" -type f \
    \( -name "*.cs" -o -name "*.csproj" -o -name "*.json" -o -name "*.md" \
    -o -name "*.txt" -o -name "*.xml" -o -name "*.props" -o -name "*.targets" \) \
    -not -path "*/bin/*" -not -path "*/obj/*" | while read -r file; do
    update_file_content "$file"
done

# Rename files
echo "  Renaming files..."
find "$TARGET_DIR" -depth -name "*${TEMPLATE_NAME}*" \
    -not -path "*/bin/*" -not -path "*/obj/*" | while read -r old_path; do
    dir=$(dirname "$old_path")
    filename=$(basename "$old_path")
    new_filename="${filename//${TEMPLATE_NAME}/${PROJECT_NAME}}"
    new_filename="${new_filename//${TEMPLATE_NAME^}/${PROJECT_NAME^}}"
    new_filename="${new_filename//${TEMPLATE_NAME^^}/${PROJECT_NAME^^}}"
    
    if [ "$filename" != "$new_filename" ]; then
        new_path="$dir/$new_filename"
        mv "$old_path" "$new_path"
        echo "    Renamed: $filename -> $new_filename"
    fi
done

print_success "\n✓ Project '$PROJECT_NAME' created successfully at $TARGET_DIR"

# Calculate relative path for display
if [[ "$TARGET_DIR" == "$WORKSPACE_ROOT"* ]]; then
    REL_PATH="${TARGET_DIR#$WORKSPACE_ROOT/}"
else
    REL_PATH="$TARGET_DIR"
fi

echo ""
print_info "Next steps:"
echo "  1. Add to VS Code configuration:"
echo "     ./scripts/add-project-to-vscode-config.sh $PROJECT_NAME"
echo ""
echo "  2. Build the project:"
echo "     dotnet build $REL_PATH/${PROJECT_NAME}.csproj"
echo ""
echo "  3. Run the project:"
echo "     dotnet run --project $REL_PATH/${PROJECT_NAME}.csproj"
echo ""
echo "  4. Customize your app in Source/${PROJECT_NAME}-app.cs"
