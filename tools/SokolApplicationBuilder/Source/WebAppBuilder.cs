using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Task = Microsoft.Build.Utilities.Task;


namespace SokolApplicationBuilder
{
    public class WebBuildTask : Task
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

        public WebBuildTask(Options opts)
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
            // Register all .NET SDKs that are installe on this machine.

            URHONET_HOME_PATH = Utils.GetUrhoNetHomePath();
            if (!ParseEnvironmentVariables())
            {
                Log.LogError("Failed to parse environment variables");
                return false;
            }

            string parentProjectPath = Path.Combine(opts.ProjectPath, PROJECT_NAME+".csproj");
            if (!File.Exists(parentProjectPath))
            {
                Log.LogError($"Project file {PROJECT_NAME+".csproj"} not found , searching for a default project file");
                if(!Utils.FindProjectInPath(opts.ProjectPath, ref parentProjectPath))
                {
                    Log.LogError("Default Project file not found");
                    return false;
                }
                else
                {
                    Log.LogMessage($"Project file {parentProjectPath} found");
                }
            }

            List<ReferenceInfo> references = new List<ReferenceInfo>();
            List<PackageReferenceInfo> packageReferences = new List<PackageReferenceInfo>();
            Utils.GetProjectReferences(parentProjectPath,ref references);
            Utils.GetProjectPackageReferences(parentProjectPath, ref packageReferences);

            string buildType = "Debug";

            if (opts.Type == "release")
            {
                buildType = "Release";
            }

            // net10.0 doesnt work so for now we will use Net8.0
            // sudo dotnet workload install wasm-tools-net8
            string targetFramework = (opts.Framework != "") ? opts.Framework : "net8.0";

            if (opts.OutputPath != "")
            {
                opts.OutputPath = Path.Combine(opts.OutputPath, "Web", buildType);
            }

            opts.ProjectPath = Path.Combine(opts.ProjectPath, "Web");

            string projectName = PROJECT_NAME + "Web.csproj";


            if (!Directory.Exists(Path.Combine(opts.ProjectPath)))
            {
                Path.Combine(URHONET_HOME_PATH, "template/Web").CopyDirectory(Path.Combine(opts.ProjectPath), true);
                File.Move(Path.Combine(opts.ProjectPath, "template.csproj"), Path.Combine(opts.ProjectPath, projectName), true);
            }


            string initialMemory = envVarsDict.GetEnvValue("WEB_INITIAL_MEMORY");
            if(initialMemory == string.Empty)
            {
                initialMemory = "128MB";
            }
            Path.Combine(opts.ProjectPath, projectName).ReplaceInfile("@INITIAL_MEMORY@", initialMemory);

            string maxMemory = envVarsDict.GetEnvValue("WEB_MAXIMUM_MEMORY");
            if(maxMemory == string.Empty)
            {
                maxMemory = "2048MB";
            }
            Path.Combine(opts.ProjectPath, projectName).ReplaceInfile("@MAXIMUM_MEMORY@", maxMemory);

            string stackSize = envVarsDict.GetEnvValue("WEB_STACK_SIZE");
            if(stackSize == string.Empty)
            {
                stackSize = "10000000";
            }
            Path.Combine(opts.ProjectPath, projectName).ReplaceInfile("@ASYNCIFY_STACK_SIZE@", stackSize);


            string totalMemory = envVarsDict.GetEnvValue("WEB_TOTAL_MEMORY");
            if(totalMemory == string.Empty)
            {
                totalMemory = "520MB";
            }
            Path.Combine(opts.ProjectPath, projectName).ReplaceInfile("@TOTAL_MEMORY@", totalMemory);

      

            // ADD all referecnes and package references to the project
            Utils.AppendReferencesAndPackageReferencesToProject(Path.Combine(opts.ProjectPath, projectName), references, packageReferences);

            string projectFile = Path.Combine(opts.ProjectPath, projectName);
            string dotnet_build_command = $"dotnet build -f {targetFramework} \"{projectFile}\" -c {buildType} -p:DefineConstants=\"WEB\" -o {opts.OutputPath}";

            (int exitCode, string output) = Utils.RunShellCommand(Log,
                dotnet_build_command,
                envVars,
                workingDir: opts.ProjectPath,
                logStdErrAsMessage: true,
                debugMessageImportance: MessageImportance.High,
                label: "dotnet-web-build");

            if (exitCode != 0)
            {
                Log.LogError("dotnet publish error");
                return false;
            }

            // Process web icon if specified (use parent project path, not Web subfolder)
            string iconProjectPath = Path.GetDirectoryName(opts.ProjectPath) ?? opts.ProjectPath;
            ProcessWebIcon(iconProjectPath, opts.OutputPath);

