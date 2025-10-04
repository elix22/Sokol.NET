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
//

using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Task = Microsoft.Build.Utilities.Task;
using CliWrap;
using CliWrap.Buffered;


namespace SokolApplicationBuilder
{
    enum ProcessortArchitecture
    {
        Intel=1,
        AppleSilicon
    }

    public class IOSBuildTask : Task
    {
        private readonly Options opts;
        private readonly Dictionary<string, string> envVars = new();

        private string PROJECT_UUID = string.Empty;
        private string PROJECT_NAME = string.Empty;
        private string JAVA_PACKAGE_PATH = string.Empty;
        private string VERSION_CODE = string.Empty;
        private string VERSION_NAME = string.Empty;

        private string URHONET_HOME_PATH = string.Empty;

        private string DEVELOPMENT_TEAM = string.Empty;
        
        // iOS properties from Directory.Build.props
        private string iOSMinVersion = "14.0";
        private string iOSScreenOrientation = "both";
        private bool iOSRequiresFullScreen = false;
        private bool iOSStatusBarHidden = false;
        private string iOSDevelopmentTeam = string.Empty;
        private string iOSIcon = string.Empty;

        private string CLANG_CMD = string.Empty;
        private string AR_CMD = string.Empty;
        private string LIPO_CMD = string.Empty;
        private string IOS_SDK_PATH = string.Empty;

#pragma warning disable CS0414 // The field is assigned but its value is never used
        private ProcessortArchitecture processortArchitecture = ProcessortArchitecture.Intel;
#pragma warning restore CS0414

        public IOSBuildTask(Options opts)
        {
            this.opts = opts;
            Utils.opts = opts;
        }

        public override bool Execute()
        {
            return BuildIOSApp();
        }

        private bool BuildIOSApp()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Log.LogError("Can run only on Apple OSX");
                return false;
            }

            string architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString();

            if (architecture == "Arm64")
            {
                Console.WriteLine("Processor Architecture : Apple Silicon");
                processortArchitecture = ProcessortArchitecture.AppleSilicon;
            }
            else if (architecture == "X64")
            {
                Console.WriteLine("Processor Architecture : Intel x86/x64");
                processortArchitecture = ProcessortArchitecture.Intel;
            }
            else
            {
                Log.LogError($"Unknown architecture: {architecture}");
                return false;
            }

