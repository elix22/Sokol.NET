#!/usr/bin/env python3
"""
Add a project to VS Code tasks.json and launch.json configurations.
Usage: python add-project-to-vscode-config.py <project-name>
"""

import json
import sys
from pathlib import Path

def add_to_tasks_json(workspace_root, project_name):
    """Add compilation, build, and prepare tasks for the project."""
    tasks_file = workspace_root / '.vscode' / 'tasks.json'
    
    with open(tasks_file, 'r', encoding='utf-8') as f:
        content = f.read()
    
    # Remove trailing comments for parsing
    import re
    json_content = re.sub(r'//.*', '', content)
    data = json.loads(json_content)
    
    # Find insertion points
    tasks = data['tasks']
    
    # Add compile shader tasks after the last compile shader task
    compile_shader_task = {
        "label": f"compile-{project_name}-shaders",
        "command": "dotnet",
        "type": "process",
        "args": [
            "msbuild",
            f"examples/{project_name}/{project_name}.csproj",
            "-t:CompileShaders"
        ],
        "problemMatcher": "$msCompile"
    }
    
    compile_shader_web_task = {
        "label": f"compile-{project_name}-web-shaders",
        "command": "dotnet",
        "type": "process",
        "args": [
            "msbuild",
            f"examples/{project_name}/{project_name}Web.csproj",
            "-t:CompileShaders"
        ],
        "problemMatcher": "$msCompile"
    }
    
    # Add build tasks
    build_task = {
        "label": f"build-{project_name}",
        "command": "dotnet",
        "type": "process",
        "args": [
            "build",
            f"examples/{project_name}/{project_name}.csproj"
        ],
        "group": "build",
        "problemMatcher": "$msCompile"
    }
    
    build_web_task = {
        "label": f"build-{project_name}-web",
        "command": "dotnet",
        "type": "process",
        "args": [
            "build",
            f"examples/{project_name}/{project_name}Web.csproj"
        ],
        "group": "build",
        "problemMatcher": "$msCompile"
    }
    
    # Add prepare tasks
    prepare_task = {
        "label": f"prepare-{project_name}",
        "dependsOrder": "sequence",
        "dependsOn": [
            f"compile-{project_name}-shaders",
            f"build-{project_name}"
        ]
    }
    
    prepare_web_task = {
        "label": f"prepare-{project_name}-web",
        "dependsOrder": "sequence",
        "dependsOn": [
            f"compile-{project_name}-web-shaders",
            f"build-{project_name}-web"
        ]
    }
    
    # Find where to insert (after compile-clear-web-shaders or similar)
    insert_after_compile = None
    insert_after_build = None
    insert_after_prepare = None
    
    for i, task in enumerate(tasks):
        if task.get('label', '').startswith('compile-') and '-web-shaders' in task.get('label', ''):
            insert_after_compile = i
        elif task.get('label', '').startswith('build-') and '-web' in task.get('label', ''):
            insert_after_build = i
        elif task.get('label', '').startswith('prepare-') and '-web' in task.get('label', ''):
            insert_after_prepare = i
    
    # Check if tasks already exist
    existing_labels = {task.get('label') for task in tasks}
    
    tasks_to_add = [
        (insert_after_compile, compile_shader_task),
        (insert_after_compile, compile_shader_web_task),
        (insert_after_build, build_task),
        (insert_after_build, build_web_task),
        (insert_after_prepare, prepare_task),
        (insert_after_prepare, prepare_web_task),
    ]
    
    offset = 0
    for insert_pos, task in tasks_to_add:
        if task['label'] not in existing_labels:
            tasks.insert(insert_pos + 1 + offset, task)
            offset += 1
            print(f"  Added task: {task['label']}")
        else:
            print(f"  Task already exists: {task['label']}")
    
    # Update inputs
    example_path_input = next((inp for inp in data['inputs'] if inp['id'] == 'examplePath'), None)
    if example_path_input:
        example_path = f"examples/{project_name}"
        if example_path not in example_path_input['options']:
            example_path_input['options'].append(example_path)
            example_path_input['options'].sort()
            print(f"  Added to examplePath input: {example_path}")
    
    # Write back with proper formatting
    with open(tasks_file, 'w', encoding='utf-8') as f:
        json.dump(data, f, indent='\t')
        f.write('\n')
    
    return True

def add_to_launch_json(workspace_root, project_name):
    """Add the project to launch.json inputs."""
    launch_file = workspace_root / '.vscode' / 'launch.json'
    
    with open(launch_file, 'r', encoding='utf-8') as f:
        data = json.load(f)
    
    # Update inputs
    example_name_input = next((inp for inp in data['inputs'] if inp['id'] == 'exampleName'), None)
    if example_name_input:
        if project_name not in example_name_input['options']:
            example_name_input['options'].append(project_name)
            example_name_input['options'].sort()
            print(f"  Added to exampleName input: {project_name}")
        else:
            print(f"  Project already in exampleName input: {project_name}")
    
    # Write back with proper formatting
    with open(launch_file, 'w', encoding='utf-8') as f:
        json.dump(data, f, indent=4)
        f.write('\n')
    
    return True

def main():
    if len(sys.argv) < 2:
        print("Usage: python add-project-to-vscode-config.py <project-name>")
        print("\nExample:")
        print("  python add-project-to-vscode-config.py myapp")
        sys.exit(1)
    
    project_name = sys.argv[1]
    
    # Get the workspace root (script is in scripts/)
    script_dir = Path(__file__).parent
    workspace_root = script_dir.parent
    
    print(f"Adding project '{project_name}' to VS Code configuration...")
    
    try:
        add_to_tasks_json(workspace_root, project_name)
        print("✓ Updated .vscode/tasks.json")
        
        add_to_launch_json(workspace_root, project_name)
        print("✓ Updated .vscode/launch.json")
        
        print(f"\n✓ Project '{project_name}' added to VS Code configuration successfully!")
        print(f"\nYou can now:")
        print(f"  - Build with: Ctrl+Shift+B (or Cmd+Shift+B) -> select 'build-{project_name}'")
        print(f"  - Debug with: F5 -> select 'Desktop (Sokol)' or 'Browser (Sokol)'")
        
    except Exception as e:
        print(f"Error: {e}")
        import traceback
        traceback.print_exc()
        sys.exit(1)

if __name__ == '__main__':
    main()
