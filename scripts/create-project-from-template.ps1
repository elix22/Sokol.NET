# Create a new Sokol C# project from the 'clear' template.
# Usage: .\create-project-from-template.ps1 <project-name> [target-folder]

param(
    [Parameter(Mandatory=$true)]
    [string]$ProjectName,
    
    [Parameter(Mandatory=$false)]
    [string]$TargetFolder = "",
    
    [Parameter(Mandatory=$false)]
    [string]$TemplateName = "clear"
)

function Write-ColorOutput($ForegroundColor, $Message) {
    $originalColor = $Host.UI.RawUI.ForegroundColor
    $Host.UI.RawUI.ForegroundColor = $ForegroundColor
    Write-Output $Message
    $Host.UI.RawUI.ForegroundColor = $originalColor
}

function Write-Success($Message) {
    Write-ColorOutput Green $Message
}

function Write-Error2($Message) {
    Write-ColorOutput Red "Error: $Message"
}

function Write-Info($Message) {
    Write-ColorOutput Yellow $Message
}

# Validate project name
if ($ProjectName -notmatch '^[a-zA-Z][a-zA-Z0-9_-]*$') {
    Write-Error2 "Project name must start with a letter and contain only letters, numbers, hyphens, and underscores"
    exit 1
}

# Get script directory and workspace root
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$WorkspaceRoot = Split-Path -Parent $ScriptDir

# Template source
$TemplateDir = Join-Path $WorkspaceRoot "examples\$TemplateName"

# Validate template exists
if (-not (Test-Path $TemplateDir)) {
    Write-Error2 "Template '$TemplateName' not found at $TemplateDir"
    exit 1
}

# Determine target directory
if ($TargetFolder) {
    $TargetDir = Join-Path $TargetFolder $ProjectName
} else {
    $TargetDir = Join-Path $WorkspaceRoot "examples\$ProjectName"
}

# Check if target already exists
if (Test-Path $TargetDir) {
    Write-Error2 "Target directory already exists: $TargetDir"
    $response = Read-Host "Do you want to overwrite it? (yes/no)"
    if ($response -notmatch '^y(es)?$') {
        Write-Output "Aborted."
        exit 0
    }
    Remove-Item -Recurse -Force $TargetDir
}

Write-Output "Creating new project '$ProjectName'..."
Write-Output "  Template: $TemplateDir"
Write-Output "  Target:   $TargetDir"

# Copy template to target (exclude build artifacts)
$excludeDirs = @('bin', 'obj')
$excludeFiles = @('.DS_Store', '*.user')

function Copy-Template {
    param($Source, $Destination)
    
    if (-not (Test-Path $Destination)) {
        New-Item -ItemType Directory -Path $Destination | Out-Null
    }
    
    Get-ChildItem -Path $Source -Force | ForEach-Object {
        $destPath = Join-Path $Destination $_.Name
        
        if ($_.PSIsContainer) {
            # Skip excluded directories
            if ($excludeDirs -notcontains $_.Name) {
                Copy-Template $_.FullName $destPath
            }
        } else {
            # Skip excluded files
            $skip = $false
            foreach ($pattern in $excludeFiles) {
                if ($_.Name -like $pattern) {
                    $skip = $true
                    break
                }
            }
            
            if (-not $skip) {
                Copy-Item $_.FullName $destPath -Force
            }
        }
    }
}

Copy-Template $TemplateDir $TargetDir
Write-Success "  ✓ Copied template files"

# Update file contents
Write-Output "  Updating file contents..."
$fileExtensions = @('*.cs', '*.csproj', '*.json', '*.md', '*.txt', '*.xml', '*.props', '*.targets')

Get-ChildItem -Path $TargetDir -Recurse -Include $fileExtensions | 
    Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' } | 
    ForEach-Object {
        $content = Get-Content $_.FullName -Raw -Encoding UTF8
        
        # Replace various case combinations
        $content = $content -replace $TemplateName, $ProjectName
        $templateCap = (Get-Culture).TextInfo.ToTitleCase($TemplateName.ToLower())
        $projectCap = (Get-Culture).TextInfo.ToTitleCase($ProjectName.ToLower())
        $content = $content -replace $templateCap, $projectCap
        $content = $content -replace $TemplateName.ToUpper(), $ProjectName.ToUpper()
        
        Set-Content -Path $_.FullName -Value $content -Encoding UTF8 -NoNewline
    }

# Rename files
Write-Output "  Renaming files..."
Get-ChildItem -Path $TargetDir -Recurse -File | 
    Where-Object { $_.Name -match $TemplateName -and $_.FullName -notmatch '\\(bin|obj)\\' } | 
    Sort-Object { $_.FullName.Length } -Descending |
    ForEach-Object {
        $oldName = $_.Name
        $newName = $oldName -replace $TemplateName, $ProjectName
        $templateCap = (Get-Culture).TextInfo.ToTitleCase($TemplateName.ToLower())
        $projectCap = (Get-Culture).TextInfo.ToTitleCase($ProjectName.ToLower())
        $newName = $newName -replace $templateCap, $projectCap
        $newName = $newName -replace $TemplateName.ToUpper(), $ProjectName.ToUpper()
        
        if ($oldName -ne $newName) {
            $newPath = Join-Path $_.Directory $newName
            Rename-Item $_.FullName $newPath
            Write-Output "    Renamed: $oldName -> $newName"
        }
    }

Write-Success "`n✓ Project '$ProjectName' created successfully at $TargetDir"

# Calculate relative path for display
$RelPath = $TargetDir -replace [regex]::Escape($WorkspaceRoot + '\'), ''

Write-Output ""
Write-Info "Next steps:"
Write-Output "  1. Add to VS Code configuration:"
Write-Output "     .\scripts\add-project-to-vscode-config.ps1 $ProjectName"
Write-Output ""
Write-Output "  2. Build the project:"
Write-Output "     dotnet build $RelPath\$ProjectName.csproj"
Write-Output ""
Write-Output "  3. Run the project:"
Write-Output "     dotnet run --project $RelPath\$ProjectName.csproj"
Write-Output ""
Write-Output "  4. Customize your app in Source\$ProjectName-app.cs"