            try
            {
                // Extract project information
                string projectPath = opts.Path;

                // Use smart project selection logic
                string projectName = GetProjectName(projectPath);
                string projectDir = projectPath;

                Log.LogMessage(MessageImportance.High, $"Building iOS app for project: {projectName}");

                // Read iOS properties from Directory.Build.props
                ReadIOSPropertiesFromDirectoryBuildProps(projectDir);

                // Setup development team (with caching support)
                if (!SetupDevelopmentTeam(projectName))
                    return false;

                // Create iOS build directory structure
                string iosDir = Path.Combine(projectDir, "ios");
                Directory.CreateDirectory(iosDir);

                // Build sokol framework
                if (!BuildSokolFramework(iosDir, projectDir))
                    return false;

                // Compile shaders
                if (!CompileShaders(projectDir))
                    return false;

                // Publish .NET project for iOS
                if (!PublishDotNetProject(projectDir, projectName))
                    return false;

                // Create app framework
                if (!CreateAppFramework(iosDir, projectDir, projectName))
                    return false;

                // Copy iOS templates
                if (!CopyIOSTemplates(iosDir, projectName))
                    return false;

                // Process iOS icon if specified
                ProcessIOSIcon(iosDir, projectDir);

                // Generate Xcode project
                if (!GenerateXcodeProject(iosDir, projectName))
                    return false;

                // Compile Xcode project (always compile when building)
                if (!CompileXcodeProject(iosDir, projectName))
                    return false;

                // Install on device if requested
                if (opts.Install)
                {
                    // Always launch app after installation (matching Android behavior)
                    if (!InstallOnDevice(iosDir, projectName, true))
                        return false;
                }

                Log.LogMessage(MessageImportance.High, "iOS build completed successfully!");
                return true;
            }
            catch (Exception ex)
            {
                Log.LogError($"iOS build failed: {ex.Message}");
                return false;
            }
        }

        private bool BuildSokolFramework(string iosDir, string projectDir)
        {
            try
            {
                Log.LogMessage(MessageImportance.High, "Building sokol framework...");

                string sokolDir = Path.Combine(iosDir, "sokol-ios");
                Directory.CreateDirectory(sokolDir);

                // Find ext directory using the same logic as AndroidAppBuilder
                string extDir;
                string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (string.IsNullOrEmpty(homeDir) || !Directory.Exists(homeDir))
                {
                    homeDir = Environment.GetEnvironmentVariable("HOME") ?? "";
                }
                string configFile = Path.Combine(homeDir, ".sokolnet_config", "sokolnet_home");
                if (File.Exists(configFile))
                {
                    string sokolNetHome = File.ReadAllText(configFile).Trim();
                    extDir = Path.GetFullPath(Path.Combine(sokolNetHome, "ext"));
                }
                else
                {
                    extDir = Path.GetFullPath(Path.Combine(projectDir, "..", "..", "..", "ext"));
                }
                
                if (!Directory.Exists(extDir))
                {
                    Log.LogError($"ext directory not found at: {extDir}");
                    return false;
                }

                // Build sokol framework using CMake
                var cmakeResult = Cli.Wrap("cmake")
                    .WithArguments($"-G Xcode -DCMAKE_SYSTEM_NAME=iOS -DCMAKE_OSX_DEPLOYMENT_TARGET={iOSMinVersion} -DCMAKE_OSX_ARCHITECTURES=\"arm64\" \"{extDir}\"")
                    .WithWorkingDirectory(sokolDir)
                    .WithStandardOutputPipe(PipeTarget.ToDelegate(s => Log.LogMessage(MessageImportance.Normal, s)))
                    .WithStandardErrorPipe(PipeTarget.ToDelegate(s => Log.LogError(s)))
                    .ExecuteBufferedAsync()
                    .GetAwaiter()
                    .GetResult();

                if (cmakeResult.ExitCode != 0)
                {
                    Log.LogError($"CMake configure failed: {cmakeResult.StandardError}");
                    return false;
                }

                var buildResult = Cli.Wrap("cmake")
                    .WithArguments("--build . --config Release")
                    .WithWorkingDirectory(sokolDir)
                    .WithStandardOutputPipe(PipeTarget.ToDelegate(s => Log.LogMessage(MessageImportance.Normal, s)))
                    .WithStandardErrorPipe(PipeTarget.ToDelegate(s => Log.LogError(s)))
                    .ExecuteBufferedAsync()
                    .GetAwaiter()
                    .GetResult();

                if (buildResult.ExitCode != 0)
                {
                    Log.LogError($"CMake build failed: {buildResult.StandardError}");
                    return false;
                }

                // Copy framework to frameworks directory
                string frameworksDir = Path.Combine(iosDir, "frameworks");
                Directory.CreateDirectory(frameworksDir);

                string sourceFramework = Path.Combine(sokolDir, "Release-iphoneos", "sokol.framework");
                string destFramework = Path.Combine(frameworksDir, "sokol.framework");

                if (Directory.Exists(sourceFramework))
                {
                    CopyDirectory(sourceFramework, destFramework);
                }

                return true;
            }
            catch (Exception ex)
            {
                Log.LogError($"Failed to build sokol framework: {ex.Message}");
                return false;
            }
        }

        private bool CompileShaders(string projectDir)
        {
            try
            {
                Log.LogMessage(MessageImportance.High, "Compiling shaders...");

                // Get the project name using the same logic as the main build process
                string projectName = GetProjectName(projectDir);
                string projectFile = Path.Combine(projectDir, projectName + ".csproj");

                var result = Cli.Wrap("dotnet")
                    .WithArguments($"msbuild \"{projectFile}\" -t:CompileShaders -p:DefineConstants=\"__IOS__\"")
                    .WithWorkingDirectory(projectDir)
                    .WithStandardOutputPipe(PipeTarget.ToDelegate(s => Log.LogMessage(MessageImportance.Normal, s)))
                    .WithStandardErrorPipe(PipeTarget.ToDelegate(s => Log.LogError(s)))
                    .ExecuteBufferedAsync()
                    .GetAwaiter()
                    .GetResult();

                if (result.ExitCode != 0)
                {
                    Log.LogError($"Shader compilation failed: {result.StandardError}");
                    return false;
                }

                Log.LogMessage(MessageImportance.High, "Shaders compilation completed");
                return true;
            }
            catch (Exception ex)
            {
                Log.LogError($"Shader compilation failed: {ex.Message}");
                return false;
            }
        }

        private bool PublishDotNetProject(string projectDir, string projectName)
        {
            try
            {
                Log.LogMessage(MessageImportance.High, "Publishing .NET project for iOS...");

                string projectFile = Path.Combine(projectDir, projectName + ".csproj");

                string publishArgs = $"publish \"{projectFile}\" -r ios-arm64 -c Release -p:BuildAsLibrary=true -p:DefineConstants=\"__IOS__\"";
                if (!string.IsNullOrEmpty(opts.LinkerFlags))
                {
                    publishArgs += $" -p:LinkerFlags=\"{opts.LinkerFlags}\"";
                }

                var result = Cli.Wrap("dotnet")
                    .WithArguments(publishArgs)
                    .WithWorkingDirectory(projectDir)
                    .WithStandardOutputPipe(PipeTarget.ToDelegate(s => Log.LogMessage(MessageImportance.Normal, s)))
                    .WithStandardErrorPipe(PipeTarget.ToDelegate(s => Log.LogError(s)))
                    .ExecuteBufferedAsync()
                    .GetAwaiter()
                    .GetResult();

                if (result.ExitCode != 0)
                {
                    Log.LogError($"Dotnet publish failed: {result.StandardError}");
                    return false;
                }

                Log.LogMessage(MessageImportance.High, "Dotnet publish completed");
                return true;
            }
            catch (Exception ex)
            {
                Log.LogError($"Dotnet publish failed: {ex.Message}");
                return false;
            }
        }

        private bool CreateAppFramework(string iosDir, string projectDir, string projectName)
        {
            try
            {
                Log.LogMessage(MessageImportance.High, $"Creating {projectName} framework...");

                string frameworksDir = Path.Combine(iosDir, "frameworks");
                string frameworkDir = Path.Combine(frameworksDir, $"{projectName}.framework");
                Directory.CreateDirectory(frameworkDir);

                string libPath = Path.Combine(projectDir, "bin", "Release", "net10.0", "ios-arm64", "publish", $"lib{projectName}.dylib");

                if (!File.Exists(libPath))
                {
                    Log.LogError($"Library file not found: {libPath}");
                    return false;
                }

                // Copy and modify the library
                string destLib = Path.Combine(frameworkDir, projectName);
                File.Copy(libPath, destLib, true);

                // Use install_name_tool to modify the library
                var idResult = Cli.Wrap("install_name_tool")
                    .WithArguments($"-id @rpath/{projectName}.framework/{projectName} \"{destLib}\"")
                    .WithStandardOutputPipe(PipeTarget.ToDelegate(s => Log.LogMessage(MessageImportance.Normal, s)))
                    .WithStandardErrorPipe(PipeTarget.ToDelegate(s => Log.LogError(s)))
                    .ExecuteBufferedAsync()
                    .GetAwaiter()
                    .GetResult();

                if (idResult.ExitCode != 0)
                {
                    Log.LogError($"install_name_tool id failed: {idResult.StandardError}");
                    return false;
                }

                // Copy Info.plist
                string infoPlistSource = Path.Combine(opts.TemplatesPath, "ios", "Info.plist");
                string infoPlistDest = Path.Combine(frameworkDir, "Info.plist");
                File.Copy(infoPlistSource, infoPlistDest, true);

                // Replace placeholders in Info.plist
                string content = File.ReadAllText(infoPlistDest);
                content = content.Replace("TEMPLATE_PROJECT_NAME", projectName);
                File.WriteAllText(infoPlistDest, content);

                return true;
            }
            catch (Exception ex)
            {
                Log.LogError($"Failed to create app framework: {ex.Message}");
                return false;
            }
        }

        private bool CopyIOSTemplates(string iosDir, string projectName)
        {
            try
            {
                Log.LogMessage(MessageImportance.High, "Copying iOS templates...");

                string templatesDir = Path.Combine(opts.TemplatesPath, "ios");

                // Copy CMakeLists.txt
                string cmakeSource = Path.Combine(templatesDir, "CMakeLists.txt");
                string cmakeDest = Path.Combine(iosDir, "CMakeLists.txt");
                File.Copy(cmakeSource, cmakeDest, true);

                // Replace placeholders
                string content = File.ReadAllText(cmakeDest);
                content = content.Replace("TEMPLATE_PROJECT_NAME", projectName);
                
                // Set orientation based on Directory.Build.props or command line option
                string orientation = !string.IsNullOrEmpty(opts.Orientation) && opts.Orientation != "both" 
                    ? opts.ValidatedOrientation 
                    : iOSScreenOrientation;
                
                string iosOrientations, ipadOrientations;
                string iosOrientationsPlist, ipadOrientationsPlist;
                switch (orientation)
                {
                    case "portrait":
                        iosOrientations = "UIInterfaceOrientationPortrait UIInterfaceOrientationPortraitUpsideDown";
                        ipadOrientations = "UIInterfaceOrientationPortrait UIInterfaceOrientationPortraitUpsideDown";
                        iosOrientationsPlist = "\n        <string>UIInterfaceOrientationPortrait</string>\n        <string>UIInterfaceOrientationPortraitUpsideDown</string>";
                        ipadOrientationsPlist = "\n        <string>UIInterfaceOrientationPortrait</string>\n        <string>UIInterfaceOrientationPortraitUpsideDown</string>";
                        break;
                    case "landscape":
                        iosOrientations = "UIInterfaceOrientationLandscapeLeft UIInterfaceOrientationLandscapeRight";
                        ipadOrientations = "UIInterfaceOrientationLandscapeLeft UIInterfaceOrientationLandscapeRight";
                        iosOrientationsPlist = "\n        <string>UIInterfaceOrientationLandscapeLeft</string>\n        <string>UIInterfaceOrientationLandscapeRight</string>";
                        ipadOrientationsPlist = "\n        <string>UIInterfaceOrientationLandscapeLeft</string>\n        <string>UIInterfaceOrientationLandscapeRight</string>";
                        break;
                    case "both":
                    default:
                        iosOrientations = "UIInterfaceOrientationPortrait UIInterfaceOrientationLandscapeLeft UIInterfaceOrientationLandscapeRight";
                        ipadOrientations = "UIInterfaceOrientationPortrait UIInterfaceOrientationPortraitUpsideDown UIInterfaceOrientationLandscapeLeft UIInterfaceOrientationLandscapeRight";
                        iosOrientationsPlist = "\n        <string>UIInterfaceOrientationPortrait</string>\n        <string>UIInterfaceOrientationPortraitUpsideDown</string>\n        <string>UIInterfaceOrientationLandscapeLeft</string>\n        <string>UIInterfaceOrientationLandscapeRight</string>";
                        ipadOrientationsPlist = "\n        <string>UIInterfaceOrientationPortrait</string>\n        <string>UIInterfaceOrientationPortraitUpsideDown</string>\n        <string>UIInterfaceOrientationLandscapeLeft</string>\n        <string>UIInterfaceOrientationLandscapeRight</string>";
                        break;
                }
                
                content = content.Replace("TEMPLATE_IOS_ORIENTATIONS", iosOrientations);
                content = content.Replace("TEMPLATE_IPAD_ORIENTATIONS", ipadOrientations);
                
                File.WriteAllText(cmakeDest, content);

                // Copy and process Info.plist.in
                string plistSource = Path.Combine(templatesDir, "Info.plist.in");
                string plistDest = Path.Combine(iosDir, "Info.plist.in");
                if (File.Exists(plistSource))
                {
                    string plistContent = File.ReadAllText(plistSource);
                    plistContent = plistContent.Replace("TEMPLATE_PROJECT_NAME", projectName);
                    plistContent = plistContent.Replace("@TEMPLATE_IOS_ORIENTATIONS_PLIST@", iosOrientationsPlist);
                    plistContent = plistContent.Replace("@TEMPLATE_IPAD_ORIENTATIONS_PLIST@", ipadOrientationsPlist);
                    
                    // Add UIStatusBarHidden if enabled
                    string statusBarHiddenPlist = iOSStatusBarHidden 
                        ? "\n    <key>UIStatusBarHidden</key>\n    <true/>"
                        : "";
                    plistContent = plistContent.Replace("@TEMPLATE_STATUS_BAR_HIDDEN@", statusBarHiddenPlist);
                    
                    // Add UIViewControllerBasedStatusBarAppearance when status bar is hidden
                    // This tells iOS to use the Info.plist setting instead of view controller override
                    string statusBarAppearancePlist = iOSStatusBarHidden 
                        ? "\n    <key>UIViewControllerBasedStatusBarAppearance</key>\n    <false/>"
                        : "";
                    plistContent = plistContent.Replace("@TEMPLATE_STATUS_BAR_APPEARANCE@", statusBarAppearancePlist);
                    
                    // Add UIRequiresFullScreen if enabled
                    string requiresFullScreenPlist = iOSRequiresFullScreen 
                        ? "\n    <key>UIRequiresFullScreen</key>\n    <true/>"
                        : "";
                    plistContent = plistContent.Replace("@TEMPLATE_REQUIRES_FULLSCREEN@", requiresFullScreenPlist);
                    
                    File.WriteAllText(plistDest, plistContent);
                }

                // Copy main.m
                string mainSource = Path.Combine(templatesDir, "main.m");
                string mainDest = Path.Combine(iosDir, "main.m");
                File.Copy(mainSource, mainDest, true);

                return true;
            }
            catch (Exception ex)
            {
                Log.LogError($"Failed to copy iOS templates: {ex.Message}");
                return false;
            }
        }

        private bool GenerateXcodeProject(string iosDir, string projectName)
        {
            try
            {
                Log.LogMessage(MessageImportance.High, "Generating Xcode project...");

                string buildDir = Path.Combine(iosDir, "build-xcode-ios-app");
                Directory.CreateDirectory(buildDir);

                // Build cmake command with optional development team
                string cmakeCmd = ".. -G Xcode";
                if (!string.IsNullOrEmpty(DEVELOPMENT_TEAM))
                {
                    cmakeCmd += $" -DDEVELOPMENT_TEAM={DEVELOPMENT_TEAM}";
                }

                var result = Cli.Wrap("cmake")
                    .WithArguments(cmakeCmd)
                    .WithWorkingDirectory(buildDir)
                    .WithStandardOutputPipe(PipeTarget.ToDelegate(s => Log.LogMessage(MessageImportance.Normal, s)))
                    .WithStandardErrorPipe(PipeTarget.ToDelegate(s => Log.LogError(s)))
                    .ExecuteBufferedAsync()
                    .GetAwaiter()
                    .GetResult();

                if (result.ExitCode != 0)
                {
                    Log.LogError($"CMake Xcode generation failed: {result.StandardError}");
                    return false;
                }

                Log.LogMessage(MessageImportance.High, "Xcode project generated successfully");
                return true;
            }
            catch (Exception ex)
            {
                Log.LogError($"Failed to generate Xcode project: {ex.Message}");
                return false;
            }
        }

        private bool CompileXcodeProject(string iosDir, string projectName)
        {
            try
            {
                Log.LogMessage(MessageImportance.High, "Compiling Xcode project...");

                string buildDir = Path.Combine(iosDir, "build-xcode-ios-app");

                var result = Cli.Wrap("xcodebuild")
                    .WithArguments($"-target {projectName}-ios-app -configuration Release -sdk iphoneos -arch arm64")
                    .WithWorkingDirectory(buildDir)
                    .WithStandardOutputPipe(PipeTarget.ToDelegate(s => Log.LogMessage(MessageImportance.Normal, s)))
                    .WithStandardErrorPipe(PipeTarget.ToDelegate(s => Log.LogError(s)))
                    .ExecuteBufferedAsync()
                    .GetAwaiter()
                    .GetResult();

                if (result.ExitCode != 0)
                {
                    Log.LogError($"Xcode build failed: {result.StandardError}");
                    return false;
                }

                string appBundlePath = Path.Combine(buildDir, "Release-iphoneos", $"{projectName}-ios-app.app");
                
                // Check if the app bundle exists at the expected location, otherwise look in bin/Release
                if (!Directory.Exists(appBundlePath))
                {
                    string altPath = Path.Combine(buildDir, "bin", "Release", $"{projectName}-ios-app.app");
                    if (Directory.Exists(altPath))
                    {
                        appBundlePath = altPath;
                    }
                }
                
                Log.LogMessage(MessageImportance.High, $"Xcode project compiled successfully!");
                Log.LogMessage(MessageImportance.High, $"App bundle location: {appBundlePath}");

                return true;
            }
            catch (Exception ex)
            {
                Log.LogError($"Failed to compile Xcode project: {ex.Message}");
                return false;
            }
        }

        private bool InstallOnDevice(string iosDir, string projectName, bool runAfterInstall = false)
        {
            try
            {
                Log.LogMessage(MessageImportance.High, "Installing on iOS device...");

                string buildDir = Path.Combine(iosDir, "build-xcode-ios-app");
                string appBundlePath = Path.Combine(buildDir, "Release-iphoneos", $"{projectName}-ios-app.app");

                // Check multiple possible locations for the app bundle
                string[] possiblePaths = new[]
                {
                    appBundlePath,
                    Path.Combine(buildDir, "Release-iphoneos", $"{projectName}-ios-app", $"{projectName}-ios-app.app"),
                    Path.Combine(buildDir, "Release", $"{projectName}-ios-app.app"),
                    Path.Combine(buildDir, $"{projectName}-ios-app.app"),
                    Path.Combine(buildDir, "bin", "Release", $"{projectName}-ios-app.app")
                };

                string? foundPath = null;
                foreach (var path in possiblePaths)
                {
                    if (Directory.Exists(path))
                    {
                        foundPath = path;
                        break;
                    }
                }

                if (foundPath == null)
                {
                    Log.LogError($"App bundle not found at expected locations:");
                    foreach (var path in possiblePaths)
                    {
                        Log.LogError($"  {path}");
                    }

                    // List contents of build directory to see what's actually there
                    Log.LogMessage(MessageImportance.High, $"Contents of build directory {buildDir}:");
                    if (Directory.Exists(buildDir))
                    {
                        foreach (var item in Directory.GetFileSystemEntries(buildDir))
                        {
                            Log.LogMessage(MessageImportance.Normal, $"  {item}");
                        }
                    }
                    return false;
                }

                appBundlePath = foundPath;
                Log.LogMessage(MessageImportance.High, $"Found app bundle at: {appBundlePath}");

                // Check if ios-deploy is available
                var checkResult = Cli.Wrap("which")
                    .WithArguments("ios-deploy")
                    .WithStandardOutputPipe(PipeTarget.ToDelegate(s => Log.LogMessage(MessageImportance.Normal, s)))
                    .WithStandardErrorPipe(PipeTarget.ToDelegate(s => Log.LogError(s)))
                    .ExecuteBufferedAsync()
                    .GetAwaiter()
                    .GetResult();

                if (checkResult.ExitCode != 0)
                {
                    Log.LogError("ios-deploy not found. Install with: npm install -g ios-deploy");
                    return false;
                }

                // Get connected device ID
                var deviceResult = Cli.Wrap("ios-deploy")
                    .WithArguments("-c")
                    .WithStandardOutputPipe(PipeTarget.ToDelegate(s => Log.LogMessage(MessageImportance.Normal, s)))
                    .WithStandardErrorPipe(PipeTarget.ToDelegate(s => Log.LogError(s)))
                    .ExecuteBufferedAsync()
                    .GetAwaiter()
                    .GetResult();

                if (deviceResult.ExitCode != 0)
                {
                    Log.LogError("Failed to get connected devices");
                    return false;
                }

                Log.LogMessage(MessageImportance.Normal, $"Device detection output: {deviceResult.StandardOutput}");

                // Parse all available devices
                var availableDevices = new List<(string Id, string Name)>();
                var lines = deviceResult.StandardOutput.Split('\n');
                foreach (var line in lines)
                {
                    if (line.Contains("Found") && line.Contains("connected through USB"))
                    {
                        // Extract device ID from line like: "[....] Found 00008030-000135660CE3802E (D79AP, iPhone SE 2G, iphoneos, arm64e, 18.6.2, 22G100) a.k.a. 'iPhone' connected through USB."
                        var foundIndex = line.IndexOf("Found");
                        if (foundIndex >= 0)
                        {
                            var afterFound = line.Substring(foundIndex + 6); // Skip "Found "
                            var deviceIdEnd = afterFound.IndexOf(" ");
                            if (deviceIdEnd > 0)
                            {
                                var extractedDeviceId = afterFound.Substring(0, deviceIdEnd).Trim();
                                var deviceName = "Unknown";
                                
                                // Try to extract device name
                                var akaIndex = afterFound.IndexOf("a.k.a.");
                                if (akaIndex >= 0)
                                {
                                    deviceName = afterFound.Substring(akaIndex + 7).Trim('\'', ' ');
                                }
                                
                                availableDevices.Add((extractedDeviceId, deviceName));
                            }
                        }
                    }
                }

                if (availableDevices.Count == 0)
                {
                    Log.LogError("No iOS devices found connected via USB");
                    return false;
                }

                // Select device(s) based on user preference
                List<(string Id, string Name)> selectedDevices = new List<(string Id, string Name)>();

                // Check if user specified a device ID
                if (!string.IsNullOrEmpty(opts.IOSDeviceId))
                {
                    // User specified a specific device
                    var matchingDevice = availableDevices.FirstOrDefault(d => d.Id.Contains(opts.IOSDeviceId) || d.Name.Contains(opts.IOSDeviceId));
                    if (matchingDevice.Id == null)
                    {
                        Log.LogError($"Specified iOS device '{opts.IOSDeviceId}' not found. Available devices:");
                        foreach (var device in availableDevices)
                        {
                            Log.LogError($"  {device.Id} - {device.Name}");
                        }
                        return false;
                    }
                    selectedDevices = new List<(string Id, string Name)> { matchingDevice };
                    Log.LogMessage(MessageImportance.High, $"✅ Using specified iOS device: {matchingDevice.Name} ({matchingDevice.Id})");
                }
                else if (availableDevices.Count == 1)
                {
                    // If only one device, use it automatically
                    selectedDevices.Add(availableDevices[0]);
                    Log.LogMessage(MessageImportance.High, $"✅ Found single device: {availableDevices[0].Name} ({availableDevices[0].Id})");
                }
                else
                {
                    // Multiple devices - handle interactive or automatic selection
                    Log.LogMessage(MessageImportance.High, $"📱 Multiple iOS devices detected ({availableDevices.Count} devices):");
                    Log.LogMessage(MessageImportance.High, "======================================================");

                    for (int i = 0; i < availableDevices.Count; i++)
                    {
                        Log.LogMessage(MessageImportance.High, $"{i + 1}) {availableDevices[i].Name} ({availableDevices[i].Id})");
                    }
                    Log.LogMessage(MessageImportance.High, $"{availableDevices.Count + 1}) All devices");

                    if (opts.Interactive)
                    {
                        // Interactive mode - prompt user for selection
                        Console.WriteLine();
                        int selection = -1;
                        while (selection < 1 || selection > availableDevices.Count + 1)
                        {
                            Console.Write($"Select device (1-{availableDevices.Count + 1}): ");
                            string? input = Console.ReadLine();
                            if (int.TryParse(input, out selection) && selection >= 1 && selection <= availableDevices.Count + 1)
                            {
                                if (selection == availableDevices.Count + 1)
                                {
                                    // All devices selected
                                    selectedDevices = availableDevices.ToList();
                                    Log.LogMessage(MessageImportance.High, $"✅ Selected all devices ({availableDevices.Count} devices)");
                                }
                                else
                                {
                                    selectedDevices.Add(availableDevices[selection - 1]);
                                    Log.LogMessage(MessageImportance.High, $"✅ Selected device: {availableDevices[selection - 1].Name} ({availableDevices[selection - 1].Id})");
                                }
                                break;
                            }
                            else
                            {
                                Console.WriteLine($"❌ Invalid selection. Please enter a number between 1 and {availableDevices.Count + 1}.");
                                selection = -1;
                            }
                        }
                    }
                    else
                    {
                        // Non-interactive mode - use first device with warning
                        selectedDevices.Add(availableDevices[0]);
                        Log.LogMessage(MessageImportance.High, $"⚠️  Using first device: {availableDevices[0].Name} ({availableDevices[0].Id})");
                        Log.LogWarning("Multiple devices found. Using the first one. Use --ios-device <device_id> to specify which device to use, or use --interactive for device selection.");
                    }
                }

                // Install on each selected device
                foreach (var device in selectedDevices)
                {
                    string deviceId = device.Id;
                    string deviceName = device.Name;

                    if (string.IsNullOrEmpty(deviceId))
                    {
                        Log.LogError("No iOS device found connected via USB");
                        return false;
                    }

                Log.LogMessage(MessageImportance.High, $"Installing to device: {deviceName} ({deviceId})");

                // Try to uninstall the app first if it exists (helps with installation errors)
                try
                {
                    Log.LogMessage(MessageImportance.Normal, $"Attempting to uninstall existing app from device: {deviceName}");
                    var uninstallResult = Cli.Wrap("ios-deploy")
                        .WithArguments($"--id {deviceId} --uninstall_only --bundle_id com.elix22.cube-ios-app")
                        .WithStandardOutputPipe(PipeTarget.ToDelegate(s => Log.LogMessage(MessageImportance.Normal, s)))
                        .WithStandardErrorPipe(PipeTarget.ToDelegate(s => Log.LogMessage(MessageImportance.Normal, s)))
                        .ExecuteBufferedAsync()
                        .GetAwaiter()
                        .GetResult();

                    // Note: uninstall may fail if app is not installed, which is fine
                    if (uninstallResult.ExitCode == 0)
                    {
                        Log.LogMessage(MessageImportance.Normal, $"Successfully uninstalled existing app from {deviceName}");
                    }
                }
                catch (Exception ex)
                {
                    Log.LogMessage(MessageImportance.Normal, $"Uninstall attempt failed (app may not be installed): {ex.Message}");
                }

                var installResult = Cli.Wrap("ios-deploy")
                    .WithArguments($"--id {deviceId} --bundle \"{appBundlePath}\" --no-wifi")
                    .WithStandardOutputPipe(PipeTarget.ToDelegate(s => Log.LogMessage(MessageImportance.Normal, s)))
                    .WithStandardErrorPipe(PipeTarget.ToDelegate(s => Log.LogError(s)))
                    .ExecuteBufferedAsync()
                    .GetAwaiter()
                    .GetResult();
                    
                if (installResult.ExitCode != 0)
                {
                    // Check for common installation errors and provide helpful suggestions
                    string errorOutput = installResult.StandardError ?? "";
                    if (errorOutput.Contains("0xe8008001") || errorOutput.Contains("unknown error"))
                    {
                        Log.LogMessage(MessageImportance.Normal, $"Installation failed with error 0xe8008001. This can happen due to:");
                        Log.LogMessage(MessageImportance.Normal, $"  - Trust issues with developer certificate");
                        Log.LogMessage(MessageImportance.Normal, $"  - Provisioning profile problems");
                        Log.LogMessage(MessageImportance.Normal, $"  - App already installed with different signing");
                        Log.LogMessage(MessageImportance.Normal, $"");
                        Log.LogMessage(MessageImportance.Normal, $"Troubleshooting steps:");
                        Log.LogMessage(MessageImportance.Normal, $"  1. On your iOS device, go to Settings > General > VPN & Device Management");
                        Log.LogMessage(MessageImportance.Normal, $"  2. Trust the developer certificate for 'Eli Aloni'");
                        Log.LogMessage(MessageImportance.Normal, $"  3. Try restarting your iOS device");
                        Log.LogMessage(MessageImportance.Normal, $"  4. Try a different USB cable or USB port");
                        Log.LogMessage(MessageImportance.Normal, $"");
                        Log.LogMessage(MessageImportance.Normal, $"Retrying installation...");
                        
                        // Retry installation once
                        installResult = Cli.Wrap("ios-deploy")
                            .WithArguments($"--id {deviceId} --bundle \"{appBundlePath}\" --no-wifi")
                            .WithStandardOutputPipe(PipeTarget.ToDelegate(s => Log.LogMessage(MessageImportance.Normal, s)))
                            .WithStandardErrorPipe(PipeTarget.ToDelegate(s => Log.LogError(s)))
                            .ExecuteBufferedAsync()
                            .GetAwaiter()
                            .GetResult();
                    }
                    
                    if (installResult.ExitCode != 0)
                    {
                        Log.LogError($"Installation failed on {deviceName}: {installResult.StandardError}");
                        return false;
                    }
                }

                    Log.LogMessage(MessageImportance.High, $"App installed successfully on device: {deviceName}!");

                    // Launch the app if requested
                    if (runAfterInstall)
                    {
                        Log.LogMessage(MessageImportance.High, $"Launching app on device: {deviceName} ({deviceId})");

                        // Extract bundle ID from Info.plist
                        string infoPlistPath = Path.Combine(appBundlePath, "Info.plist");
                        string bundleId = "";

                        try
                        {
                            // Use plutil to extract bundle ID from binary plist
                            var plistResult = Cli.Wrap("plutil")
                                .WithArguments($"-extract CFBundleIdentifier raw \"{infoPlistPath}\"")
                                .ExecuteBufferedAsync()
                                .GetAwaiter()
                                .GetResult();

                            if (plistResult.ExitCode == 0)
                            {
                                bundleId = plistResult.StandardOutput.Trim();
                            }
                            else
                            {
                                Log.LogError($"Failed to extract bundle ID from Info.plist: {plistResult.StandardError}");
                                return false;
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.LogError($"Failed to extract bundle ID: {ex.Message}");
                            return false;
                        }

                        // Launch the app using devicectl (more reliable than ios-deploy)
                        var launchResult = Cli.Wrap("xcrun")
                            .WithArguments($"devicectl device process launch --device {deviceId} {bundleId}")
                            .WithStandardOutputPipe(PipeTarget.ToDelegate(s => Log.LogMessage(MessageImportance.Normal, s)))
                            .WithStandardErrorPipe(PipeTarget.ToDelegate(s => Log.LogError(s)))
                            .ExecuteBufferedAsync()
                            .GetAwaiter()
                            .GetResult();

                        if (launchResult.ExitCode != 0)
                        {
                            Log.LogError($"Failed to launch app on {deviceName}: {launchResult.StandardError}");
                            return false;
                        }

                        Log.LogMessage(MessageImportance.High, $"App launched successfully on device: {deviceName}!");
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Log.LogError($"Failed to install on device: {ex.Message}");
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
                throw new FileNotFoundException("No .csproj files found in the specified directory");
            }

            if (csprojFiles.Length == 1)
            {
                // Only one project found, use it
                string projectName = Path.GetFileNameWithoutExtension(csprojFiles[0]);
                Log.LogMessage(MessageImportance.Normal, $"Found single project: {projectName}");
                return projectName;
            }

            // Multiple projects found, try to match with parent folder name
            string parentFolderName = Path.GetFileName(projectPath);
            Log.LogMessage(MessageImportance.Normal, $"Found {csprojFiles.Length} projects, looking for match with parent folder: {parentFolderName}");

            foreach (string csprojFile in csprojFiles)
            {
                string projectName = Path.GetFileNameWithoutExtension(csprojFile);
                if (string.Equals(projectName, parentFolderName, StringComparison.OrdinalIgnoreCase))
                {
                    Log.LogMessage(MessageImportance.Normal, $"Matched project with parent folder name: {projectName}");
                    return projectName;
                }
            }

            // No match found, list available projects and use the first one as fallback
            Log.LogMessage(MessageImportance.Normal, $"No project matched parent folder name '{parentFolderName}'. Available projects:");
            foreach (string csprojFile in csprojFiles)
            {
                string projectName = Path.GetFileNameWithoutExtension(csprojFile);
                Log.LogMessage(MessageImportance.Normal, $"  - {projectName}");
            }

            string fallbackProject = Path.GetFileNameWithoutExtension(csprojFiles[0]);
            Log.LogMessage(MessageImportance.Normal, $"Using first project as fallback: {fallbackProject}");
            return fallbackProject;
        }

        private void ReadIOSPropertiesFromDirectoryBuildProps(string projectPath)
        {
            try
            {
                string directoryBuildPropsPath = Path.Combine(projectPath, "Directory.Build.props");
                if (!File.Exists(directoryBuildPropsPath))
                {
                    Log.LogMessage(MessageImportance.Normal, "No Directory.Build.props found, using default iOS properties");
                    return;
                }

                XDocument doc = XDocument.Load(directoryBuildPropsPath);
                int propertyCount = 0;

                // Read all PropertyGroup elements (not just the first one)
                foreach (var propertyGroup in doc.Root?.Elements("PropertyGroup") ?? Enumerable.Empty<XElement>())
                {
                    // iOS Minimum Version
                    var minVersionElement = propertyGroup.Element("IOSMinVersion");
                    if (minVersionElement != null && !string.IsNullOrEmpty(minVersionElement.Value))
                    {
                        iOSMinVersion = minVersionElement.Value;
                        propertyCount++;
                    }

                    // iOS Screen Orientation
                    var orientationElement = propertyGroup.Element("IOSScreenOrientation");
                    if (orientationElement != null && !string.IsNullOrEmpty(orientationElement.Value))
                    {
                        iOSScreenOrientation = orientationElement.Value.ToLower();
                        propertyCount++;
                    }

                    // iOS Requires Full Screen
                    var fullscreenElement = propertyGroup.Element("IOSRequiresFullScreen");
                    if (fullscreenElement != null && !string.IsNullOrEmpty(fullscreenElement.Value))
                    {
                        iOSRequiresFullScreen = bool.Parse(fullscreenElement.Value);
                        propertyCount++;
                    }

                    // iOS Status Bar Hidden
                    var statusBarElement = propertyGroup.Element("IOSStatusBarHidden");
                    if (statusBarElement != null && !string.IsNullOrEmpty(statusBarElement.Value))
                    {
                        iOSStatusBarHidden = bool.Parse(statusBarElement.Value);
                        propertyCount++;
                    }

                    // iOS Development Team
                    var devTeamElement = propertyGroup.Element("IOSDevelopmentTeam");
                    if (devTeamElement != null && !string.IsNullOrEmpty(devTeamElement.Value))
                    {
                        iOSDevelopmentTeam = devTeamElement.Value;
                        propertyCount++;
                    }

                    // iOS Icon
                    var iconElement = propertyGroup.Element("IOSIcon");
                    if (iconElement != null && !string.IsNullOrEmpty(iconElement.Value))
                    {
                        iOSIcon = iconElement.Value;
                        propertyCount++;
                    }
                }

                if (propertyCount > 0)
                {
                    Log.LogMessage(MessageImportance.High, $"📋 Read {propertyCount} iOS properties from Directory.Build.props");
                    if (!string.IsNullOrEmpty(iOSMinVersion))
                        Log.LogMessage(MessageImportance.High, $"   - IOSMinVersion: {iOSMinVersion}");
                    if (!string.IsNullOrEmpty(iOSScreenOrientation))
                        Log.LogMessage(MessageImportance.High, $"   - IOSScreenOrientation: {iOSScreenOrientation}");
                    Log.LogMessage(MessageImportance.High, $"   - IOSRequiresFullScreen: {iOSRequiresFullScreen}");
                    Log.LogMessage(MessageImportance.High, $"   - IOSStatusBarHidden: {iOSStatusBarHidden}");
                    if (!string.IsNullOrEmpty(iOSDevelopmentTeam))
                        Log.LogMessage(MessageImportance.High, $"   - IOSDevelopmentTeam: {iOSDevelopmentTeam}");
                    if (!string.IsNullOrEmpty(iOSIcon))
                        Log.LogMessage(MessageImportance.High, $"   - IOSIcon: {iOSIcon}");
                }
            }
            catch (Exception ex)
            {
                Log.LogWarning($"Failed to read iOS properties from Directory.Build.props: {ex.Message}");
            }
        }

        private string GetTeamIdCacheFile(string projectName)
        {
            string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string cacheDir = Path.Combine(homeDir, ".sokol-charp-cache");
            Directory.CreateDirectory(cacheDir);
            return Path.Combine(cacheDir, $"{projectName}.teamid");
        }

        private string? GetCachedTeamId(string projectName)
        {
            try
            {
                string cacheFile = GetTeamIdCacheFile(projectName);
                if (File.Exists(cacheFile))
                {
                    string cachedTeamId = File.ReadAllText(cacheFile).Trim();
                    // Validate team ID format (should be 10 alphanumeric characters)
                    if (!string.IsNullOrEmpty(cachedTeamId) && 
                        System.Text.RegularExpressions.Regex.IsMatch(cachedTeamId, @"^[A-Z0-9]{10}$"))
                    {
                        return cachedTeamId;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.LogMessage(MessageImportance.Normal, $"Failed to read cached team ID: {ex.Message}");
            }
            return null;
        }

        private void SaveTeamIdToCache(string projectName, string teamId)
        {
            try
            {
                string cacheFile = GetTeamIdCacheFile(projectName);
                File.WriteAllText(cacheFile, teamId);
                Log.LogMessage(MessageImportance.Normal, "💾 Team ID cached for future use");
            }
            catch (Exception ex)
            {
                Log.LogMessage(MessageImportance.Normal, $"Failed to cache team ID: {ex.Message}");
            }
        }

        private bool SetupDevelopmentTeam(string projectName)
        {
            // If team ID provided via command line, use it
            if (!string.IsNullOrEmpty(opts.DevelopmentTeam))
            {
                DEVELOPMENT_TEAM = opts.DevelopmentTeam;
                Log.LogMessage(MessageImportance.High, $"Using development team from command line: {DEVELOPMENT_TEAM}");
                SaveTeamIdToCache(projectName, DEVELOPMENT_TEAM);
                return true;
            }

            // Try to get team ID from Directory.Build.props
            if (!string.IsNullOrEmpty(iOSDevelopmentTeam))
            {
                DEVELOPMENT_TEAM = iOSDevelopmentTeam;
                Log.LogMessage(MessageImportance.High, $"✅ Using Development Team ID from Directory.Build.props: {DEVELOPMENT_TEAM}");
                SaveTeamIdToCache(projectName, DEVELOPMENT_TEAM);
                return true;
            }

            // Try to get cached team ID
            string? cachedTeamId = GetCachedTeamId(projectName);
            if (cachedTeamId != null)
            {
                DEVELOPMENT_TEAM = cachedTeamId;
                Log.LogMessage(MessageImportance.High, $"✅ Using cached Development Team ID: {DEVELOPMENT_TEAM}");
                string cacheFile = GetTeamIdCacheFile(projectName);
                Log.LogMessage(MessageImportance.Normal, $"   (Delete {cacheFile} to reset)");
                return true;
            }

            // Interactive mode - prompt for team ID
            if (opts.Interactive)
            {
                Console.WriteLine();
                Console.WriteLine("🔑 iOS Development Team ID Required");
                Console.WriteLine("===================================");
                Console.WriteLine("Enter your Apple Developer Team ID (found in developer.apple.com/account):");
                Console.Write("Development Team ID: ");
                
                string? teamId = Console.ReadLine()?.Trim();
                
                if (string.IsNullOrEmpty(teamId))
                {
                    Log.LogError("❌ Development Team ID is required for iOS builds");
                    return false;
                }

                // Validate team ID format
                if (!System.Text.RegularExpressions.Regex.IsMatch(teamId, @"^[A-Z0-9]{10}$"))
                {
                    Log.LogWarning("⚠️  Team ID format looks incorrect (should be 10 alphanumeric characters)");
                    Log.LogWarning("   Continuing anyway, but this may cause build failures...");
                }

                DEVELOPMENT_TEAM = teamId;
                SaveTeamIdToCache(projectName, teamId);
                return true;
            }

            // Non-interactive mode without team ID
            Log.LogError("❌ Development Team ID is required for iOS builds");
            Log.LogError("   Provide it via --development-team flag or use --interactive mode");
            return false;
        }

        private void CopyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);

            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string destFile = Path.Combine(destDir, Path.GetFileName(file));
                File.Copy(file, destFile, true);
            }

            foreach (string subDir in Directory.GetDirectories(sourceDir))
            {
                string destSubDir = Path.Combine(destDir, Path.GetFileName(subDir));
                CopyDirectory(subDir, destSubDir);
            }
        }

        private void ProcessIOSIcon(string iosDir, string projectDir)
        {
            if (string.IsNullOrWhiteSpace(iOSIcon))
            {
                Log.LogMessage(MessageImportance.Normal, "ℹ️  No IOSIcon specified in Directory.Build.props, using default icon");
                return;
            }

            // Find the icon file
            string sourceIconPath = FindIconFile(iOSIcon, projectDir);
            if (string.IsNullOrEmpty(sourceIconPath) || !File.Exists(sourceIconPath))
            {
                Log.LogWarning($"⚠️  iOS icon not found: {iOSIcon}");
                return;
            }

            Log.LogMessage(MessageImportance.High, $"📱 Processing iOS icon: {Path.GetFileName(sourceIconPath)}");

            try
            {
                // Create Assets.xcassets directory structure
                string assetsDir = Path.Combine(iosDir, "Assets.xcassets");
                string appIconDir = Path.Combine(assetsDir, "AppIcon.appiconset");
                Directory.CreateDirectory(appIconDir);

                // iOS icon sizes (iPhone and iPad)
                var iconSizes = new List<(string name, int size, string idiom, string scale)>
                {
                    // iPhone
                    ("icon-20@2x.png", 40, "iphone", "2x"),
                    ("icon-20@3x.png", 60, "iphone", "3x"),
                    ("icon-29@2x.png", 58, "iphone", "2x"),
                    ("icon-29@3x.png", 87, "iphone", "3x"),
                    ("icon-40@2x.png", 80, "iphone", "2x"),
                    ("icon-40@3x.png", 120, "iphone", "3x"),
                    ("icon-60@2x.png", 120, "iphone", "2x"),
                    ("icon-60@3x.png", 180, "iphone", "3x"),
                    
                    // iPad
                    ("icon-20.png", 20, "ipad", "1x"),
                    ("icon-20@2x-ipad.png", 40, "ipad", "2x"),
                    ("icon-29.png", 29, "ipad", "1x"),
                    ("icon-29@2x-ipad.png", 58, "ipad", "2x"),
                    ("icon-40.png", 40, "ipad", "1x"),
                    ("icon-40@2x-ipad.png", 80, "ipad", "2x"),
                    ("icon-76.png", 76, "ipad", "1x"),
                    ("icon-76@2x.png", 152, "ipad", "2x"),
                    ("icon-83.5@2x.png", 167, "ipad", "2x"),
                    
                    // App Store
                    ("icon-1024.png", 1024, "ios-marketing", "1x")
                };

                // Generate Contents.json
                var contentsJson = new StringBuilder();
                contentsJson.AppendLine("{");
                contentsJson.AppendLine("  \"images\" : [");

                for (int i = 0; i < iconSizes.Count; i++)
                {
                    var icon = iconSizes[i];
                    string destIcon = Path.Combine(appIconDir, icon.name);
                    
                    // Resize and save icon
                    ResizeImage(sourceIconPath, destIcon, icon.size, icon.size);
                    
                    // Extract size from icon size
                    string sizeStr = icon.size < 100 
                        ? $"{icon.size}x{icon.size}" 
                        : icon.size == 1024 
                            ? "1024x1024" 
                            : $"{icon.size / (icon.scale == "2x" ? 2 : icon.scale == "3x" ? 3 : 1)}x{icon.size / (icon.scale == "2x" ? 2 : icon.scale == "3x" ? 3 : 1)}";
                    
                    contentsJson.AppendLine("    {");
                    contentsJson.AppendLine($"      \"filename\" : \"{icon.name}\",");
                    contentsJson.AppendLine($"      \"idiom\" : \"{icon.idiom}\",");
                    contentsJson.AppendLine($"      \"scale\" : \"{icon.scale}\",");
                    contentsJson.AppendLine($"      \"size\" : \"{sizeStr}\"");
                    contentsJson.Append("    }");
                    contentsJson.AppendLine(i < iconSizes.Count - 1 ? "," : "");
                    
                    Log.LogMessage(MessageImportance.Normal, $"   ✅ Created {icon.name} ({icon.size}x{icon.size})");
                }

                contentsJson.AppendLine("  ],");
                contentsJson.AppendLine("  \"info\" : {");
                contentsJson.AppendLine("    \"author\" : \"xcode\",");
                contentsJson.AppendLine("    \"version\" : 1");
                contentsJson.AppendLine("  }");
                contentsJson.AppendLine("}");

                // Write Contents.json
                string contentsJsonPath = Path.Combine(appIconDir, "Contents.json");
                File.WriteAllText(contentsJsonPath, contentsJson.ToString());

                Log.LogMessage(MessageImportance.High, "✅ iOS icon processed successfully");
            }
            catch (Exception ex)
            {
                Log.LogWarning($"⚠️  Failed to process iOS icon: {ex.Message}");
            }
        }

        private string FindIconFile(string iconPath, string projectDir)
        {
            // If it's already an absolute path and exists, use it
            if (Path.IsPathRooted(iconPath) && File.Exists(iconPath))
            {
                return iconPath;
            }

            // Check in Assets folder first
            string assetsPath = Path.Combine(projectDir, "Assets", iconPath);
            if (File.Exists(assetsPath))
            {
                return assetsPath;
            }

            // Check relative to project path
            string relativePath = Path.Combine(projectDir, iconPath);
            if (File.Exists(relativePath))
            {
                return relativePath;
            }

            return null;
        }

        private void ResizeImage(string sourcePath, string destPath, int width, int height)
        {
            // Try using ImageMagick first (convert or magick command)
            bool resized = false;

            try
            {
                // Try 'magick' command (ImageMagick 7+)
                var magickResult = Cli.Wrap("magick")
                    .WithArguments($"\"{sourcePath}\" -resize {width}x{height}! \"{destPath}\"")
                    .WithValidation(CommandResultValidation.None)
                    .ExecuteBufferedAsync()
                    .GetAwaiter()
                    .GetResult();

                if (magickResult.ExitCode == 0)
                {
                    resized = true;
                }
            }
            catch
            {
                // ImageMagick not available, try 'convert' command
                try
                {
                    var convertResult = Cli.Wrap("convert")
                        .WithArguments($"\"{sourcePath}\" -resize {width}x{height}! \"{destPath}\"")
                        .WithValidation(CommandResultValidation.None)
                        .ExecuteBufferedAsync()
                        .GetAwaiter()
                        .GetResult();

                    if (convertResult.ExitCode == 0)
                    {
                        resized = true;
                    }
                }
                catch { }
            }

            // If ImageMagick is not available, try using sips (macOS only)
            if (!resized && RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                try
                {
                    // Copy file first
                    File.Copy(sourcePath, destPath, true);
                    
                    var sipsResult = Cli.Wrap("sips")
                        .WithArguments($"-z {height} {width} \"{destPath}\"")
                        .WithValidation(CommandResultValidation.None)
                        .ExecuteBufferedAsync()
                        .GetAwaiter()
                        .GetResult();

                    if (sipsResult.ExitCode == 0)
                    {
                        resized = true;
                    }
                }
                catch { }
            }

            // If no tool is available, just copy the file
            if (!resized)
            {
                Log.LogWarning($"⚠️  Image resizing tools not found (ImageMagick or sips). Copying original icon.");
                File.Copy(sourcePath, destPath, true);
            }
        }
    }
}