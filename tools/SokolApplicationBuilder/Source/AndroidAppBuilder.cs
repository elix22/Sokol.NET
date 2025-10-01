// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Task = Microsoft.Build.Utilities.Task;
using CliWrap;
using CliWrap.Buffered;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SokolApplicationBuilder
{
    public class AndroidBuildTask : Task
    {


        Options opts;
        Dictionary<string, string> envVarsDict = new();

        string PROJECT_UUID = string.Empty;
        string PROJECT_NAME = string.Empty;
        string JAVA_PACKAGE_PATH = string.Empty;
        string VERSION_CODE = string.Empty;
        string VERSION_NAME = string.Empty;

        string URHONET_HOME_PATH = string.Empty;


        public AndroidBuildTask(Options opts)
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
            return BuildAndroidAppBundle();
        }

        bool BuildAndroidAppBundle()
        {
            try
            {
                // Parse command line arguments (similar to shell script)
                bool installApp = opts.Install;
                string buildType = !string.IsNullOrEmpty(opts.Type) ? opts.Type.ToLower() : "debug";
                bool buildAAB = opts.SubTask?.ToLower() == "aab"; // Check if AAB build is requested

                Log.LogMessage(MessageImportance.High, $"Build type: {buildType}");
                Log.LogMessage(MessageImportance.High, $"Build format: {(buildAAB ? "AAB" : "APK")}");
                if (installApp)
                    Log.LogMessage(MessageImportance.High, $"Will install {(buildAAB ? "AAB" : "APK")} on device after build");

                // Get app name
                string appName = GetAppName();
                PROJECT_NAME = appName; // Store for use in other methods
                Log.LogMessage(MessageImportance.High, $"Configuring Android app for: {appName}");

                // Copy Android template
                CopyAndroidTemplate();

                // Configure Android app
                ConfigureAndroidApp(appName);

                // Compile shaders
                CompileShaders();

                // Publish .NET assemblies for different architectures
                PublishAssemblies();

                // Build Android app (APK or AAB)
                if (buildAAB)
                    BuildAndroidAAB(appName, buildType);
                else
                    BuildAndroidApp(appName, buildType);

                // Sign if release
                if (buildType == "release")
                {
                    if (buildAAB)
                        SignReleaseAAB();
                    else
                        SignReleaseApp();
                }

                // Install if requested
                if (installApp)
                {
                    if (buildAAB)
                        InstallAABOnDevice(appName, buildType);
                    else
                        InstallOnDevice(appName, buildType);
                }

                return true;
            }
            catch (Exception ex)
            {
                Log.LogError($"Build failed: {ex.Message}");
                return false;
            }
        }



        string GetAppName()
        {
            // If project name is explicitly provided via options, use it
            if (!string.IsNullOrEmpty(opts.ProjectName))
            {
                Log.LogMessage(MessageImportance.Normal, $"Using explicitly specified project name: {opts.ProjectName}");
                return opts.ProjectName;
            }

            // Find all .csproj files in the project directory
            string[] csprojFiles = Directory.GetFiles(opts.ProjectPath, "*.csproj");

            if (csprojFiles.Length == 0)
            {
                Log.LogError($"No .csproj files found in directory: {opts.ProjectPath}");
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
            string parentFolderName = Path.GetFileName(opts.ProjectPath);
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

        void CopyAndroidTemplate()
        {
            // Get the path to the templates folder in the output directory
            string templatesPath = Path.Combine(Path.GetDirectoryName(typeof(AndroidBuildTask).Assembly.Location), "templates", "Android");
            string androidDest = Path.Combine(opts.ProjectPath, "Android");

            Log.LogMessage(MessageImportance.Normal, $"Copying Android template from: {templatesPath}");
            Log.LogMessage(MessageImportance.Normal, $"Copying Android template to: {androidDest}");

            if (Directory.Exists(templatesPath))
            {
                Log.LogMessage(MessageImportance.Normal, "Android template directory found, copying...");
                Utils.CopyDirectory(templatesPath, androidDest);
                Log.LogMessage(MessageImportance.Normal, "Android template copied successfully");
            }
            else
            {
                Log.LogError($"Android template not found at: {templatesPath}");
                Log.LogError($"Assembly location: {typeof(AndroidBuildTask).Assembly.Location}");
            }
        }

        void ConfigureAndroidApp(string appName)
        {
            string androidPath = Path.Combine(opts.ProjectPath, "Android", "native-activity");

            // Update AndroidManifest.xml
            string manifestPath = Path.Combine(androidPath, "app", "src", "main", "AndroidManifest.xml");
            if (File.Exists(manifestPath))
            {
                string content = File.ReadAllText(manifestPath);
                // Replace the @string/app_name references with the actual app name (without extra quotes)
                content = content.Replace("@string/app_name", appName);

                // Configure orientation
                string androidOrientation = opts.ValidatedOrientation switch
                {
                    "portrait" => "portrait",
                    "landscape" => "landscape",
                    "both" => "unspecified", // Android uses "unspecified" to allow both orientations
                    _ => "unspecified"
                };

                // Replace orientation placeholder
                content = content.Replace("ANDROID_ORIENTATION_PLACEHOLDER", androidOrientation);

                File.WriteAllText(manifestPath, content);
            }

            // Update build.gradle
            string buildGradlePath = Path.Combine(androidPath, "app", "build.gradle");
            if (File.Exists(buildGradlePath))
            {
                string content = File.ReadAllText(buildGradlePath);
                content = content.Replace("applicationId = 'com.example.native_activity'", $"applicationId = 'com.elix22.{appName}'");
                content = content.Replace("namespace 'com.example.native_activity'", $"namespace 'com.elix22.{appName}'");
                File.WriteAllText(buildGradlePath, content);
            }

            // Update strings.xml
            string stringsPath = Path.Combine(androidPath, "app", "src", "main", "res", "values", "strings.xml");
            if (File.Exists(stringsPath))
            {
                string content = File.ReadAllText(stringsPath);
                content = content.Replace("NativeActivity", appName);
                File.WriteAllText(stringsPath, content);
            }

            // Update CMakeLists.txt
            string cmakePath = Path.Combine(androidPath, "app", "src", "main", "cpp", "CMakeLists.txt");
            if (File.Exists(cmakePath))
            {
                string content = File.ReadAllText(cmakePath);
                // Replace APP_NAME placeholder but preserve ANativeActivity_onCreate function name
                content = content.Replace("${APP_NAME}", appName);
                content = content.Replace("lib${APP_NAME}.so", $"lib{appName}.so");
                // Set EXT_ROOT_DIR to absolute path
                string extPath;
                string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (string.IsNullOrEmpty(homeDir) || !Directory.Exists(homeDir))
                {
                    homeDir = Environment.GetEnvironmentVariable("HOME") ?? "";
                }
                string configFile = Path.Combine(homeDir, ".sokolnet_config", "sokolnet_home");
                if (File.Exists(configFile))
                {
                    string sokolNetHome = File.ReadAllText(configFile).Trim();
                    extPath = Path.GetFullPath(Path.Combine(sokolNetHome, "ext"));
                }
                else
                {
                    extPath = Path.GetFullPath(Path.Combine(opts.ProjectPath, "..", "..", "..", "ext"));
                }
                content = content.Replace("set(EXT_ROOT_DIR \"../../../../../../../../ext\")", $"set(EXT_ROOT_DIR \"{extPath}\")");
                // Don't replace ANativeActivity_onCreate - it must remain unchanged
                File.WriteAllText(cmakePath, content);
            }

            // Update Java/Kotlin files
            foreach (string file in Directory.GetFiles(androidPath, "*.java", SearchOption.AllDirectories)
                .Concat(Directory.GetFiles(androidPath, "*.kt", SearchOption.AllDirectories)))
            {
                string content = File.ReadAllText(file);
                content = content.Replace("NativeActivity", appName);
                File.WriteAllText(file, content);
            }
        }

        void CompileShaders()
        {
            Log.LogMessage(MessageImportance.High, "Compiling shaders...");

            string projectFile = Path.Combine(opts.ProjectPath, $"{PROJECT_NAME}.csproj");

            var result = Cli.Wrap("dotnet")
                .WithArguments($"msbuild \"{projectFile}\" -t:CompileShaders -p:DefineConstants=\"__ANDROID__\"")
                .WithWorkingDirectory(opts.ProjectPath)
                .WithStandardOutputPipe(PipeTarget.ToDelegate(s => Log.LogMessage(MessageImportance.Normal, s)))
                .WithStandardErrorPipe(PipeTarget.ToDelegate(s => Log.LogError(s)))
                .ExecuteAsync()
                .GetAwaiter()
                .GetResult();

            Log.LogMessage(MessageImportance.High, $"Shaders compilation completed with exit code: {result.ExitCode}");
        }

        void PublishAssemblies()
        {
            string[] architectures = { "linux-bionic-arm64", "linux-bionic-arm", "linux-bionic-x64" };

            foreach (string arch in architectures)
            {
                Log.LogMessage(MessageImportance.High, $"Publishing for {arch}...");

                try
                {
                    string projectFile = Path.Combine(opts.ProjectPath, $"{PROJECT_NAME}.csproj");

                    var result = Cli.Wrap("dotnet")
                        .WithArguments($"publish \"{projectFile}\" -r {arch} -p:BuildAsLibrary=true -p:DisableUnsupportedError=true -p:PublishAotUsingRuntimePack=true -p:RemoveSections=true -p:DefineConstants=\"__ANDROID__\" --verbosity quiet")
                        .WithWorkingDirectory(opts.ProjectPath)
                        .WithStandardOutputPipe(PipeTarget.ToDelegate(s => Log.LogMessage(MessageImportance.Normal, s)))
                        .WithStandardErrorPipe(PipeTarget.ToDelegate(s => Log.LogError(s)))
                        .ExecuteAsync()
                        .GetAwaiter()
                        .GetResult();

                    if (result.ExitCode == 0)
                    {
                        Log.LogMessage(MessageImportance.High, $"Publishing for {arch} completed successfully");
                        
                        // Copy the published library to the Android libs directory
                        string publishDir = Path.Combine(opts.ProjectPath, "bin", "Release", "net9.0", arch, "publish");
                        string libsDir = Path.Combine(opts.ProjectPath, "Android", "native-activity", "app", "src", "main", "libs");
                        string abiName = arch switch
                        {
                            "linux-bionic-arm64" => "arm64-v8a",
                            "linux-bionic-arm" => "armeabi-v7a",
                            "linux-bionic-x64" => "x86_64",
                            _ => arch
                        };
                        string libsAbiDir = Path.Combine(libsDir, abiName);
                        string sourceLib = Path.Combine(publishDir, $"lib{GetAppName()}.so");
                        string destLib = Path.Combine(libsAbiDir, $"lib{GetAppName()}.so");
                        
                        if (File.Exists(sourceLib))
                        {
                            Directory.CreateDirectory(libsAbiDir);
                            File.Copy(sourceLib, destLib, true);
                            Log.LogMessage(MessageImportance.Normal, $"Copied {sourceLib} to {destLib}");
                        }
                        else
                        {
                            Log.LogWarning($"Published library not found: {sourceLib}");
                        }
                    }
                    else
                    {
                        Log.LogWarning($"Publishing for {arch} completed with exit code: {result.ExitCode}");
                    }
                }
                catch (Exception ex)
                {
                    Log.LogError($"Publishing for {arch} failed: {ex.Message}");
                    throw;
                }
            }
        }

        void BuildAndroidApp(string appName, string buildType)
        {
            string androidPath = Path.Combine(opts.ProjectPath, "Android", "native-activity");

            Log.LogMessage(MessageImportance.Normal, $"Android path: {androidPath}");
            Log.LogMessage(MessageImportance.Normal, $"Android path exists: {Directory.Exists(androidPath)}");
            Log.LogMessage(MessageImportance.Normal, $"Gradlew path: {Path.Combine(androidPath, "gradlew")}");
            Log.LogMessage(MessageImportance.Normal, $"Gradlew exists: {File.Exists(Path.Combine(androidPath, "gradlew"))}");

            if (buildType == "release")
            {
                Log.LogMessage(MessageImportance.High, "Building release APK...");
                string gradlewPath = Path.Combine(androidPath, "gradlew");
                Log.LogMessage(MessageImportance.Normal, $"Using gradlew path: {gradlewPath}");

                var result = Cli.Wrap(gradlewPath)
                    .WithArguments($"assembleRelease")
                    .WithWorkingDirectory(androidPath)
                    .WithStandardOutputPipe(PipeTarget.ToDelegate(s => Log.LogMessage(MessageImportance.Normal, s)))
                    .WithStandardErrorPipe(PipeTarget.ToDelegate(s => Log.LogError(s)))
                    .ExecuteAsync()
                    .GetAwaiter()
                    .GetResult();

                Log.LogMessage(MessageImportance.High, $"Release APK build completed with exit code: {result.ExitCode}");
            }
            else
            {
                Log.LogMessage(MessageImportance.High, "Building debug APK...");
                string gradlewPath = Path.Combine(androidPath, "gradlew");
                Log.LogMessage(MessageImportance.Normal, $"Using gradlew path: {gradlewPath}");

                var result = Cli.Wrap(gradlewPath)
                    .WithArguments($"assembleDebug")
                    .WithWorkingDirectory(androidPath)
                    .WithStandardOutputPipe(PipeTarget.ToDelegate(s => Log.LogMessage(MessageImportance.Normal, s)))
                    .WithStandardErrorPipe(PipeTarget.ToDelegate(s => Log.LogError(s)))
                    .ExecuteAsync()
                    .GetAwaiter()
                    .GetResult();

                Log.LogMessage(MessageImportance.High, $"Debug APK build completed with exit code: {result.ExitCode}");
            }
        }

        void BuildAndroidAAB(string appName, string buildType)
        {
            string androidPath = Path.Combine(opts.ProjectPath, "Android", "native-activity");

            Log.LogMessage(MessageImportance.Normal, $"Android path: {androidPath}");
            Log.LogMessage(MessageImportance.Normal, $"Android path exists: {Directory.Exists(androidPath)}");
            Log.LogMessage(MessageImportance.Normal, $"Gradlew path: {Path.Combine(androidPath, "gradlew")}");
            Log.LogMessage(MessageImportance.Normal, $"Gradlew exists: {File.Exists(Path.Combine(androidPath, "gradlew"))}");

            if (buildType == "release")
            {
                Log.LogMessage(MessageImportance.High, "Building release AAB...");
                string gradlewPath = Path.Combine(androidPath, "gradlew");
                Log.LogMessage(MessageImportance.Normal, $"Using gradlew path: {gradlewPath}");

                var result = Cli.Wrap(gradlewPath)
                    .WithArguments($"bundleRelease -PcmakeArgs=\"-DAPP_NAME={appName}\"")
                    .WithWorkingDirectory(androidPath)
                    .WithStandardOutputPipe(PipeTarget.ToDelegate(s => Log.LogMessage(MessageImportance.Normal, s)))
                    .WithStandardErrorPipe(PipeTarget.ToDelegate(s => Log.LogError(s)))
                    .ExecuteAsync()
                    .GetAwaiter()
                    .GetResult();

                Log.LogMessage(MessageImportance.High, $"Release AAB build completed with exit code: {result.ExitCode}");
            }
            else
            {
                Log.LogMessage(MessageImportance.High, "Building debug AAB...");
                string gradlewPath = Path.Combine(androidPath, "gradlew");
                Log.LogMessage(MessageImportance.Normal, $"Using gradlew path: {gradlewPath}");

                var result = Cli.Wrap(gradlewPath)
                    .WithArguments($"bundleDebug -PcmakeArgs=\"-DAPP_NAME={appName}\"")
                    .WithWorkingDirectory(androidPath)
                    .WithStandardOutputPipe(PipeTarget.ToDelegate(s => Log.LogMessage(MessageImportance.Normal, s)))
                    .WithStandardErrorPipe(PipeTarget.ToDelegate(s => Log.LogError(s)))
                    .ExecuteAsync()
                    .GetAwaiter()
                    .GetResult();

                Log.LogMessage(MessageImportance.High, $"Debug AAB build completed with exit code: {result.ExitCode}");
            }
        }

        string EnsureDebugKeystore()
        {
            // Create debug keystore if it doesn't exist
            string debugKeystore = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".android", "debug.keystore");
            if (!File.Exists(debugKeystore))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(debugKeystore));
                Log.LogMessage(MessageImportance.High, "Creating debug keystore...");
                var keystoreResult = Cli.Wrap("keytool")
                    .WithArguments($"-genkey -v -keystore \"{debugKeystore}\" -storepass android -alias androiddebugkey -keypass android -keyalg RSA -keysize 2048 -validity 10000 -dname \"CN=Android Debug,O=Android,C=US\"")
                    .WithStandardOutputPipe(PipeTarget.ToDelegate(s => Log.LogMessage(MessageImportance.Normal, s)))
                    .WithStandardErrorPipe(PipeTarget.ToDelegate(s => Log.LogError(s)))
                    .ExecuteAsync()
                    .GetAwaiter()
                    .GetResult();

                Log.LogMessage(MessageImportance.High, $"Keystore creation completed with exit code: {keystoreResult.ExitCode}");
            }
            return debugKeystore;
        }

        void SignReleaseApp()
        {
            string androidPath = Path.Combine(opts.ProjectPath, "Android", "native-activity");
            string unsignedApkPath = Path.Combine(androidPath, "app", "build", "outputs", "apk", "release", "app-release-unsigned.apk");

            if (!File.Exists(unsignedApkPath))
            {
                Log.LogError("Unsigned release APK not found!");
                return;
            }

            Log.LogMessage(MessageImportance.High, "Signing release APK...");

            string debugKeystore = EnsureDebugKeystore();

            // Sign the APK
            string signedApkPath = Path.Combine(androidPath, "app", "build", "outputs", "apk", "release", "app-release.apk");

            // Don't copy the unsigned APK yet - let apksigner do it with --out parameter
            // Copy unsigned APK to final location before signing
            if (File.Exists(signedApkPath))
                File.Delete(signedApkPath);

            // Try to sign with apksigner first (better for APKs with native libraries)
            bool signingSuccess = false;
            
            try
            {
                // Find apksigner in Android SDK
                var stringBuilder = new System.Text.StringBuilder();
                var findApksignerResult = Cli.Wrap("find")
                    .WithArguments(new[] { "/Users/elialoni/Library/Android/sdk", "-name", "apksigner", "-type", "f" })
                    .WithStandardOutputPipe(PipeTarget.ToStringBuilder(stringBuilder))
                    .WithValidation(CommandResultValidation.None) // Don't fail on exit code
                    .ExecuteAsync()
                    .GetAwaiter()
                    .GetResult();
                
                string apksignerPath = stringBuilder.ToString().Trim();
                if (!string.IsNullOrEmpty(apksignerPath))
                {
                    Log.LogMessage(MessageImportance.High, $"Using apksigner: {apksignerPath}");
                    
                    var apksignerResult = Cli.Wrap(apksignerPath.Split('\n')[0]) // Use first line if multiple
                        .WithArguments($"sign --ks \"{debugKeystore}\" --ks-pass pass:android --key-pass pass:android --out \"{signedApkPath}\" \"{unsignedApkPath}\"")
                        .WithStandardOutputPipe(PipeTarget.ToDelegate(s => Log.LogMessage(MessageImportance.Normal, s)))
                        .WithStandardErrorPipe(PipeTarget.ToDelegate(s => Log.LogError(s)))
                        .ExecuteAsync()
                        .GetAwaiter()
                        .GetResult();
                    
                    if (apksignerResult.ExitCode == 0)
                    {
                        signingSuccess = true;
                        Log.LogMessage(MessageImportance.High, "APK signed successfully with apksigner!");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.LogWarning($"apksigner failed: {ex.Message}");
            }
            
            // Fallback to jarsigner if apksigner failed
            if (!signingSuccess)
            {
                Log.LogMessage(MessageImportance.High, "Falling back to jarsigner...");
                
                // Copy unsigned APK to final location before signing with jarsigner
                File.Copy(unsignedApkPath, signedApkPath, true);
                
                var jarsignerResult = Cli.Wrap("jarsigner")
                    .WithArguments($"-keystore \"{debugKeystore}\" -storepass android -keypass android -digestalg SHA-256 -sigalg SHA256withRSA \"{signedApkPath}\" androiddebugkey")
                    .WithStandardOutputPipe(PipeTarget.ToDelegate(s => Log.LogMessage(MessageImportance.Normal, s)))
                    .WithStandardErrorPipe(PipeTarget.ToDelegate(s => Log.LogError(s)))
                    .ExecuteAsync()
                    .GetAwaiter()
                    .GetResult();
                
                if (jarsignerResult.ExitCode == 0)
                {
                    signingSuccess = true;
                    Log.LogMessage(MessageImportance.High, "APK signed successfully with jarsigner!");
                }
            }
            
            if (signingSuccess)
            {
                // Remove unsigned APK
                if (File.Exists(unsignedApkPath))
                    File.Delete(unsignedApkPath);
            }
            else
            {
                Log.LogError("Failed to sign APK with both apksigner and jarsigner!");
            }
        }

        void SignReleaseAAB()
        {
            string androidPath = Path.Combine(opts.ProjectPath, "Android", "native-activity");
            string aabPath = Path.Combine(androidPath, "app", "build", "outputs", "bundle", "release", "app-release.aab");

            if (!File.Exists(aabPath))
            {
                Log.LogError("Release AAB not found!");
                return;
            }

            Log.LogMessage(MessageImportance.High, "Signing release AAB...");

            string debugKeystore = EnsureDebugKeystore();

            // Sign the AAB with jarsigner (AAB files use JAR signing)
            var jarsignerResult = Cli.Wrap("jarsigner")
                .WithArguments($"-keystore \"{debugKeystore}\" -storepass android -keypass android -digestalg SHA-256 -sigalg SHA256withRSA \"{aabPath}\" androiddebugkey")
                .WithStandardOutputPipe(PipeTarget.ToDelegate(s => Log.LogMessage(MessageImportance.Normal, s)))
                .WithStandardErrorPipe(PipeTarget.ToDelegate(s => Log.LogError(s)))
                .ExecuteAsync()
                .GetAwaiter()
                .GetResult();

            if (jarsignerResult.ExitCode == 0)
            {
                Log.LogMessage(MessageImportance.High, "✅ AAB signed successfully with jarsigner!");
            }
            else
            {
                Log.LogError("❌ Warning: Failed to sign AAB. Using unsigned AAB.");
            }
        }

        // Helper method to get connected devices and select one interactively or automatically
        List<string> SelectAndroidDevice()
        {
            // Check if adb is available and get device list
            string deviceListOutput = "";
            try
            {
                var stringBuilder = new System.Text.StringBuilder();
                var adbCheckResult = Cli.Wrap("adb")
                    .WithArguments("devices")
                    .WithStandardOutputPipe(PipeTarget.ToDelegate(line => stringBuilder.AppendLine(line)))
                    .WithStandardErrorPipe(PipeTarget.ToDelegate(s => Log.LogError(s)))
                    .ExecuteAsync()
                    .GetAwaiter()
                    .GetResult();

                deviceListOutput = stringBuilder.ToString();
                Log.LogMessage(MessageImportance.Normal, $"ADB devices output: {deviceListOutput}");

                if (adbCheckResult.ExitCode != 0)
                {
                    Log.LogError("Failed to get device list from ADB.");
                    return new List<string>();
                }
            }
            catch (Exception ex)
            {
                Log.LogError($"ADB not found or failed: {ex.Message}");
                return new List<string>();
            }

            // Parse device list and get device info
            var devices = new List<(string id, string manufacturer, string model)>();
            var lines = deviceListOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                if (!line.Contains("List of devices") && !string.IsNullOrWhiteSpace(line))
                {
                    var parts = line.Split('\t', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2 && parts[1] == "device")
                    {
                        string deviceId = parts[0];
                        string manufacturer = "";
                        string model = "";

                        // Try to get device info
                        try
                        {
                            var modelBuilder = new System.Text.StringBuilder();
                            var modelResult = Cli.Wrap("adb")
                                .WithArguments($"-s {deviceId} shell getprop ro.product.model")
                                .WithStandardOutputPipe(PipeTarget.ToDelegate(line => modelBuilder.AppendLine(line)))
                                .ExecuteAsync()
                                .GetAwaiter()
                                .GetResult();
                            model = modelBuilder.ToString().Trim();

                            var mfgBuilder = new System.Text.StringBuilder();
                            var mfgResult = Cli.Wrap("adb")
                                .WithArguments($"-s {deviceId} shell getprop ro.product.manufacturer")
                                .WithStandardOutputPipe(PipeTarget.ToDelegate(line => mfgBuilder.AppendLine(line)))
                                .ExecuteAsync()
                                .GetAwaiter()
                                .GetResult();
                            manufacturer = mfgBuilder.ToString().Trim();
                        }
                        catch
                        {
                            // Ignore errors getting device info
                        }

                        devices.Add((deviceId, manufacturer, model));
                    }
                }
            }

            if (devices.Count == 0)
            {
                Log.LogError("No Android devices found. Please connect a device and enable USB debugging.");
                return new List<string>();
            }

            List<string> selectedDeviceIds = new List<string>();

            // Check if user specified a device ID
            if (!string.IsNullOrEmpty(opts.DeviceId))
            {
                if (devices.Any(d => d.id == opts.DeviceId))
                {
                    selectedDeviceIds.Add(opts.DeviceId);
                    Log.LogMessage(MessageImportance.High, $"Using specified device: {opts.DeviceId}");
                    return selectedDeviceIds;
                }
                else
                {
                    Log.LogError($"Specified device '{opts.DeviceId}' not found. Available devices:");
                    foreach (var device in devices)
                    {
                        string deviceInfo = !string.IsNullOrEmpty(device.manufacturer) && !string.IsNullOrEmpty(device.model)
                            ? $"{device.id} ({device.manufacturer} {device.model})"
                            : device.id;
                        Log.LogError($"  {deviceInfo}");
                    }
                    return new List<string>();
                }
            }

            // If only one device, use it automatically
            if (devices.Count == 1)
            {
                selectedDeviceIds.Add(devices[0].id);
                string deviceInfo = !string.IsNullOrEmpty(devices[0].manufacturer) && !string.IsNullOrEmpty(devices[0].model)
                    ? $"{devices[0].id} ({devices[0].manufacturer} {devices[0].model})"
                    : devices[0].id;
                Log.LogMessage(MessageImportance.High, $"✅ Found single device: {deviceInfo}");
                return selectedDeviceIds;
            }

            // Multiple devices - handle interactive or automatic selection
            Log.LogMessage(MessageImportance.High, $"📱 Multiple devices detected ({devices.Count} devices):");
            Log.LogMessage(MessageImportance.High, "======================================================");

            for (int i = 0; i < devices.Count; i++)
            {
                string deviceInfo = !string.IsNullOrEmpty(devices[i].manufacturer) && !string.IsNullOrEmpty(devices[i].model)
                    ? $"{devices[i].id} ({devices[i].manufacturer} {devices[i].model})"
                    : devices[i].id;
                Log.LogMessage(MessageImportance.High, $"{i + 1}) {deviceInfo}");
            }
            Log.LogMessage(MessageImportance.High, $"{devices.Count + 1}) All devices");

            if (opts.Interactive)
            {
                // Interactive mode - prompt user for selection
                Console.WriteLine();
                int selection = -1;
                while (selection < 1 || selection > devices.Count + 1)
                {
                    Console.Write($"Select device (1-{devices.Count + 1}): ");
                    string? input = Console.ReadLine();
                    if (int.TryParse(input, out selection) && selection >= 1 && selection <= devices.Count + 1)
                    {
                        if (selection == devices.Count + 1)
                        {
                            // All devices selected
                            selectedDeviceIds = devices.Select(d => d.id).ToList();
                            Log.LogMessage(MessageImportance.High, $"✅ Selected all devices ({devices.Count} devices)");
                        }
                        else
                        {
                            selectedDeviceIds.Add(devices[selection - 1].id);
                            Log.LogMessage(MessageImportance.High, $"✅ Selected device: {devices[selection - 1].id}");
                        }
                        break;
                    }
                    else
                    {
                        Console.WriteLine($"❌ Invalid selection. Please enter a number between 1 and {devices.Count + 1}.");
                        selection = -1;
                    }
                }
            }
            else
            {
                // Non-interactive mode - use first device with warning
                selectedDeviceIds.Add(devices[0].id);
                Log.LogMessage(MessageImportance.High, $"⚠️  Using first device: {devices[0].id}");
                Log.LogWarning("Multiple devices found. Using the first one. Use --device <device_id> to specify which device to use, or use --interactive for device selection.");
            }

            return selectedDeviceIds;
        }

        void InstallOnDevice(string appName, string buildType)
        {
            Log.LogMessage(MessageImportance.High, "Installing on Android device...");

            // Get selected device(s) using helper method
            List<string> selectedDeviceIds = SelectAndroidDevice();
            if (selectedDeviceIds == null || selectedDeviceIds.Count == 0)
            {
                return; // Error already logged by SelectAndroidDevice
            }

            string androidPath = Path.Combine(opts.ProjectPath, "Android", "native-activity");

            // Find APK file
            string apkPath = "";
            if (buildType == "release")
            {
                apkPath = Path.Combine(androidPath, "app", "build", "outputs", "apk", "release", "app-release.apk");
                if (!File.Exists(apkPath))
                    apkPath = Path.Combine(androidPath, "app", "build", "outputs", "apk", "release", "app-release-unsigned.apk");
            }
            else
            {
                apkPath = Path.Combine(androidPath, "app", "build", "outputs", "apk", "debug", "app-debug.apk");
            }

            if (!File.Exists(apkPath))
            {
                Log.LogError("APK file not found!");
                return;
            }

            // Install APK on all selected devices
            int successCount = 0;
            int failCount = 0;
            
            foreach (var selectedDeviceId in selectedDeviceIds)
            {
                if (selectedDeviceIds.Count > 1)
                {
                    Log.LogMessage(MessageImportance.High, $"\n📱 Installing on device: {selectedDeviceId}");
                }
                
                var installResult = Cli.Wrap("adb")
                    .WithArguments($"-s {selectedDeviceId} install -r \"{apkPath}\"")
                    .WithStandardOutputPipe(PipeTarget.ToDelegate(s => Log.LogMessage(MessageImportance.Normal, s)))
                    .WithStandardErrorPipe(PipeTarget.ToDelegate(s => Log.LogError(s)))
                    .ExecuteAsync()
                    .GetAwaiter()
                    .GetResult();

                if (installResult.ExitCode == 0)
                {
                    Log.LogMessage(MessageImportance.High, $"✅ APK installed successfully on {selectedDeviceId}!");
                    successCount++;

                    // Try to launch the app on selected device
                    string packageName = $"com.elix22.{appName}";

                    try
                    {
                        var launchResult = Cli.Wrap("adb")
                            .WithArguments($"-s {selectedDeviceId} shell monkey -p {packageName} -c android.intent.category.LAUNCHER 1")
                            .WithStandardOutputPipe(PipeTarget.ToDelegate(s => Log.LogMessage(MessageImportance.Normal, s)))
                            .WithStandardErrorPipe(PipeTarget.ToDelegate(s => Log.LogError(s)))
                            .ExecuteAsync()
                            .GetAwaiter()
                            .GetResult();

                        Log.LogMessage(MessageImportance.High, $"App launch completed with exit code: {launchResult.ExitCode}");
                        Log.LogMessage(MessageImportance.High, $"✅ App launched successfully on {selectedDeviceId}!");
                    }
                    catch
                    {
                        Log.LogWarning($"Could not launch app automatically on {selectedDeviceId}. Package: {packageName}");
                    }
                }
                else
                {
                    Log.LogError($"❌ Failed to install APK on device {selectedDeviceId}!");
                    failCount++;
                }
            }
            
            if (selectedDeviceIds.Count > 1)
            {
                Log.LogMessage(MessageImportance.High, $"\n📊 Installation Summary: {successCount} succeeded, {failCount} failed (Total: {selectedDeviceIds.Count} devices)");
            }
        }

        void InstallAABOnDevice(string appName, string buildType)
        {
            Log.LogMessage(MessageImportance.High, "Installing AAB on Android device...");

            // Get selected device(s) using helper method
            List<string> selectedDeviceIds = SelectAndroidDevice();
            if (selectedDeviceIds == null || selectedDeviceIds.Count == 0)
            {
                return; // Error already logged by SelectAndroidDevice
            }

            string androidPath = Path.Combine(opts.ProjectPath, "Android", "native-activity");

            // Find AAB file
            string aabPath = "";
            if (buildType == "release")
            {
                aabPath = Path.Combine(androidPath, "app", "build", "outputs", "bundle", "release", "app-release.aab");
            }
            else
            {
                aabPath = Path.Combine(androidPath, "app", "build", "outputs", "bundle", "debug", "app-debug.aab");
            }

            if (!File.Exists(aabPath))
            {
                Log.LogError($"AAB file not found at: {aabPath}");
                return;
            }

            Log.LogMessage(MessageImportance.High, $"Found AAB: {aabPath}");

            // Convert AAB to APK and install using bundletool
            Log.LogMessage(MessageImportance.High, "Converting AAB to APK for device installation...");

            // Find bundletool
            string bundletoolPath = FindBundletool();

            if (string.IsNullOrEmpty(bundletoolPath))
            {
                Log.LogError("bundletool not found. AAB files cannot be directly installed on devices.");
                Log.LogError("To install AAB files, you need to:");
                Log.LogError("1. Install bundletool: https://developer.android.com/tools/bundletool");
                Log.LogError("2. Or upload to Google Play Console for testing");
                return;
            }

            Log.LogMessage(MessageImportance.High, $"Using bundletool: {bundletoolPath}");

            // Install AAB on all selected devices
            int successCount = 0;
            int failCount = 0;

            foreach (var selectedDeviceId in selectedDeviceIds)
            {
                if (selectedDeviceIds.Count > 1)
                {
                    Log.LogMessage(MessageImportance.High, $"\n📱 Installing on device: {selectedDeviceId}");
                }

                // Create a temporary directory for the conversion
                string tempDir = Path.Combine(Path.GetTempPath(), $"aab_install_{selectedDeviceId}_{Guid.NewGuid()}");
                Directory.CreateDirectory(tempDir);

                try
                {
                    // Get device specifications for bundletool
                    string deviceSpecFile = Path.Combine(tempDir, "device-spec.json");
                    
                    // Get device ABI
                    var abiBuilder = new System.Text.StringBuilder();
                    Cli.Wrap("adb")
                        .WithArguments($"-s {selectedDeviceId} shell getprop ro.product.cpu.abi")
                        .WithStandardOutputPipe(PipeTarget.ToStringBuilder(abiBuilder))
                        .ExecuteAsync()
                        .GetAwaiter()
                        .GetResult();
                    string deviceAbi = abiBuilder.ToString().Trim();

                    // Get SDK version
                    var sdkBuilder = new System.Text.StringBuilder();
                    Cli.Wrap("adb")
                        .WithArguments($"-s {selectedDeviceId} shell getprop ro.build.version.sdk")
                        .WithStandardOutputPipe(PipeTarget.ToStringBuilder(sdkBuilder))
                        .ExecuteAsync()
                        .GetAwaiter()
                        .GetResult();
                    string sdkVersion = sdkBuilder.ToString().Trim();

                    // Create device spec file
                    string deviceSpec = $@"{{
  ""supportedAbis"": [""{deviceAbi}""],
  ""supportedLocales"": [""en-US""],
  ""deviceFeatures"": [],
  ""glExtensions"": [],
  ""screenDensity"": 420,
  ""sdkVersion"": {sdkVersion}
}}";
                    File.WriteAllText(deviceSpecFile, deviceSpec);

                    // Convert AAB to APK using bundletool (universal mode for all architectures)
                    string apksPath = Path.Combine(tempDir, "app.apks");
                    
                    var bundletoolResult = Cli.Wrap("java")
                        .WithArguments($"-jar \"{bundletoolPath}\" build-apks --bundle=\"{aabPath}\" --output=\"{apksPath}\" --mode=universal")
                        .WithStandardOutputPipe(PipeTarget.ToDelegate(s => Log.LogMessage(MessageImportance.Normal, s)))
                        .WithStandardErrorPipe(PipeTarget.ToDelegate(s => Log.LogError(s)))
                        .ExecuteAsync()
                        .GetAwaiter()
                        .GetResult();

                    if (bundletoolResult.ExitCode != 0)
                    {
                        Log.LogError($"❌ Failed to convert AAB to APK using bundletool for {selectedDeviceId}");
                        failCount++;
                        continue;
                    }

                    // Extract the universal APK from the .apks file
                    string apkPath = Path.Combine(tempDir, "universal.apk");
                    
                    var unzipResult = Cli.Wrap("unzip")
                        .WithArguments($"-q \"{apksPath}\" -d \"{tempDir}\"")
                        .WithStandardOutputPipe(PipeTarget.ToDelegate(s => Log.LogMessage(MessageImportance.Normal, s)))
                        .WithStandardErrorPipe(PipeTarget.ToDelegate(s => Log.LogError(s)))
                        .ExecuteAsync()
                        .GetAwaiter()
                        .GetResult();

                    if (!File.Exists(apkPath))
                    {
                        Log.LogError($"❌ Failed to extract universal APK from bundle for {selectedDeviceId}");
                        failCount++;
                        continue;
                    }

                    Log.LogMessage(MessageImportance.High, "✅ AAB converted to APK successfully!");

                    // Install the converted APK
                    Log.LogMessage(MessageImportance.High, "Installing APK on device...");
                    var installResult = Cli.Wrap("adb")
                        .WithArguments($"-s {selectedDeviceId} install -r \"{apkPath}\"")
                        .WithStandardOutputPipe(PipeTarget.ToDelegate(s => Log.LogMessage(MessageImportance.Normal, s)))
                        .WithStandardErrorPipe(PipeTarget.ToDelegate(s => Log.LogError(s)))
                        .ExecuteAsync()
                        .GetAwaiter()
                        .GetResult();

                    if (installResult.ExitCode == 0)
                    {
                        Log.LogMessage(MessageImportance.High, $"✅ AAB installed successfully on {selectedDeviceId}!");
                        successCount++;

                        // Try to launch the app
                        string packageName = $"com.elix22.{appName}";
                        Log.LogMessage(MessageImportance.High, $"Launching app (package: {packageName})...");

                        try
                        {
                            var launchResult = Cli.Wrap("adb")
                                .WithArguments($"-s {selectedDeviceId} shell monkey -p {packageName} -c android.intent.category.LAUNCHER 1")
                                .WithStandardOutputPipe(PipeTarget.ToDelegate(s => Log.LogMessage(MessageImportance.Normal, s)))
                                .WithStandardErrorPipe(PipeTarget.ToDelegate(s => Log.LogError(s)))
                                .ExecuteAsync()
                                .GetAwaiter()
                                .GetResult();

                            Log.LogMessage(MessageImportance.High, $"✅ App launched successfully on {selectedDeviceId}!");
                        }
                        catch
                        {
                            Log.LogWarning($"Could not launch app automatically on {selectedDeviceId}. Package: {packageName}");
                        }
                    }
                    else
                    {
                        Log.LogError($"❌ Error: Failed to install APK on device {selectedDeviceId}!");
                        failCount++;
                    }
                }
                finally
                {
                    // Clean up temporary files
                    try
                    {
                        if (Directory.Exists(tempDir))
                            Directory.Delete(tempDir, true);
                    }
                    catch (Exception ex)
                    {
                        Log.LogWarning($"Failed to clean up temporary directory: {ex.Message}");
                    }
                }
            }

            if (selectedDeviceIds.Count > 1)
            {
                Log.LogMessage(MessageImportance.High, $"\n📊 Installation Summary: {successCount} succeeded, {failCount} failed (Total: {selectedDeviceIds.Count} devices)");
            }
        }

        string FindBundletool()
        {
            // First check local tools folder
            string localBundletool = Path.Combine(opts.ProjectPath, "..", "..", "tools", "bundletool.jar");
            if (File.Exists(localBundletool))
                return Path.GetFullPath(localBundletool);

            // Then check Android SDK
            string androidSdk = Environment.GetEnvironmentVariable("ANDROID_SDK_ROOT") 
                ?? Environment.GetEnvironmentVariable("ANDROID_HOME")
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Android", "sdk");

            if (Directory.Exists(androidSdk))
            {
                var bundletoolFiles = Directory.GetFiles(androidSdk, "bundletool*.jar", SearchOption.AllDirectories);
                if (bundletoolFiles.Length > 0)
                    return bundletoolFiles[0];
            }

            return null;
        }



        List<string> GetAndroidPermissions()
        {
            List<string> permissions = new List<string>();

            // Add default permissions
            permissions.Add("android.permission.INTERNET");
            permissions.Add("android.permission.ACCESS_NETWORK_STATE");

            // Add permissions from environment variables if they exist
            string extraPermissions = GetEnvValue("ANDROID_PERMISSIONS");
            if (!string.IsNullOrEmpty(extraPermissions))
            {
                var extraPerms = SplitToList(extraPermissions);
                permissions.AddRange(extraPerms);
            }

            return permissions;
        }

        void ParseEnvironmentVars(string project_vars_path)
        {
            string[] project_vars = project_vars_path.FileReadAllLines();

            foreach (string v in project_vars)
            {
                if (v.Contains('#') || v == string.Empty) continue;
                string tr = v.Trim();
                if (tr.StartsWith("export"))
                {
                    tr = tr.Replace("export", "");
                    string[] vars = tr.Split('=', 2);
                    envVarsDict[vars[0].Trim()] = vars[1].Trim();
                }
            }
        }

        string GetEnvValue(string key)
        {
            string value = string.Empty;
            if (envVarsDict.TryGetValue(key, out var val))
            {
                value = val;
                value = value.Replace("\'", "");
            }
            return value.Trim();
        }


        List<string> SplitToList(string value)
        {
            List<string> result = new List<string>();
            if (value != string.Empty)
            {
                value = value.Replace("\'", "").Replace(",", "").Trim().Trim('(').Trim(')').Trim();

                string[] entries = value.Split(' ');
                foreach (var entry in entries)
                {
                    if (entry == string.Empty) continue;
                    result.Add(entry);
                }
            }
            return result;
        }

        void CreateAndroidManifest()
        {
            string AndroidManifest = Path.Combine(opts.OutputPath, "Android/app/src/main/AndroidManifest.xml");

            AndroidManifest.DeleteFile();
            AndroidManifest.AppendTextLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            AndroidManifest.AppendTextLine($"<manifest xmlns:android=\"http://schemas.android.com/apk/res/android\" package=\"{PROJECT_UUID}\">");

            List<string> permissions = GetAndroidPermissions();

            foreach (var i in permissions)
            {
                AndroidManifest.AppendTextLine($"   <uses-permission android:name=\"{i}\"/>");
            }


            if (File.Exists(Path.Combine(opts.ProjectPath, "platform/android/manifest/AndroidManifest.xml")))
            {
                string extra = File.ReadAllText(Path.Combine(opts.ProjectPath, "platform/android/manifest/AndroidManifest.xml"));
                AndroidManifest.AppendText(extra);
            }

            AndroidManifest.AppendTextLine("   <application android:allowBackup=\"true\" android:icon=\"@mipmap/ic_launcher\" android:label=\"@string/app_name\" android:roundIcon=\"@mipmap/ic_launcher_round\" android:supportsRtl=\"true\" android:theme=\"@style/AppTheme\">");

            string GAD_APPLICATION_ID = GetEnvValue("GAD_APPLICATION_ID");
            if (GAD_APPLICATION_ID != string.Empty)
            {
                AndroidManifest.AppendTextLine($"      <meta-data android:name=\"com.google.android.gms.ads.APPLICATION_ID\" android:value=\"{GAD_APPLICATION_ID}\"/>");
            }


            AndroidManifest.AppendTextLine($"      <activity android:name=\".MainActivity\" android:exported=\"true\">");
            AndroidManifest.AppendTextLine($"          <intent-filter>");
            AndroidManifest.AppendTextLine($"              <action android:name=\"android.intent.action.MAIN\" />");
            AndroidManifest.AppendTextLine($"              <category android:name=\"android.intent.category.LAUNCHER\" />");
            AndroidManifest.AppendTextLine($"          </intent-filter>");

            if (File.Exists(Path.Combine(opts.ProjectPath, "platform/android/manifest/IntentFilters.xml")))
            {
                string extra = File.ReadAllText(Path.Combine(opts.ProjectPath, "platform/android/manifest/IntentFilters.xml"));
                AndroidManifest.AppendText(extra);
            }

            AndroidManifest.AppendTextLine($"      </activity>");

            string SCREEN_ORIENTATION = opts.ValidatedOrientation;
            if (SCREEN_ORIENTATION == string.Empty)
            {
                SCREEN_ORIENTATION = "both";
            }
            if (SCREEN_ORIENTATION != "landscape" && SCREEN_ORIENTATION != "portrait" && SCREEN_ORIENTATION != "both")
            {
                SCREEN_ORIENTATION = "both";
            }

            // Convert orientation to Android manifest format
            string androidOrientation = SCREEN_ORIENTATION switch
            {
                "portrait" => "portrait",
                "landscape" => "landscape",
                "both" => "unspecified", // Android uses "unspecified" to allow both orientations
                _ => "unspecified"
            };

            AndroidManifest.AppendTextLine($"      <activity android:name=\".UrhoMainActivity\" android:exported=\"true\" android:configChanges=\"keyboardHidden|orientation|screenSize\" android:screenOrientation=\"{androidOrientation}\" android:theme=\"@android:style/Theme.NoTitleBar.Fullscreen\"/>");

            if (File.Exists(Path.Combine(opts.ProjectPath, "platform/android/manifest/Activities.xml")))
            {
                string extra = File.ReadAllText(Path.Combine(opts.ProjectPath, "platform/android/manifest/Activities.xml"));
                AndroidManifest.AppendText(extra);
            }

            AndroidManifest.AppendTextLine($"   </application>");
            AndroidManifest.AppendTextLine($"</manifest>");


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

