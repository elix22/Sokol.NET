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

                // Set development team if provided
                if (!string.IsNullOrEmpty(opts.DevelopmentTeam))
                {
                    DEVELOPMENT_TEAM = opts.DevelopmentTeam;
                    Log.LogMessage(MessageImportance.High, $"Using development team: {DEVELOPMENT_TEAM}");
                }

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

                // Generate Xcode project
                if (!GenerateXcodeProject(iosDir, projectName))
                    return false;

                // Compile Xcode project if requested
                if (opts.Compile)
                {
                    if (!CompileXcodeProject(iosDir, projectName))
                        return false;

                    // Install on device if requested
                    if (opts.Install)
                    {
                        if (!InstallOnDevice(iosDir, projectName, opts.Run))
                            return false;
                    }
                }
                else
                {
                    Log.LogMessage(MessageImportance.High, "Xcode project generated. To compile manually:");
                    Log.LogMessage(MessageImportance.High, $"cd {Path.Combine(iosDir, "build-xcode-ios-app")} && xcodebuild -configuration Release -sdk iphoneos -arch arm64");
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

                // Build sokol framework using CMake
                var cmakeResult = Cli.Wrap("cmake")
                    .WithArguments($"-G Xcode -DCMAKE_SYSTEM_NAME=iOS -DCMAKE_OSX_DEPLOYMENT_TARGET=14.0 -DCMAKE_OSX_ARCHITECTURES=\"arm64\" {Path.Combine(projectDir, "../../ext")}")
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

                var result = Cli.Wrap("dotnet")
                    .WithArguments($"publish \"{projectFile}\" -r ios-arm64 -c Release -p:BuildAsLibrary=true -p:DefineConstants=\"__IOS__\"")
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

                string libPath = Path.Combine(projectDir, "bin", "Release", "net9.0", "ios-arm64", "publish", $"lib{projectName}.dylib");

                if (!File.Exists(libPath))
                {
                    Log.LogError($"Library file not found: {libPath}");
                    return false;
                }

                // Copy and modify the library
                string destLib = Path.Combine(frameworkDir, projectName);
                File.Copy(libPath, destLib, true);

                // Use install_name_tool to modify the library
                var installNameResult = Cli.Wrap("install_name_tool")
                    .WithArguments($"-rpath @executable_path @executable_path/Frameworks \"{destLib}\"")
                    .WithStandardOutputPipe(PipeTarget.ToDelegate(s => Log.LogMessage(MessageImportance.Normal, s)))
                    .WithStandardErrorPipe(PipeTarget.ToDelegate(s => Log.LogError(s)))
                    .ExecuteBufferedAsync()
                    .GetAwaiter()
                    .GetResult();

                if (installNameResult.ExitCode != 0)
                {
                    Log.LogError($"install_name_tool rpath failed: {installNameResult.StandardError}");
                    return false;
                }

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
                
                // Set orientation based on user preference
                string iosOrientations, ipadOrientations;
                string iosOrientationsPlist, ipadOrientationsPlist;
                switch (opts.ValidatedOrientation)
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
                List<(string Id, string Name)> selectedDevices;

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
                    Log.LogMessage(MessageImportance.High, $"Using specified iOS device: {matchingDevice.Name} ({matchingDevice.Id})");
                }
                else
                {
                    // Use all available devices when multiple are connected
                    selectedDevices = availableDevices;
                    if (availableDevices.Count == 1)
                    {
                        Log.LogMessage(MessageImportance.High, $"Using available iOS device: {availableDevices[0].Name} ({availableDevices[0].Id})");
                    }
                    else
                    {
                        Log.LogMessage(MessageImportance.High, $"Installing on all {availableDevices.Count} connected iOS devices:");
                        foreach (var device in availableDevices)
                        {
                            Log.LogMessage(MessageImportance.Normal, $"  - {device.Name} ({device.Id})");
                        }
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
    }
}