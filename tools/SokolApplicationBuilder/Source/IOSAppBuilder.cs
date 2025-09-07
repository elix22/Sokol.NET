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

        private ProcessortArchitecture processortArchitecture = ProcessortArchitecture.Intel;

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
                string projectName = Path.GetFileName(projectPath);
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
                        if (!InstallOnDevice(iosDir, projectName))
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

                var result = Cli.Wrap("dotnet")
                    .WithArguments("msbuild -t:CompileShaders -p:DefineConstants=\"__IOS__\"")
                    .WithWorkingDirectory(projectDir)
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

                var result = Cli.Wrap("dotnet")
                    .WithArguments("publish -r ios-arm64 -c Release -p:BuildAsLibrary=true -p:DefineConstants=\"__IOS__\"")
                    .WithWorkingDirectory(projectDir)
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
                File.WriteAllText(cmakeDest, content);

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
                string cmakeCmd = "cmake .. -G Xcode";
                if (!string.IsNullOrEmpty(DEVELOPMENT_TEAM))
                {
                    cmakeCmd += $" -DDEVELOPMENT_TEAM={DEVELOPMENT_TEAM}";
                }

                var result = Cli.Wrap("cmake")
                    .WithArguments(cmakeCmd)
                    .WithWorkingDirectory(buildDir)
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
                    .WithArguments("-configuration Release -sdk iphoneos -arch arm64")
                    .WithWorkingDirectory(buildDir)
                    .ExecuteBufferedAsync()
                    .GetAwaiter()
                    .GetResult();

                if (result.ExitCode != 0)
                {
                    Log.LogError($"Xcode build failed: {result.StandardError}");
                    return false;
                }

                string appBundlePath = Path.Combine(buildDir, "Release-iphoneos", $"{projectName}-ios-app.app");
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

        private bool InstallOnDevice(string iosDir, string projectName)
        {
            try
            {
                Log.LogMessage(MessageImportance.High, "Installing on iOS device...");

                string buildDir = Path.Combine(iosDir, "build-xcode-ios-app");
                string appBundlePath = Path.Combine(buildDir, "Release-iphoneos", $"{projectName}-ios-app.app");

                if (!Directory.Exists(appBundlePath))
                {
                    Log.LogError($"App bundle not found: {appBundlePath}");
                    return false;
                }

                // Check if ios-deploy is available
                var checkResult = Cli.Wrap("which")
                    .WithArguments("ios-deploy")
                    .ExecuteBufferedAsync()
                    .GetAwaiter()
                    .GetResult();

                if (checkResult.ExitCode != 0)
                {
                    Log.LogError("ios-deploy not found. Install with: npm install -g ios-deploy");
                    return false;
                }

                var installResult = Cli.Wrap("ios-deploy")
                    .WithArguments($"--id $(ios-deploy -c | head -1 | cut -d' ' -f2) --bundle \"{appBundlePath}\" --no-wifi")
                    .ExecuteBufferedAsync()
                    .GetAwaiter()
                    .GetResult();

                if (installResult.ExitCode != 0)
                {
                    Log.LogError($"Installation failed: {installResult.StandardError}");
                    return false;
                }

                Log.LogMessage(MessageImportance.High, "App installed successfully on device!");
                return true;
            }
            catch (Exception ex)
            {
                Log.LogError($"Failed to install on device: {ex.Message}");
                return false;
            }
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