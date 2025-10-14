using System;
using System.IO;
using System.Diagnostics;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Task = Microsoft.Build.Utilities.Task;

namespace SokolApplicationBuilder
{
    public class ShaderCompileTask : Task
    {
        private readonly Options opts;

        public ShaderCompileTask(Options opts)
        {
            this.opts = opts;
            Utils.opts = opts;
        }

        public override bool Execute()
        {
            try
            {
                Log.LogMessage(MessageImportance.High, "🎨 Compiling Shaders...");

                // Determine project name
                string projectName = GetProjectName(opts.ProjectPath);
                if (string.IsNullOrEmpty(projectName))
                {
                    Log.LogError("Could not determine project name");
                    return false;
                }

                Log.LogMessage(MessageImportance.Normal, $"Project name: {projectName}");

                // Find project file
                string projectFile = Path.Combine(opts.ProjectPath, $"{projectName}.csproj");
                if (!File.Exists(projectFile))
                {
                    Log.LogError($"Project file not found: {projectFile}");
                    if (!Utils.FindProjectInPath(opts.ProjectPath, ref projectFile))
                    {
                        Log.LogError("No .csproj file found in project directory");
                        return false;
                    }
                    Log.LogMessage(MessageImportance.Normal, $"Using project file: {projectFile}");
                }

                // Ensure paths are absolute
                string absoluteProjectFile = Path.GetFullPath(projectFile);

                // Determine platform-specific defines if needed
                string defineConstants = "";
                if (!string.IsNullOrEmpty(opts.Arch))
                {
                    switch (opts.Arch.ToLower())
                    {
                        case "android":
                            defineConstants = "-p:DefineConstants=\"__ANDROID__\"";
                            break;
                        case "ios":
                            defineConstants = "-p:DefineConstants=\"__IOS__\"";
                            break;
                        case "web":
                            defineConstants = "-p:DefineConstants=\"__WEB__\"";
                            break;
                    }
                }

                // Build shader compilation command
                string msbuildCommand = $"dotnet msbuild \"{absoluteProjectFile}\" -t:CompileShaders {defineConstants}";

                Log.LogMessage(MessageImportance.High, "🔨 Running MSBuild to compile shaders...");
                (int exitCode, string output) = Utils.RunShellCommand(
                    Log,
                    msbuildCommand,
                    new Dictionary<string, string>(),
                    workingDir: opts.ProjectPath,
                    logStdErrAsMessage: true,
                    debugMessageImportance: MessageImportance.High,
                    label: "msbuild-compile-shaders");

                if (exitCode != 0)
                {
                    Log.LogError("Shader compilation failed");
                    return false;
                }

                Log.LogMessage(MessageImportance.High, "✅ Shaders compiled successfully");

                // Also build the project if requested
                if (opts.Compile)
                {
                    Log.LogMessage(MessageImportance.High, "📦 Building project...");
                    string buildCommand = $"dotnet build \"{absoluteProjectFile}\"";

                    (int buildExitCode, string buildOutput) = Utils.RunShellCommand(
                        Log,
                        buildCommand,
                        new Dictionary<string, string>(),
                        workingDir: opts.ProjectPath,
                        logStdErrAsMessage: true,
                        debugMessageImportance: MessageImportance.High,
                        label: "dotnet-build");

                    if (buildExitCode != 0)
                    {
                        Log.LogError("Project build failed");
                        return false;
                    }

                    Log.LogMessage(MessageImportance.High, "✅ Project built successfully");
                }

                return true;
            }
            catch (Exception ex)
            {
                Log.LogError($"Shader compilation failed: {ex.Message}");
                return false;
            }
        }

        private string GetProjectName(string projectPath)
        {
            // If project name is explicitly provided via options, use it
            if (!string.IsNullOrEmpty(opts.ProjectName))
            {
                Log.LogMessage(MessageImportance.Normal, $"Using explicitly specified project name: {opts.ProjectName}");
                return opts.ProjectName;
            }

            // Find all .csproj files in the project directory
            string[] csprojFiles = Directory.GetFiles(projectPath, "*.csproj");

            if (csprojFiles.Length == 0)
            {
                Log.LogError($"No .csproj files found in directory: {projectPath}");
                return string.Empty;
            }

            if (csprojFiles.Length == 1)
            {
                // Only one project found, use it
                string projectName = Path.GetFileNameWithoutExtension(csprojFiles[0]);
                Log.LogMessage(MessageImportance.Normal, $"Found single project: {projectName}");
                return projectName;
            }

            // Multiple projects found - try to find the main one
            // First, try to find a project with the same name as the directory
            string dirName = new DirectoryInfo(projectPath).Name;
            string expectedProjectFile = Path.Combine(projectPath, $"{dirName}.csproj");
            if (File.Exists(expectedProjectFile))
            {
                Log.LogMessage(MessageImportance.Normal, $"Using project matching directory name: {dirName}");
                return dirName;
            }

            // If architecture is specified, try to find project with that suffix
            if (!string.IsNullOrEmpty(opts.Arch))
            {
                string archSuffix = opts.Arch == "web" ? "Web" : opts.Arch;
                var matchingProject = csprojFiles.FirstOrDefault(f =>
                    Path.GetFileNameWithoutExtension(f).EndsWith(archSuffix, StringComparison.OrdinalIgnoreCase));

                if (matchingProject != null)
                {
                    string projectName = Path.GetFileNameWithoutExtension(matchingProject);
                    Log.LogMessage(MessageImportance.Normal, $"Using project matching architecture: {projectName}");
                    return projectName;
                }
            }

            // Fallback: use the first project that doesn't have platform suffix
            var mainProject = csprojFiles.FirstOrDefault(f =>
            {
                string name = Path.GetFileNameWithoutExtension(f);
                return !name.EndsWith("Web", StringComparison.OrdinalIgnoreCase) &&
                       !name.EndsWith("Android", StringComparison.OrdinalIgnoreCase) &&
                       !name.EndsWith("iOS", StringComparison.OrdinalIgnoreCase);
            });

            if (mainProject != null)
            {
                string projectName = Path.GetFileNameWithoutExtension(mainProject);
                Log.LogMessage(MessageImportance.Normal, $"Using non-platform-specific project: {projectName}");
                return projectName;
            }

            // Last resort: use the first project
            string fallbackName = Path.GetFileNameWithoutExtension(csprojFiles[0]);
            Log.LogMessage(MessageImportance.Normal, $"Using first project found: {fallbackName}");
            return fallbackName;
        }
    }
}
