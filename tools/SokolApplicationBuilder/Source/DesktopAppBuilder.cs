using System;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Task = Microsoft.Build.Utilities.Task;

namespace SokolApplicationBuilder
{
    public class DesktopBuildTask : Task
    {
        Options opts;
        Dictionary<string, string> envVars = new();

        string PROJECT_UUID = string.Empty;
        string PROJECT_NAME = string.Empty;
        string JAVA_PACKAGE_PATH = string.Empty;
        string VERSION_CODE = string.Empty;
        string VERSION_NAME = string.Empty;

        string URHONET_HOME_PATH = string.Empty;

        string DEVELOPMENT_TEAM = string.Empty;

        Dictionary<string, string> envVarsDict = new();

        public DesktopBuildTask(Options opts)
        {
            this.opts = opts;
            Utils.opts = opts;
        }


        public override bool Equals(object? obj)
        {
            return base.Equals(obj);
        }

        public override bool Execute()
        {

            URHONET_HOME_PATH = Utils.GetUrhoNetHomePath();
            if (!ParseEnvironmentVariables())
            {
                Log.LogError("Failed to parse environment variables");
                return false;
            }

            string buildType = "Debug";

            if (opts.Type == "release")
            {
                buildType = "Release";
            }
            string targetFramework = (opts.Framework != "") ? opts.Framework : "net9.0";

            if (opts.OutputPath != "")
            {
                opts.OutputPath = Path.Combine(opts.OutputPath, opts.RID);
            }

            string projectPath = Path.Combine(opts.ProjectPath, PROJECT_NAME+".csproj");
            if (!File.Exists(projectPath))
            {
                Log.LogError($"Project file {PROJECT_NAME+".csproj"} not found , searching for a default project file");
                if(!Utils.FindProjectInPath(opts.ProjectPath, ref projectPath))
                {
                    Log.LogError("Default Project file not found");
                    return false;
                }
                else
                {
                    Log.LogMessage($"Project file {projectPath} found");
                }
            }

            string dotnet_build_command = $"dotnet publish -f {targetFramework} {projectPath} -r  {opts.RID} -c {buildType}   -p:PublishAot=true -p:TrimmerRemoveSymbols=false -p:TrimMode=partial -p:DisableUnsupportedError=true -p:PublishAotUsingRuntimePack=true -p:StripSymbols=true   -p:DefineConstants=\"_DESKTOP_PUBLISHED_BINARY_\" -o {opts.OutputPath}";

            (int exitCode, string output) = Utils.RunShellCommand(Log,
                dotnet_build_command,
                envVars,
                workingDir: opts.ProjectPath,
                logStdErrAsMessage: true,
                debugMessageImportance: MessageImportance.High,
                label: "dotnet-build");

            if (exitCode != 0)
            {
                Log.LogError("dotnet publish error");
                return false;
            }

            Path.Combine(opts.ProjectPath, "Assets").CopyDirectoryIfDifferent(Path.Combine(opts.OutputPath, ""), true);

            return true;
        }

        private bool ParseEnvironmentVariables()
        {

            string project_vars_path = Path.Combine(opts.ProjectPath, "script", "project_vars.sh");

            if (!File.Exists(project_vars_path))
            {
                Log.LogError($"project_vars.sh not found");
                return false;
            }

            project_vars_path.ParseEnvironmentVariables(out envVarsDict);

            PROJECT_UUID = envVarsDict.GetEnvValue("PROJECT_UUID");
            PROJECT_NAME = envVarsDict.GetEnvValue("PROJECT_NAME");
            JAVA_PACKAGE_PATH = envVarsDict.GetEnvValue("JAVA_PACKAGE_PATH");
            VERSION_CODE = envVarsDict.GetEnvValue("VERSION_CODE");
            VERSION_NAME = envVarsDict.GetEnvValue("VERSION_NAME");

            // Override PROJECT_NAME if specified via command line option
            if (!string.IsNullOrEmpty(opts.ProjectName))
            {
                PROJECT_NAME = opts.ProjectName;
                Log.LogMessage(MessageImportance.Normal, $"Using project name from command line: {PROJECT_NAME}");
            }

            // If PROJECT_NAME is still empty, use smart project selection
            if (string.IsNullOrEmpty(PROJECT_NAME))
            {
                string dummyPath = "";
                if (Utils.FindProjectInPath(opts.ProjectPath, ref dummyPath))
                {
                    PROJECT_NAME = Path.GetFileNameWithoutExtension(dummyPath);
                    Log.LogMessage(MessageImportance.Normal, $"Using auto-detected project name: {PROJECT_NAME}");
                }
                else
                {
                    Log.LogError("Could not determine project name");
                    return false;
                }
            }

            if (VERSION_CODE == string.Empty)
            {
                VERSION_CODE = "1";
            }

            if (VERSION_NAME == string.Empty)
            {
                VERSION_NAME = "1.0.0";
            }


            Console.WriteLine("UrhoNetHomePath = " + URHONET_HOME_PATH);
            Console.WriteLine("opts.OutputPath = " + opts.OutputPath);
            Console.WriteLine("OutputPath  = " + opts.OutputPath);
            Console.WriteLine("PROJECT_UUID" + "=" + PROJECT_UUID);
            Console.WriteLine("PROJECT_NAME" + "=" + PROJECT_NAME);
            Console.WriteLine("JAVA_PACKAGE_PATH" + "=" + JAVA_PACKAGE_PATH);


            return true;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public override string? ToString()
        {
            return base.ToString();
        }
    }

}