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
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace SokolApplicationBuilder
{
    public class DeleteExampleTask : Microsoft.Build.Utilities.Task
    {
        private Options options;

        public DeleteExampleTask(Options opts)
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
                string examplePath = Path.Combine(sokolNetHome, "examples", exampleName);
                
                if (!Directory.Exists(examplePath))
                {
                    Log.LogError($"ERROR: Example '{exampleName}' does not exist at '{examplePath}'");
                    return false;
                }

                // Show warning and prompt for confirmation
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("╔═══════════════════════════════════════════════════════════════════════╗");
                Console.WriteLine("║                            ⚠️  WARNING  ⚠️                             ║");
                Console.WriteLine("╚═══════════════════════════════════════════════════════════════════════╝");
                Console.ResetColor();
                Console.WriteLine();
                Console.WriteLine($"You are about to PERMANENTLY DELETE the example '{exampleName}':");
                Console.WriteLine();
                Console.WriteLine($"  • Project folder: {examplePath}");
                Console.WriteLine($"  • Solution entry: Sokol.NET.sln");
                Console.WriteLine($"  • VS Code launch configuration: .vscode/launch.json");
                Console.WriteLine($"  • VS Code tasks: .vscode/tasks.json");
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("⚠️  THIS ACTION CANNOT BE UNDONE!");
                Console.WriteLine("⚠️  ALL FILES WILL BE PERMANENTLY DELETED!");
                Console.ResetColor();
                Console.WriteLine();
                Console.Write("Type the example name to confirm deletion: ");
                
                string confirmation = Console.ReadLine()?.Trim();
                
                if (confirmation != exampleName)
                {
                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("✓ Deletion cancelled. No changes were made.");
                    Console.ResetColor();
                    return true;
                }

                Console.WriteLine();
                Console.WriteLine($"Deleting example '{exampleName}'...");

                // Remove from solution file
                RemoveFromSolution(sokolNetHome, exampleName);

                // Remove from VS Code configuration
                RemoveFromVSCodeConfig(sokolNetHome, exampleName);

                // Delete the project directory
                try
                {
                    Directory.Delete(examplePath, true);
                    Log.LogMessage(MessageImportance.Normal, $"Deleted project folder: {examplePath}");
                }
                catch (Exception ex)
                {
                    Log.LogError($"Failed to delete project folder: {ex.Message}");
                    return false;
                }

                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("✓ Example successfully deleted!");
                Console.ResetColor();
                Console.WriteLine();
                Console.WriteLine("Removed from:");
                Console.WriteLine($"  ✓ {examplePath}");
                Console.WriteLine($"  ✓ Sokol.NET.sln");
                Console.WriteLine($"  ✓ .vscode/launch.json");
                Console.WriteLine($"  ✓ .vscode/tasks.json");
                Console.WriteLine();

                return true;
            }
            catch (Exception ex)
            {
                Log.LogError($"Failed to delete example: {ex.Message}");
                Log.LogError(ex.StackTrace);
                return false;
            }
        }

        private void RemoveFromSolution(string sokolNetHome, string exampleName)
        {
            string slnPath = Path.Combine(sokolNetHome, "Sokol.NET.sln");
            
            if (!File.Exists(slnPath))
            {
                Log.LogWarning($"Warning: Solution file not found at '{slnPath}'");
                return;
            }

            string content = File.ReadAllText(slnPath);
            var lines = new List<string>(content.Split('\n'));
            var linesToRemove = new List<int>();

            // Find and mark lines related to this project for removal
            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];
                
                // Check for project entries
                if (line.Contains($"{exampleName}.csproj") || line.Contains($"{exampleName}Web.csproj"))
                {
                    // Mark the Project line
                    linesToRemove.Add(i);
                    
                    // Mark EndProject line (should be next line)
                    if (i + 1 < lines.Count && lines[i + 1].Contains("EndProject"))
                    {
                        linesToRemove.Add(i + 1);
                    }
                }
                // Check for build configuration entries
                else if (line.Contains($"{exampleName}.") || line.Contains($"{exampleName}Web."))
                {
                    linesToRemove.Add(i);
                }
            }

            // Remove lines in reverse order to maintain indices
            linesToRemove = linesToRemove.Distinct().OrderByDescending(x => x).ToList();
            foreach (int index in linesToRemove)
            {
                lines.RemoveAt(index);
            }

            File.WriteAllText(slnPath, string.Join("\n", lines));
            Log.LogMessage(MessageImportance.Normal, $"Removed '{exampleName}' from solution file");
        }

        private void RemoveFromVSCodeConfig(string sokolNetHome, string exampleName)
        {
            string vscodeDir = Path.Combine(sokolNetHome, ".vscode");
            
            if (!Directory.Exists(vscodeDir))
            {
                Log.LogWarning($"Warning: .vscode directory not found at '{vscodeDir}'");
                return;
            }

            // Remove from launch.json
            RemoveFromLaunchJson(Path.Combine(vscodeDir, "launch.json"), exampleName);

            // Remove from tasks.json
            RemoveFromTasksJson(Path.Combine(vscodeDir, "tasks.json"), exampleName);
        }

        private void RemoveFromLaunchJson(string launchJsonPath, string exampleName)
        {
            if (!File.Exists(launchJsonPath))
            {
                Log.LogWarning($"Warning: launch.json not found at '{launchJsonPath}'");
                return;
            }

            string content = File.ReadAllText(launchJsonPath);

            // Remove the entry from the options array
            var lines = content.Split('\n').ToList();
            var linesToRemove = new List<int>();

            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].Contains($"\"{exampleName}\""))
                {
                    linesToRemove.Add(i);
                }
            }

            // Remove lines in reverse order
            foreach (int index in linesToRemove.OrderByDescending(x => x))
            {
                lines.RemoveAt(index);
            }

            File.WriteAllText(launchJsonPath, string.Join("\n", lines));
            Log.LogMessage(MessageImportance.Normal, $"Removed '{exampleName}' from launch.json");
        }

        private void RemoveFromTasksJson(string tasksJsonPath, string exampleName)
        {
            if (!File.Exists(tasksJsonPath))
            {
                Log.LogWarning($"Warning: tasks.json not found at '{tasksJsonPath}'");
                return;
            }

            string content = File.ReadAllText(tasksJsonPath);
            var lines = content.Split('\n').ToList();
            var linesToRemove = new List<int>();

            // Find and mark all lines related to this example
            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];
                
                // Check for task labels
                if (line.Contains($"prepare-{exampleName}\"") || 
                    line.Contains($"prepare-{exampleName}-web\""))
                {
                    // Mark the entire task block (from current line back to opening brace and forward to closing brace)
                    int startIdx = i;
                    
                    // Go back to find the opening brace
                    while (startIdx > 0 && !lines[startIdx].Trim().Equals("{"))
                    {
                        startIdx--;
                    }
                    
                    // Go forward to find the closing brace and comma
                    int endIdx = i;
                    int braceCount = 0;
                    bool foundOpenBrace = false;
                    
                    for (int j = startIdx; j < lines.Count; j++)
                    {
                        if (lines[j].Contains("{")) 
                        {
                            braceCount++;
                            foundOpenBrace = true;
                        }
                        if (lines[j].Contains("}")) braceCount--;
                        
                        if (foundOpenBrace && braceCount == 0)
                        {
                            endIdx = j;
                            // Include the comma if present on the next line
                            if (endIdx + 1 < lines.Count && lines[endIdx].TrimEnd().EndsWith("},"))
                            {
                                // Already included
                            }
                            break;
                        }
                    }
                    
                    // Mark all lines from startIdx to endIdx
                    for (int k = startIdx; k <= endIdx; k++)
                    {
                        if (!linesToRemove.Contains(k))
                        {
                            linesToRemove.Add(k);
                        }
                    }
                }
                // Check for input options
                else if (line.Contains($"\"{exampleName}\"") || line.Contains($"\"examples/{exampleName}\""))
                {
                    linesToRemove.Add(i);
                }
            }

            // Remove lines in reverse order
            foreach (int index in linesToRemove.Distinct().OrderByDescending(x => x))
            {
                lines.RemoveAt(index);
            }

            File.WriteAllText(tasksJsonPath, string.Join("\n", lines));
            Log.LogMessage(MessageImportance.Normal, $"Removed tasks for '{exampleName}' from tasks.json");
        }
    }
}