            return true;
        }

        private void ProcessWebIcon(string projectPath, string outputPath)
        {
            try
            {
                string? webIcon = ReadWebIconFromDirectoryBuildProps(projectPath);
                
                if (string.IsNullOrEmpty(webIcon))
                {
                    Log.LogMessage(MessageImportance.Normal, "ℹ️  No WebIcon specified in Directory.Build.props");
                    return;
                }

                string? sourceIconPath = FindIconFile(projectPath, webIcon);
                
                if (sourceIconPath == null)
                {
                    Log.LogWarning($"⚠️  Web icon not found: {webIcon}");
                    return;
                }

                Log.LogMessage(MessageImportance.High, $"🌐 Processing Web icon: {Path.GetFileName(sourceIconPath)}");

                // Generate all web icon formats
                GenerateFavicon(sourceIconPath, outputPath);
                GenerateAppleTouchIcon(sourceIconPath, outputPath);
                GenerateManifestIcons(sourceIconPath, outputPath);
                GenerateWebManifest(outputPath, PROJECT_NAME);

                Log.LogMessage(MessageImportance.High, "✅ Web icon processed successfully");
            }
            catch (Exception ex)
            {
                Log.LogWarning($"⚠️  Failed to process web icon: {ex.Message}");
            }
        }

        private void GenerateFavicon(string sourceIconPath, string outputPath)
        {
            string faviconPath = Path.Combine(outputPath, "favicon.ico");
            
            // Try to create multi-size favicon.ico with ImageMagick
            bool created = false;
            
            // Resize source to 48, 32, 16 for favicon
            var tempIcons = new List<string>();
            var sizes = new[] { 48, 32, 16 };
            
            foreach (int size in sizes)
            {
                string tempIcon = Path.Combine(Path.GetTempPath(), $"favicon_{size}.png");
                if (ResizeImageForWeb(sourceIconPath, tempIcon, size))
                {
                    tempIcons.Add(tempIcon);
                }
            }

            if (tempIcons.Count > 0)
            {
                string resizedArgs = string.Join(" ", tempIcons.Select(p => $"\"{p}\""));
                
                try
                {
                    var magickResult = CliWrap.Cli.Wrap("magick")
                        .WithArguments($"convert {resizedArgs} \"{faviconPath}\"")
                        .WithValidation(CliWrap.CommandResultValidation.None)
                        .ExecuteAsync()
                        .GetAwaiter()
                        .GetResult();

                    if (magickResult.ExitCode == 0)
                    {
                        created = true;
                        Log.LogMessage(MessageImportance.Normal, $"   ✅ Created favicon.ico");
                    }
                }
                catch { }

                if (!created)
                {
                    try
                    {
                        var convertResult = CliWrap.Cli.Wrap("convert")
                            .WithArguments($"{resizedArgs} \"{faviconPath}\"")
                            .WithValidation(CliWrap.CommandResultValidation.None)
                            .ExecuteAsync()
                            .GetAwaiter()
                            .GetResult();

                        if (convertResult.ExitCode == 0)
                        {
                            created = true;
                            Log.LogMessage(MessageImportance.Normal, $"   ✅ Created favicon.ico");
                        }
                    }
                    catch { }
                }

                // Clean up temp files
                foreach (var tempIcon in tempIcons)
                {
                    try { File.Delete(tempIcon); } catch { }
                }
            }

            if (!created)
            {
                // Fallback: Create 32x32 PNG as favicon.png
                string faviconPngPath = Path.Combine(outputPath, "favicon.png");
                if (ResizeImageForWeb(sourceIconPath, faviconPngPath, 32))
                {
                    Log.LogMessage(MessageImportance.Normal, $"   ✅ Created favicon.png (32x32) - ICO generation failed");
                }
            }
        }

        private void GenerateAppleTouchIcon(string sourceIconPath, string outputPath)
        {
            // Apple touch icon sizes
            var sizes = new[] 
            {
                (180, "apple-touch-icon-180x180.png"),  // iPhone @3x
                (167, "apple-touch-icon-167x167.png"),  // iPad Pro
                (152, "apple-touch-icon-152x152.png"),  // iPad @2x
                (120, "apple-touch-icon-120x120.png")   // iPhone @2x
            };

            foreach (var (size, filename) in sizes)
            {
                string iconPath = Path.Combine(outputPath, filename);
                if (ResizeImageForWeb(sourceIconPath, iconPath, size))
                {
                    Log.LogMessage(MessageImportance.Normal, $"   ✅ Created {filename} ({size}x{size})");
                }
            }

            // Also create default apple-touch-icon.png (180x180)
            string defaultAppleIcon = Path.Combine(outputPath, "apple-touch-icon.png");
            if (ResizeImageForWeb(sourceIconPath, defaultAppleIcon, 180))
            {
                Log.LogMessage(MessageImportance.Normal, $"   ✅ Created apple-touch-icon.png (180x180)");
            }
        }

        private void GenerateManifestIcons(string sourceIconPath, string outputPath)
        {
            // PWA manifest icon sizes
            var sizes = new[] 
            {
                (192, "icon-192x192.png"),  // Standard icon
                (512, "icon-512x512.png")   // Large icon for splash
            };

            foreach (var (size, filename) in sizes)
            {
                string iconPath = Path.Combine(outputPath, filename);
                if (ResizeImageForWeb(sourceIconPath, iconPath, size))
                {
                    Log.LogMessage(MessageImportance.Normal, $"   ✅ Created {filename} ({size}x{size})");
                }
            }
        }

        private void GenerateWebManifest(string outputPath, string appName)
        {
            string manifestPath = Path.Combine(outputPath, "manifest.json");
            
            var manifest = new
            {
                name = appName,
                short_name = appName,
                description = $"{appName} - Powered by Sokol",
                start_url = "./",
                display = "standalone",
                background_color = "#ffffff",
                theme_color = "#000000",
                icons = new[]
                {
                    new
                    {
                        src = "icon-192x192.png",
                        sizes = "192x192",
                        type = "image/png",
                        purpose = "any maskable"
                    },
                    new
                    {
                        src = "icon-512x512.png",
                        sizes = "512x512",
                        type = "image/png",
                        purpose = "any maskable"
                    }
                }
            };

            string json = System.Text.Json.JsonSerializer.Serialize(manifest, new System.Text.Json.JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
            
            File.WriteAllText(manifestPath, json);
            Log.LogMessage(MessageImportance.Normal, $"   ✅ Created manifest.json");
        }

        private bool ResizeImageForWeb(string sourceIcon, string outputPath, int size)
        {
            try
            {
                // Try ImageMagick 7+ with 'magick' command
                var magickResult = CliWrap.Cli.Wrap("magick")
                    .WithArguments($"convert \"{sourceIcon}\" -resize {size}x{size} \"{outputPath}\"")
                    .WithValidation(CliWrap.CommandResultValidation.None)
                    .ExecuteAsync()
                    .GetAwaiter()
                    .GetResult();

                if (magickResult.ExitCode == 0)
                    return true;
            }
            catch { }

            try
            {
                // Try ImageMagick 6 with 'convert' command
                var convertResult = CliWrap.Cli.Wrap("convert")
                    .WithArguments($"\"{sourceIcon}\" -resize {size}x{size} \"{outputPath}\"")
                    .WithValidation(CliWrap.CommandResultValidation.None)
                    .ExecuteAsync()
                    .GetAwaiter()
                    .GetResult();

                if (convertResult.ExitCode == 0)
                    return true;
            }
            catch { }

            // Try sips (macOS only)
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                try
                {
                    var sipsResult = CliWrap.Cli.Wrap("sips")
                        .WithArguments($"-z {size} {size} \"{sourceIcon}\" --out \"{outputPath}\"")
                        .WithValidation(CliWrap.CommandResultValidation.None)
                        .ExecuteAsync()
                        .GetAwaiter()
                        .GetResult();

                    if (sipsResult.ExitCode == 0)
                        return true;
                }
                catch { }
            }

            // Fallback: Copy original
            File.Copy(sourceIcon, outputPath, true);
            Log.LogMessage(MessageImportance.Low, $"   ⚠️  Image resizing tools not found. Copied original for {Path.GetFileName(outputPath)}");
            return true;
        }

        private string? ReadWebIconFromDirectoryBuildProps(string projectPath)
        {
            string directoryBuildPropsPath = Path.Combine(projectPath, "Directory.Build.props");
            if (!File.Exists(directoryBuildPropsPath))
                return null;

            try
            {
                var doc = System.Xml.Linq.XDocument.Load(directoryBuildPropsPath);
                var webIcon = doc.Descendants("WebIcon").FirstOrDefault()?.Value;
                return webIcon;
            }
            catch
            {
                return null;
            }
        }

        private string? ReadAppVersionFromDirectoryBuildProps(string projectPath)
        {
            string directoryBuildPropsPath = Path.Combine(projectPath, "Directory.Build.props");
            if (!File.Exists(directoryBuildPropsPath))
                return null;

            try
            {
                var doc = System.Xml.Linq.XDocument.Load(directoryBuildPropsPath);
                var appVersion = doc.Descendants("AppVersion").FirstOrDefault()?.Value;
                return appVersion;
            }
            catch
            {
                return null;
            }
        }

        private string? FindIconFile(string projectPath, string iconPath)
        {
            // Try absolute path
            if (Path.IsPathRooted(iconPath) && File.Exists(iconPath))
                return iconPath;

            // Try Assets folder
            string assetsPath = Path.Combine(projectPath, "Assets", iconPath);
            if (File.Exists(assetsPath))
                return assetsPath;

            // Try relative to project
            string relativePath = Path.Combine(projectPath, iconPath);
            if (File.Exists(relativePath))
                return relativePath;

            return null;
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

            // Try to read AppVersion from Directory.Build.props
            string? appVersionFromProps = ReadAppVersionFromDirectoryBuildProps(opts.ProjectPath);
            if (!string.IsNullOrEmpty(appVersionFromProps))
            {
                VERSION_NAME = appVersionFromProps;
                // Extract version code from version string (e.g., "1.2.3" -> "1")
                VERSION_CODE = VERSION_NAME.Split('.')[0];
                Log.LogMessage(MessageImportance.High, $"📋 Using AppVersion from Directory.Build.props: {VERSION_NAME}");
            }
            else
            {
                if (VERSION_CODE == string.Empty)
                {
                    VERSION_CODE = "1";
                }

                if (VERSION_NAME == string.Empty)
                {
                    VERSION_NAME = "1.0";
                }
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