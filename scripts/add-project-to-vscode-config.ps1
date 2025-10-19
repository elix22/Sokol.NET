# Add a project to VS Code tasks.json and launch.json configurations.
# Usage: .\add-project-to-vscode-config.ps1 <project-name>

param(
    [Parameter(Mandatory=$true)]
    [string]$ProjectName
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

# Get script directory and workspace root
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$WorkspaceRoot = Split-Path -Parent $ScriptDir

Write-Output "Adding project '$ProjectName' to VS Code configuration..."

# Check if Python is available
$pythonCmd = $null
if (Get-Command python3 -ErrorAction SilentlyContinue) {
    $pythonCmd = "python3"
} elseif (Get-Command python -ErrorAction SilentlyContinue) {
    $pythonCmd = "python"
} else {
    Write-Error2 "Python is required but not found. Please install Python 3."
    exit 1
}

# Use Python script to update JSON files
$pythonScript = Join-Path $ScriptDir "add-project-to-vscode-config.py"
& $pythonCmd $pythonScript $ProjectName

if ($LASTEXITCODE -eq 0) {
    Write-Success "`nYou can now use VS Code tasks and launch configurations!"
}
