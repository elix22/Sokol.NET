// Copyright (c) 2022 Eli Aloni (a.k.a  elix22)
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace SokolApplicationBuilder
{
    public class CreateExampleTask : Microsoft.Build.Utilities.Task
    {
        private Options options;

        public CreateExampleTask(Options opts)
        {
            options = opts;
        }

        public override bool Execute()
        {
            try
            {
                string exampleName = options.ProjectName;
                
                if (string.IsNullOrWhiteSpace(exampleName))
                {
                    Log.LogError("ERROR: Example name is required. Use --project <name>");
                    return false;
                }

                // Validate example name (alphanumeric and underscore only)
                if (!Regex.IsMatch(exampleName, @"^[a-zA-Z][a-zA-Z0-9_]*$"))
                {
                    Log.LogError($"ERROR: Invalid example name '{exampleName}'. Name must start with a letter and contain only letters, numbers, and underscores.");
                    return false;
                }

                // Get SokolNetHome path
                string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (string.IsNullOrEmpty(homeDir) || !Directory.Exists(homeDir))
                {
                    homeDir = Environment.GetEnvironmentVariable("HOME") ?? "";
                }
                
                string sokolNetHomeFile = Path.Combine(homeDir, ".sokolnet_config", "sokolnet_home");
                if (!File.Exists(sokolNetHomeFile))
                {
                    Log.LogError("ERROR: SokolNetHome configuration not found. Please run 'register' task first.");
                    return false;
                }

                string sokolNetHome = File.ReadAllText(sokolNetHomeFile).Trim();
                string templatePath = Path.Combine(sokolNetHome, "templates", "template_example");
                string examplesPath = Path.Combine(sokolNetHome, "examples");
                string targetPath = Path.Combine(examplesPath, exampleName);

                // Validate template exists
                if (!Directory.Exists(templatePath))
                {
                    Log.LogError($"ERROR: Template directory not found at '{templatePath}'");
                    return false;
                }

                // Check if example already exists
                if (Directory.Exists(targetPath))
                {
                    Log.LogError($"ERROR: Example '{exampleName}' already exists at '{targetPath}'");
                    return false;
                }

                Log.LogMessage(MessageImportance.High, $"Creating new example '{exampleName}' from template...");
                Log.LogMessage(MessageImportance.High, $"Template: {templatePath}");
                Log.LogMessage(MessageImportance.High, $"Target: {targetPath}");

                // Create target directory
                Directory.CreateDirectory(targetPath);

                // Copy template files
                CopyDirectory(templatePath, targetPath, exampleName);

                // Rename files and update content
                RenameAndUpdateFiles(targetPath, exampleName);

                // Add to solution file
                AddToSolution(sokolNetHome, exampleName);

                // Add to VS Code configuration files
                AddToVSCodeConfig(sokolNetHome, exampleName);

                Log.LogMessage(MessageImportance.High, $"");
                Log.LogMessage(MessageImportance.High, $"Successfully created example '{exampleName}'!");
                Log.LogMessage(MessageImportance.High, $"Location: {targetPath}");
                Log.LogMessage(MessageImportance.High, $"");
                Log.LogMessage(MessageImportance.High, $"Configuration files updated:");
                Log.LogMessage(MessageImportance.High, $"- Added to Sokol.NET.sln");
                Log.LogMessage(MessageImportance.High, $"- Added to .vscode/launch.json (Desktop & Browser debugging)");
                Log.LogMessage(MessageImportance.High, $"- Added to .vscode/tasks.json (prepare tasks for all platforms)");
                Log.LogMessage(MessageImportance.High, $"");
                Log.LogMessage(MessageImportance.High, $"Next steps:");
                Log.LogMessage(MessageImportance.High, $"1. Add your shader code to: {Path.Combine(targetPath, "shaders")}");
                Log.LogMessage(MessageImportance.High, $"2. Add your assets to: {Path.Combine(targetPath, "Assets")}");
                Log.LogMessage(MessageImportance.High, $"3. Edit the main application: {Path.Combine(targetPath, "Source", exampleName + "-app.cs")}");
                Log.LogMessage(MessageImportance.High, $"4. Build desktop: dotnet run --project tools/SokolApplicationBuilder -- --task prepare --architecture desktop --path examples/{exampleName}");
                Log.LogMessage(MessageImportance.High, $"5. Or use VS Code: Press F5, select example '{exampleName}' from the list");

                return true;
            }
            catch (Exception ex)
            {
                Log.LogError($"ERROR: Failed to create example: {ex.Message}");
                Log.LogError($"Stack trace: {ex.StackTrace}");
                return false;
            }
        }

        private void CopyDirectory(string sourceDir, string targetDir, string exampleName)
        {
            // Get all files and subdirectories
            foreach (string dirPath in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
            {
                string relativePath = dirPath.Substring(sourceDir.Length + 1);
                Directory.CreateDirectory(Path.Combine(targetDir, relativePath));
            }

            foreach (string filePath in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                string relativePath = filePath.Substring(sourceDir.Length + 1);
                string targetFilePath = Path.Combine(targetDir, relativePath);
                
                // Skip .keep files
                if (Path.GetFileName(filePath) == ".keep")
                {
                    continue;
                }

                File.Copy(filePath, targetFilePath, true);
            }
        }

        private void RenameAndUpdateFiles(string targetPath, string exampleName)
        {
            // Rename template.csproj to exampleName.csproj
            string oldCsprojPath = Path.Combine(targetPath, "template.csproj");
            string newCsprojPath = Path.Combine(targetPath, exampleName + ".csproj");
            if (File.Exists(oldCsprojPath))
            {
                File.Move(oldCsprojPath, newCsprojPath);
            }

            // Rename templateWeb.csproj to exampleNameWeb.csproj
            string oldWebCsprojPath = Path.Combine(targetPath, "templateWeb.csproj");
            string newWebCsprojPath = Path.Combine(targetPath, exampleName + "Web.csproj");
            if (File.Exists(oldWebCsprojPath))
            {
                File.Move(oldWebCsprojPath, newWebCsprojPath);
            }

            // Rename template-app.cs to exampleName-app.cs
            string oldAppPath = Path.Combine(targetPath, "Source", "template-app.cs");
            string newAppPath = Path.Combine(targetPath, "Source", exampleName + "-app.cs");
            if (File.Exists(oldAppPath))
            {
                File.Move(oldAppPath, newAppPath);
            }

            // Update content in all C# files
            UpdateFileContent(newAppPath, "TemplateApp", ToPascalCase(exampleName) + "App");
            UpdateFileContent(Path.Combine(targetPath, "Source", "Program.cs"), "TemplateApp", ToPascalCase(exampleName) + "App");

            // Update .csproj file references
            UpdateFileContent(newCsprojPath, "template", exampleName);
            UpdateFileContent(newWebCsprojPath, "template", exampleName);
        }

        private void UpdateFileContent(string filePath, string oldValue, string newValue)
        {
            if (!File.Exists(filePath))
            {
                return;
            }

            string content = File.ReadAllText(filePath);
            content = content.Replace(oldValue, newValue);
            File.WriteAllText(filePath, content);
        }

        private string ToPascalCase(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return input;
            }

            // Split by underscore and capitalize each word
            var parts = input.Split('_');
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length > 0)
                {
                    parts[i] = char.ToUpper(parts[i][0]) + parts[i].Substring(1).ToLower();
                }
            }

            return string.Join("", parts);
        }

        private void AddToSolution(string sokolNetHome, string exampleName)
        {
            string slnPath = Path.Combine(sokolNetHome, "Sokol.NET.sln");
            if (!File.Exists(slnPath))
            {
                Log.LogWarning($"Warning: Solution file not found at '{slnPath}'");
                return;
            }

            string slnContent = File.ReadAllText(slnPath);
            
            // Check if already exists
            if (slnContent.Contains($"{exampleName}.csproj"))
            {
                Log.LogMessage(MessageImportance.Normal, $"Project '{exampleName}' already in solution");
                return;
            }

            // Generate new project GUIDs
            string projectGuid = Guid.NewGuid().ToString().ToUpper();
            string webProjectGuid = Guid.NewGuid().ToString().ToUpper();

            // Find the last project entry before GlobalSection
            int globalSectionIndex = slnContent.IndexOf("Global");
            if (globalSectionIndex == -1)
            {
                Log.LogWarning("Warning: Could not find Global section in solution file");
                return;
            }

            // Add projects before Global section
            string projectEntries = 
$@"Project(""{{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}}"") = ""{exampleName}"", ""examples\{exampleName}\{exampleName}.csproj"", ""{{{projectGuid}}}""
EndProject
Project(""{{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}}"") = ""{exampleName}Web"", ""examples\{exampleName}\{exampleName}Web.csproj"", ""{{{webProjectGuid}}}""
EndProject
";

            slnContent = slnContent.Insert(globalSectionIndex, projectEntries);

            // Add to solution configuration section
            string configSection = "\t\tHideSolutionNode = preSolution";
            int configIndex = slnContent.IndexOf(configSection);
            if (configIndex != -1)
            {
                // Find the end of GlobalSection(ProjectConfigurationPlatforms)
                string projectConfigEnd = "\tEndGlobalSection";
                int projectConfigEndIndex = slnContent.IndexOf(projectConfigEnd, configIndex);
                
                if (projectConfigEndIndex != -1)
                {
                    // Find the previous EndGlobalSection
                    int previousEndIndex = slnContent.LastIndexOf(projectConfigEnd, projectConfigEndIndex - 1);
                    
                    if (previousEndIndex != -1)
                    {
                        string buildConfigs = 
$@"		{{{projectGuid}}}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{{{projectGuid}}}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{{{projectGuid}}}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{{{projectGuid}}}.Release|Any CPU.Build.0 = Release|Any CPU
		{{{webProjectGuid}}}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{{{webProjectGuid}}}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{{{webProjectGuid}}}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{{{webProjectGuid}}}.Release|Any CPU.Build.0 = Release|Any CPU
";
                        slnContent = slnContent.Insert(previousEndIndex, buildConfigs);
                    }
                }
            }

            File.WriteAllText(slnPath, slnContent);
            Log.LogMessage(MessageImportance.Normal, $"Added '{exampleName}' to solution file");
        }

        private void AddToVSCodeConfig(string sokolNetHome, string exampleName)
        {
            string vscodeDir = Path.Combine(sokolNetHome, ".vscode");
            if (!Directory.Exists(vscodeDir))
            {
                Log.LogWarning($"Warning: .vscode directory not found at '{vscodeDir}'");
                return;
            }

            // Update launch.json
            UpdateLaunchJson(Path.Combine(vscodeDir, "launch.json"), exampleName);

            // Update tasks.json
            UpdateTasksJson(Path.Combine(vscodeDir, "tasks.json"), exampleName);
        }

        private void UpdateLaunchJson(string launchJsonPath, string exampleName)
        {
            if (!File.Exists(launchJsonPath))
            {
                Log.LogWarning($"Warning: launch.json not found at '{launchJsonPath}'");
                return;
            }

            string content = File.ReadAllText(launchJsonPath);

            // Check if already exists
            if (content.Contains($"\"{exampleName}\""))
            {
                Log.LogMessage(MessageImportance.Normal, $"Example '{exampleName}' already in launch.json");
                return;
            }

            // Find the exampleName input section
            string searchPattern = "\"default\": \"cube\"";
            int insertIndex = content.LastIndexOf(searchPattern);
            
            if (insertIndex != -1)
            {
                // Insert before the default line
                int lineStart = content.LastIndexOf("\"options\": [", insertIndex);
                if (lineStart != -1)
                {
                    // Find where to insert (after the opening bracket and newline)
                    int optionsStart = content.IndexOf("[", lineStart) + 1;
                    int nextLine = content.IndexOf("\n", optionsStart) + 1;
                    
                    // Find proper indentation
                    int indentEnd = nextLine;
                    while (indentEnd < content.Length && (content[indentEnd] == ' ' || content[indentEnd] == '\t'))
                    {
                        indentEnd++;
                    }
                    string indent = content.Substring(nextLine, indentEnd - nextLine);
                    
                    string newEntry = $"{indent}\"{exampleName}\",\n";
                    content = content.Insert(nextLine, newEntry);
                    
                    File.WriteAllText(launchJsonPath, content);
                    Log.LogMessage(MessageImportance.Normal, $"Added '{exampleName}' to launch.json");
                }
            }
        }

        private void UpdateTasksJson(string tasksJsonPath, string exampleName)
        {
            if (!File.Exists(tasksJsonPath))
            {
                Log.LogWarning($"Warning: tasks.json not found at '{tasksJsonPath}'");
                return;
            }

            string content = File.ReadAllText(tasksJsonPath);

            // Check if already exists
            if (content.Contains($"prepare-{exampleName}"))
            {
                Log.LogMessage(MessageImportance.Normal, $"Tasks for '{exampleName}' already in tasks.json");
                return;
            }

            // Find the Create New Example task (or the last task before inputs section)
            string searchPattern = "\"label\": \"Create New Example\"";
            int insertIndex = content.IndexOf(searchPattern);
            
            if (insertIndex == -1)
            {
                // Fallback: find the inputs section
                searchPattern = "\"inputs\": [";
                insertIndex = content.IndexOf(searchPattern);
            }
            
            if (insertIndex != -1)
            {
                // Find the end of the previous task (before this position)
                int taskEndIndex = content.LastIndexOf("},", insertIndex);
                
                if (taskEndIndex != -1)
                {
                    // Insert after the comma and newline
                    int insertPosition = taskEndIndex + 2;
                    
                    // Find next newline to get proper indentation
                    int nextLine = content.IndexOf("\n", insertPosition) + 1;
                    
                    string tasks = $@"
		{{
			""label"": ""prepare-{exampleName}"",
			""type"": ""shell"",
			""command"": ""dotnet"",
			""args"": [""run"", ""--project"", ""${{workspaceFolder}}/tools/SokolApplicationBuilder"", ""--"", ""--task"", ""prepare"", ""--architecture"", ""desktop"", ""--path"", ""${{workspaceFolder}}/examples/{exampleName}""],
			""problemMatcher"": ""$msCompile""
		}},
		{{
			""label"": ""prepare-{exampleName}-web"",
			""type"": ""shell"",
			""command"": ""dotnet"",
			""args"": [""run"", ""--project"", ""${{workspaceFolder}}/tools/SokolApplicationBuilder"", ""--"", ""--task"", ""prepare"", ""--architecture"", ""web"", ""--path"", ""${{workspaceFolder}}/examples/{exampleName}""],
			""problemMatcher"": ""$msCompile""
		}},";
                    
                    content = content.Insert(insertPosition, tasks);
                    
                    File.WriteAllText(tasksJsonPath, content);
                    Log.LogMessage(MessageImportance.Normal, $"Added tasks for '{exampleName}' to tasks.json");
                }
            }

            // Also add to inputs section
            AddToTasksInputs(tasksJsonPath, exampleName);
        }

        private void AddToTasksInputs(string tasksJsonPath, string exampleName)
        {
            string content = File.ReadAllText(tasksJsonPath);
            bool modified = false;

            // Find and update the exampleName input options (for launch.json picker)
            modified |= AddToInputOptions(ref content, "\"id\": \"exampleName\"", exampleName);

            // Find and update the examplePath input options (for Android/iOS tasks)
            modified |= AddToInputOptions(ref content, "\"id\": \"examplePath\"", $"examples/{exampleName}");

            // Find and update the exampleToDelete input options (for delete task)
            modified |= AddToInputOptions(ref content, "\"id\": \"exampleToDelete\"", exampleName);

            if (modified)
            {
                File.WriteAllText(tasksJsonPath, content);
            }
        }

        private bool AddToInputOptions(ref string content, string inputIdPattern, string valueToAdd)
        {
            int inputIndex = content.IndexOf(inputIdPattern);
            
            if (inputIndex != -1)
            {
                // Find the options array after this input
                int optionsStart = content.IndexOf("\"options\": [", inputIndex);
                
                // Find the end of this options array (before the next input or end of inputs)
                int nextInputIndex = content.IndexOf("\"id\":", inputIndex + inputIdPattern.Length);
                
                if (optionsStart != -1 && (nextInputIndex == -1 || optionsStart < nextInputIndex))
                {
                    // Check if the value already exists
                    int closingBracket = content.IndexOf("]", optionsStart);
                    if (closingBracket != -1)
                    {
                        string optionsSection = content.Substring(optionsStart, closingBracket - optionsStart);
                        if (optionsSection.Contains($"\"{valueToAdd}\""))
                        {
                            return false; // Already exists
                        }
                    }

                    // Find where to insert (after the opening bracket)
                    int bracketIndex = content.IndexOf("[", optionsStart) + 1;
                    int nextLine = content.IndexOf("\n", bracketIndex) + 1;
                    
                    // Get indentation
                    int indentEnd = nextLine;
                    while (indentEnd < content.Length && (content[indentEnd] == ' ' || content[indentEnd] == '\t'))
                    {
                        indentEnd++;
                    }
                    string indent = content.Substring(nextLine, indentEnd - nextLine);
                    
                    string newEntry = $"{indent}\"{valueToAdd}\",\n";
                    content = content.Insert(nextLine, newEntry);
                    return true;
                }
            }
            
            return false;
        }
    }
}
