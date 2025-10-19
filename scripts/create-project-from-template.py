#!/usr/bin/env python3
"""
Create a new Sokol C# project from the 'clear' template.
Usage: python create-project-from-template.py <project-name> [target-folder]
"""

import os
import sys
import shutil
import re
import json
from pathlib import Path

def update_file_content(file_path, old_name, new_name):
    """Replace all occurrences of the old project name with the new one."""
    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            content = f.read()
        
        # Replace various case combinations
        replacements = [
            (old_name.lower(), new_name.lower()),
            (old_name.upper(), new_name.upper()),
            (old_name.capitalize(), new_name.capitalize()),
            (old_name, new_name),
        ]
        
        for old, new in replacements:
            content = content.replace(old, new)
        
        with open(file_path, 'w', encoding='utf-8') as f:
            f.write(content)
        
        return True
    except Exception as e:
        print(f"Warning: Could not update {file_path}: {e}")
        return False

def rename_files(directory, old_name, new_name):
    """Rename files that contain the old project name."""
    for root, dirs, files in os.walk(directory, topdown=False):
        # Rename files
        for filename in files:
            if old_name.lower() in filename.lower():
                old_path = os.path.join(root, filename)
                new_filename = filename.replace(old_name.lower(), new_name.lower())
                new_filename = new_filename.replace(old_name.capitalize(), new_name.capitalize())
                new_filename = new_filename.replace(old_name.upper(), new_name.upper())
                new_path = os.path.join(root, new_filename)
                
                if old_path != new_path:
                    try:
                        os.rename(old_path, new_path)
                        print(f"  Renamed: {filename} -> {new_filename}")
                    except Exception as e:
                        print(f"  Warning: Could not rename {old_path}: {e}")

def create_project(project_name, target_folder=None, template_name='clear'):
    """Create a new project from the template."""
    
    # Get the workspace root (script is in scripts/)
    script_dir = Path(__file__).parent
    workspace_root = script_dir.parent
    
    # Template source
    template_dir = workspace_root / 'examples' / template_name
    
    # Validate template exists
    if not template_dir.exists():
        print(f"Error: Template '{template_name}' not found at {template_dir}")
        return False
    
    # Determine target directory
    if target_folder:
        target_dir = Path(target_folder) / project_name
    else:
        target_dir = workspace_root / 'examples' / project_name
    
    # Check if target already exists
    if target_dir.exists():
        print(f"Error: Target directory already exists: {target_dir}")
        response = input("Do you want to overwrite it? (yes/no): ")
        if response.lower() not in ['yes', 'y']:
            print("Aborted.")
            return False
        shutil.rmtree(target_dir)
    
    print(f"Creating new project '{project_name}'...")
    print(f"  Template: {template_dir}")
    print(f"  Target:   {target_dir}")
    
    # Copy template to target
    try:
        shutil.copytree(template_dir, target_dir, 
                       ignore=shutil.ignore_patterns('bin', 'obj', '.DS_Store', '*.user'))
        print(f"  ✓ Copied template files")
    except Exception as e:
        print(f"Error copying template: {e}")
        return False
    
    # Update file contents
    print(f"  Updating file contents...")
    for root, dirs, files in os.walk(target_dir):
        # Skip bin and obj directories
        dirs[:] = [d for d in dirs if d not in ['bin', 'obj']]
        
        for filename in files:
            file_path = os.path.join(root, filename)
            # Only update text files
            if filename.endswith(('.cs', '.csproj', '.json', '.md', '.txt', '.xml', '.props', '.targets')):
                update_file_content(file_path, template_name, project_name)
    
    # Rename files
    print(f"  Renaming files...")
    rename_files(target_dir, template_name, project_name)
    
    print(f"\n✓ Project '{project_name}' created successfully at {target_dir}")
    
    # Show next steps
    print(f"\nNext steps:")
    print(f"  1. Open the project in VS Code")
    print(f"  2. Add '{project_name}' to .vscode/tasks.json and .vscode/launch.json")
    print(f"     (or run: python scripts/add-project-to-vscode-config.py {project_name})")
    print(f"  3. Build the project:")
    print(f"     - Desktop: dotnet build {target_dir.relative_to(workspace_root)}/{project_name}.csproj")
    print(f"     - Web: dotnet build {target_dir.relative_to(workspace_root)}/{project_name}Web.csproj")
    print(f"  4. Customize your app in Source/{project_name}-app.cs")
    
    return True

def main():
    if len(sys.argv) < 2:
        print("Usage: python create-project-from-template.py <project-name> [target-folder]")
        print("\nExamples:")
        print("  python create-project-from-template.py myapp")
        print("  python create-project-from-template.py myapp /path/to/projects")
        sys.exit(1)
    
    project_name = sys.argv[1]
    target_folder = sys.argv[2] if len(sys.argv) > 2 else None
    
    # Validate project name
    if not re.match(r'^[a-zA-Z][a-zA-Z0-9_-]*$', project_name):
        print("Error: Project name must start with a letter and contain only letters, numbers, hyphens, and underscores")
        sys.exit(1)
    
    success = create_project(project_name, target_folder)
    sys.exit(0 if success else 1)

if __name__ == '__main__':
    main()
