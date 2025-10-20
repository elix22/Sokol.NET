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
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Task = Microsoft.Build.Utilities.Task;
using CliWrap;
using CliWrap.Buffered;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml.Linq;
using SkiaSharp;

namespace SokolApplicationBuilder
{
    public class AndroidBuildTask : Task
    {


        Options opts;
        Dictionary<string, string> envVarsDict = new();
        Dictionary<string, string> androidProperties = new();

        string PROJECT_UUID = string.Empty;
        string PROJECT_NAME = string.Empty;
        string JAVA_PACKAGE_PATH = string.Empty;
        string VERSION_CODE = string.Empty;
        string VERSION_NAME = string.Empty;

        string URHONET_HOME_PATH = string.Empty;
        string DETECTED_NDK_VERSION = string.Empty;


        public AndroidBuildTask(Options opts)
        {
            this.opts = opts;
            Utils.opts = opts;
        }

        private string GetGradlewScriptName()
        {
            // On Windows, use gradlew.bat; on Unix-like systems, use gradlew
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "gradlew.bat" : "gradlew";
        }

        private string GetSokolNetHome()
        {
            string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrEmpty(homeDir) || !Directory.Exists(homeDir))
            {
                homeDir = Environment.GetEnvironmentVariable("HOME") ?? "";
            }
            string configFile = Path.Combine(homeDir, ".sokolnet_config", "sokolnet_home");
            if (File.Exists(configFile))
            {
                return File.ReadAllText(configFile).Trim();
            }
            // Fallback to relative path
            return Path.GetFullPath(Path.Combine(opts.ProjectPath, "..", "..", ".."));
        }

        private string FindJava17OrHigher()
        {
            // Check JAVA_HOME first
            string javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
            if (!string.IsNullOrEmpty(javaHome) && Directory.Exists(javaHome))
            {
                int version = GetJavaVersion(javaHome);
                if (version >= 17)
                {
                    Log.LogMessage(MessageImportance.High, $"✅ Using Java {version} from JAVA_HOME: {javaHome}");
                    return javaHome;
                }
                else if (version > 0)
                {
                    Log.LogWarning($"⚠️  JAVA_HOME points to Java {version}, but Android Gradle Plugin requires Java 17+");
                }
            }

            // Search common Java installation locations
            var searchPaths = new List<string>();
            
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Windows common locations
                searchPaths.Add(@"C:\Program Files\Microsoft");
                searchPaths.Add(@"C:\Program Files\Eclipse Adoptium");
                searchPaths.Add(@"C:\Program Files\Java");
                searchPaths.Add(@"C:\Program Files (x86)\Java");
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                searchPaths.Add(Path.Combine(localAppData, "Programs"));
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // macOS common locations
                searchPaths.Add("/Library/Java/JavaVirtualMachines");
                searchPaths.Add("/usr/local/Cellar/openjdk");
                string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                searchPaths.Add(Path.Combine(homeDir, ".sdkman/candidates/java"));
            }
            else // Linux
            {
                searchPaths.Add("/usr/lib/jvm");
                searchPaths.Add("/usr/java");
                string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                searchPaths.Add(Path.Combine(homeDir, ".sdkman/candidates/java"));
            }

            var javaInstallations = new List<(string path, int version)>();

            foreach (string searchPath in searchPaths)
            {
                if (!Directory.Exists(searchPath))
                    continue;

                try
                {
                    foreach (string dir in Directory.GetDirectories(searchPath, "*", SearchOption.TopDirectoryOnly))
                    {
                        int version = GetJavaVersion(dir);
                        if (version >= 17)
                        {
                            javaInstallations.Add((dir, version));
                            Log.LogMessage(MessageImportance.Normal, $"Found Java {version} at: {dir}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.LogMessage(MessageImportance.Low, $"Failed to search {searchPath}: {ex.Message}");
                }
            }

            if (javaInstallations.Count > 0)
            {
                // Prefer Java 17, then latest version
                var selected = javaInstallations
                    .OrderByDescending(j => j.version == 17 ? 1000 : j.version)
                    .First();
                
                Log.LogMessage(MessageImportance.High, $"✅ Selected Java {selected.version}: {selected.path}");
                return selected.path;
            }

            Log.LogWarning("⚠️  No Java 17+ installation found. Gradle may fail.");
            return null;
        }

        private int GetJavaVersion(string javaHome)
        {
            try
            {
                // Look for release file (present in modern JDKs)
                string releaseFile = Path.Combine(javaHome, "release");
                if (File.Exists(releaseFile))
                {
                    string content = File.ReadAllText(releaseFile);
                    var match = System.Text.RegularExpressions.Regex.Match(content, @"JAVA_VERSION=""(\d+)");
                    if (match.Success)
                    {
                        return int.Parse(match.Groups[1].Value);
                    }
                }

                // Try running java -version
                string javaExe = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    ? Path.Combine(javaHome, "bin", "java.exe")
                    : Path.Combine(javaHome, "bin", "java");

                if (File.Exists(javaExe))
                {
                    var result = Cli.Wrap(javaExe)
                        .WithArguments("-version")
                        .WithValidation(CommandResultValidation.None)
                        .ExecuteBufferedAsync()
                        .GetAwaiter()
                        .GetResult();

                    string output = result.StandardError + result.StandardOutput;
                    var match = System.Text.RegularExpressions.Regex.Match(output, @"version ""(\d+)");
                    if (match.Success)
                    {
                        return int.Parse(match.Groups[1].Value);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.LogMessage(MessageImportance.Low, $"Failed to get Java version from {javaHome}: {ex.Message}");
            }

            return 0;
        }

        private string GetAndroidSdkPath()
        {
            // Try to find Android SDK location from environment variables
            string androidSdk = Environment.GetEnvironmentVariable("ANDROID_SDK_ROOT") 
                ?? Environment.GetEnvironmentVariable("ANDROID_HOME")
                ?? Environment.GetEnvironmentVariable("ANDROID_SDK");

            // If not found in environment, try common locations
            if (string.IsNullOrEmpty(androidSdk) || !Directory.Exists(androidSdk))
            {
                string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    androidSdk = Path.Combine(homeDir, "AppData", "Local", "Android", "Sdk");
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    androidSdk = Path.Combine(homeDir, "Library", "Android", "sdk");
                }
                else // Linux
                {
                    androidSdk = Path.Combine(homeDir, "Android", "Sdk");
                }
            }

            return androidSdk;
        }

        private string FindBestAndroidNDK(string currentNdkHome = null, string currentNdkRoot = null)
        {
            // First, check if environment variables point to a suitable NDK (≥25)
            string envNdk = currentNdkHome ?? currentNdkRoot;
            if (!string.IsNullOrEmpty(envNdk) && Directory.Exists(envNdk))
            {
                string sourcePropsFile = Path.Combine(envNdk, "source.properties");
                if (File.Exists(sourcePropsFile))
                {
                    try
                    {
                        string content = File.ReadAllText(sourcePropsFile);
                        var match = System.Text.RegularExpressions.Regex.Match(content, @"Pkg\.Revision\s*=\s*(\d+)\.(\d+)\.(\d+)");
                        if (match.Success)
                        {
                            int majorVersion = int.Parse(match.Groups[1].Value);
                            string fullVersion = $"{match.Groups[1].Value}.{match.Groups[2].Value}.{match.Groups[3].Value}";
                            if (majorVersion >= 25)
                            {
                                DETECTED_NDK_VERSION = fullVersion;
                                Log.LogMessage(MessageImportance.High, $"✅ Using NDK version {fullVersion} from environment variables (meets minimum: 25)");
                                return envNdk;
                            }
                            else
                            {
                                Log.LogWarning($"⚠️  Environment NDK version {fullVersion} is too old (< 25). Searching for a newer NDK...");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.LogWarning($"Failed to parse NDK version from environment: {ex.Message}");
                    }
                }
            }

            // Environment NDK not suitable, search for best available NDK
            Log.LogMessage(MessageImportance.Normal, "Searching Android SDK for suitable NDK (≥25)...");
            
            // Use the helper method to get Android SDK path
            string androidSdk = GetAndroidSdkPath();

            if (!Directory.Exists(androidSdk))
            {
                Log.LogWarning($"Android SDK not found at: {androidSdk}");
                return null;
            }

            string ndkDir = Path.Combine(androidSdk, "ndk");
            if (!Directory.Exists(ndkDir))
            {
                Log.LogWarning($"Android NDK directory not found at: {ndkDir}");
                return null;
            }

            // Find all NDK versions
            var ndkVersions = new List<(string path, int majorVersion, string fullVersion)>();
            
            foreach (string ndkPath in Directory.GetDirectories(ndkDir))
            {
                string versionDirName = Path.GetFileName(ndkPath);
                string sourcePropsFile = Path.Combine(ndkPath, "source.properties");
                
                if (File.Exists(sourcePropsFile))
                {
                    try
                    {
                        string content = File.ReadAllText(sourcePropsFile);
                        var match = System.Text.RegularExpressions.Regex.Match(content, @"Pkg\.Revision\s*=\s*(\d+)\.(\d+)\.(\d+)");
                        if (match.Success)
                        {
                            int majorVersion = int.Parse(match.Groups[1].Value);
                            string fullVersion = $"{match.Groups[1].Value}.{match.Groups[2].Value}.{match.Groups[3].Value}";
                            ndkVersions.Add((ndkPath, majorVersion, fullVersion));
                            Log.LogMessage(MessageImportance.Normal, $"Found NDK version {fullVersion} at: {ndkPath}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.LogWarning($"Failed to parse NDK version from {sourcePropsFile}: {ex.Message}");
                    }
                }
            }

            if (ndkVersions.Count == 0)
            {
                Log.LogWarning("No valid NDK installations found");
                return null;
            }

            // Prefer NDK 29, then 26-28, then 25, then any version >= 25
            var preferredNdk = ndkVersions
                .Where(ndk => ndk.majorVersion >= 25)
                .OrderByDescending(ndk => ndk.majorVersion == 29 ? 1000 : ndk.majorVersion) // Give NDK 29 highest priority
                .FirstOrDefault();

            if (preferredNdk.path != null)
            {
                DETECTED_NDK_VERSION = preferredNdk.fullVersion;
                Log.LogMessage(MessageImportance.High, $"✅ Selected NDK version {preferredNdk.fullVersion} (minimum required: 25)");
                return preferredNdk.path;
            }

            // If no NDK >= 25 found, warn and use the latest available
            var latestNdk = ndkVersions.OrderByDescending(ndk => ndk.majorVersion).First();
            DETECTED_NDK_VERSION = latestNdk.fullVersion;
            Log.LogWarning($"⚠️  No NDK version >= 25 found. Using NDK {latestNdk.fullVersion}, but this may cause runtime errors!");
            Log.LogWarning($"⚠️  Please install NDK 29 or higher for best compatibility.");
            return latestNdk.path;
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
                PublishAssemblies(buildType);

                // Copy additional native libraries before Gradle build
                CopyNativeLibraries(buildType);

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

                // Always copy to output folder (project's output folder or custom path)
                CopyToOutputPath(appName, buildType, buildAAB);

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

            // Read Android properties from Directory.Build.props
            androidProperties = ReadAndroidPropertiesFromDirectoryBuildProps();

            // Update AndroidManifest.xml
            string manifestPath = Path.Combine(androidPath, "app", "src", "main", "AndroidManifest.xml");
            if (File.Exists(manifestPath))
            {
                // Generate manifest content dynamically
                string manifestContent = GenerateAndroidManifest(appName, androidProperties);
                File.WriteAllText(manifestPath, manifestContent);
                Log.LogMessage(MessageImportance.High, "✅ Generated AndroidManifest.xml with properties from Directory.Build.props");
            }

            // Update build.gradle
            string buildGradlePath = Path.Combine(androidPath, "app", "build.gradle");
            if (File.Exists(buildGradlePath))
            {
                string packagePrefix = androidProperties.GetValueOrDefault("AndroidPackagePrefix", "com.elix22");
                string packageName = $"{packagePrefix}.{appName}";
                string content = File.ReadAllText(buildGradlePath);
                content = content.Replace("applicationId = 'com.example.native_activity'", $"applicationId = '{packageName}'");
                content = content.Replace("namespace 'com.example.native_activity'", $"namespace '{packageName}'");
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

            // Update gradle.properties with Java 17+ path
            string gradlePropertiesPath = Path.Combine(androidPath, "gradle.properties");
            if (File.Exists(gradlePropertiesPath))
            {
                string javaHome = FindJava17OrHigher();
                if (!string.IsNullOrEmpty(javaHome))
                {
                    // Convert to forward slashes and escape backslashes for gradle.properties
                    string javaHomePath = javaHome.Replace("\\", "\\\\");
                    
                    string content = File.ReadAllText(gradlePropertiesPath);
                    
                    // Check if org.gradle.java.home line exists (commented or uncommented)
                    var regex = new System.Text.RegularExpressions.Regex(@"^\s*#?\s*org\.gradle\.java\.home\s*=.*$", System.Text.RegularExpressions.RegexOptions.Multiline);
                    if (regex.IsMatch(content))
                    {
                        // Replace existing line (commented or uncommented)
                        content = regex.Replace(content, $"org.gradle.java.home={javaHomePath}");
                    }
                    else
                    {
                        // Add it to the file
                        content += $"\norg.gradle.java.home={javaHomePath}\n";
                    }
                    
                    File.WriteAllText(gradlePropertiesPath, content);
                    Log.LogMessage(MessageImportance.High, $"📦 Configured Gradle to use Java from: {javaHome}");
                }
                else
                {
                    Log.LogWarning("⚠️  Could not find Java 17+. Android build may fail.");
                    Log.LogWarning("   Please install Java 17 or higher and set JAVA_HOME environment variable.");
                }
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
                // CMake requires forward slashes or escaped backslashes on Windows
                extPath = extPath.Replace("\\", "/");
                content = content.Replace("set(EXT_ROOT_DIR \"../../../../../../../../ext\")", $"set(EXT_ROOT_DIR \"{extPath}\")");
                // Don't replace ANativeActivity_onCreate - it must remain unchanged
                File.WriteAllText(cmakePath, content);
            }

            // Update Java/Kotlin files (but exclude SokolNativeActivity.java which should not be modified)
            foreach (string file in Directory.GetFiles(androidPath, "*.java", SearchOption.AllDirectories)
                .Concat(Directory.GetFiles(androidPath, "*.kt", SearchOption.AllDirectories)))
            {
                // Skip SokolNativeActivity.java - it must not be modified
                if (Path.GetFileName(file) == "SokolNativeActivity.java")
                    continue;
                    
                string content = File.ReadAllText(file);
                content = content.Replace("NativeActivity", appName);
                File.WriteAllText(file, content);
            }

            // Process Android icon if specified
            ProcessAndroidIcon(androidPath, androidProperties);
        }

        void ProcessAndroidIcon(string androidPath, Dictionary<string, string> androidProperties)
        {
            if (!androidProperties.TryGetValue("AndroidIcon", out string? iconPath) || string.IsNullOrWhiteSpace(iconPath))
            {
                Log.LogMessage(MessageImportance.Normal, "ℹ️  No AndroidIcon specified in Directory.Build.props, using default icon");
                return;
            }

            // Find the icon file
            string sourceIconPath = FindIconFile(iconPath);
            if (string.IsNullOrEmpty(sourceIconPath) || !File.Exists(sourceIconPath))
            {
                Log.LogWarning($"⚠️  Android icon not found: {iconPath}");
                return;
            }

            Log.LogMessage(MessageImportance.High, $"📱 Processing Android icon: {Path.GetFileName(sourceIconPath)}");

            try
            {
                // Android icon sizes for different densities
                var iconSizes = new Dictionary<string, int>
                {
                    { "mipmap-mdpi", 48 },
                    { "mipmap-hdpi", 72 },
                    { "mipmap-xhdpi", 96 },
                    { "mipmap-xxhdpi", 144 },
                    { "mipmap-xxxhdpi", 192 }
                };

                foreach (var kvp in iconSizes)
                {
                    string densityFolder = kvp.Key;
                    int size = kvp.Value;
                    
                    string destFolder = Path.Combine(androidPath, "app", "src", "main", "res", densityFolder);
                    Directory.CreateDirectory(destFolder);
                    
                    string destIcon = Path.Combine(destFolder, "ic_launcher.png");
                    
                    // Use ImageMagick or copy if same size
                    ResizeImage(sourceIconPath, destIcon, size, size);
                    
                    Log.LogMessage(MessageImportance.Normal, $"   ✅ Created {densityFolder}/ic_launcher.png ({size}x{size})");
                }

                Log.LogMessage(MessageImportance.High, "✅ Android icon processed successfully");
            }
            catch (Exception ex)
            {
                Log.LogWarning($"⚠️  Failed to process Android icon: {ex.Message}");
            }
        }

        string FindIconFile(string iconPath)
        {
            // If it's already an absolute path and exists, use it
            if (Path.IsPathRooted(iconPath) && File.Exists(iconPath))
            {
                return iconPath;
            }

            // Check in Assets folder first
            string assetsPath = Path.Combine(opts.ProjectPath, "Assets", iconPath);
            if (File.Exists(assetsPath))
            {
                return assetsPath;
            }

            // Check relative to project path
            string relativePath = Path.Combine(opts.ProjectPath, iconPath);
            if (File.Exists(relativePath))
            {
                return relativePath;
            }

            return null;
        }

        void ResizeImage(string sourcePath, string destPath, int width, int height)
        {
            // First choice: Use SkiaSharp (pure C# - always available, cross-platform, high quality)
            try
            {
                if (ResizeImageWithSkiaSharp(sourcePath, destPath, width, height))
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                Log.LogMessage(MessageImportance.Low, $"SkiaSharp image resizing failed: {ex.Message}");
            }

            // Fallback: Try ImageMagick 7+ with 'magick' command
            bool resized = false;
            try
            {
                var magickResult = Cli.Wrap("magick")
                    .WithArguments($"\"{sourcePath}\" -resize {width}x{height}! \"{destPath}\"")
                    .WithValidation(CommandResultValidation.None)
                    .ExecuteBufferedAsync()
                    .GetAwaiter()
                    .GetResult();

                if (magickResult.ExitCode == 0)
                {
                    resized = true;
                    return;
                }
            }
            catch { }

            // Fallback: Try ImageMagick 6 with 'convert' command
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
                    return;
                }
            }
            catch { }

            // Fallback: Try sips (macOS only)
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
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
                        return;
                    }
                }
                catch { }
            }

            // Final fallback: Copy original
            File.Copy(sourcePath, destPath, true);
            Log.LogWarning($"⚠️  All image resizing methods failed. Copied original for {Path.GetFileName(destPath)}");
        }

        bool ResizeImageWithSkiaSharp(string sourcePath, string destPath, int width, int height)
        {
            // Load the source image
            using var inputStream = File.OpenRead(sourcePath);
            using var original = SkiaSharp.SKBitmap.Decode(inputStream);
            
            if (original == null)
            {
                Log.LogWarning($"   ⚠️  Failed to decode image: {sourcePath}");
                return false;
            }

            // Calculate dimensions to maintain aspect ratio and fill the target size
            int srcWidth = original.Width;
            int srcHeight = original.Height;
            float srcAspect = (float)srcWidth / srcHeight;
            float targetAspect = (float)width / height;

            int cropWidth, cropHeight, cropX, cropY;
            
            if (Math.Abs(srcAspect - targetAspect) < 0.01f)
            {
                // Aspect ratios are similar, use full image
                cropWidth = srcWidth;
                cropHeight = srcHeight;
                cropX = 0;
                cropY = 0;
            }
            else if (srcAspect > targetAspect)
            {
                // Source is wider, crop width
                cropHeight = srcHeight;
                cropWidth = (int)(srcHeight * targetAspect);
                cropX = (srcWidth - cropWidth) / 2;
                cropY = 0;
            }
            else
            {
                // Source is taller, crop height
                cropWidth = srcWidth;
                cropHeight = (int)(srcWidth / targetAspect);
                cropX = 0;
                cropY = (srcHeight - cropHeight) / 2;
            }

            // Create cropped bitmap
            using var cropped = new SkiaSharp.SKBitmap(cropWidth, cropHeight);
            using var canvas = new SkiaSharp.SKCanvas(cropped);
            
            var srcRect = new SkiaSharp.SKRect(cropX, cropY, cropX + cropWidth, cropY + cropHeight);
            var destRect = new SkiaSharp.SKRect(0, 0, cropWidth, cropHeight);
            
            canvas.DrawBitmap(original, srcRect, destRect, new SkiaSharp.SKPaint
            {
                IsAntialias = true,
                FilterQuality = SkiaSharp.SKFilterQuality.High
            });

            // Resize to target size with high-quality sampling
            var imageInfo = new SkiaSharp.SKImageInfo(width, height, SkiaSharp.SKColorType.Rgba8888, SkiaSharp.SKAlphaType.Premul);
            var samplingOptions = new SkiaSharp.SKSamplingOptions(SkiaSharp.SKCubicResampler.CatmullRom);
            using var resized = cropped.Resize(imageInfo, samplingOptions);
            
            if (resized == null)
            {
                Log.LogWarning($"   ⚠️  Failed to resize image to {width}x{height}");
                return false;
            }

            // Save as PNG
            using var image = SkiaSharp.SKImage.FromBitmap(resized);
            using var data = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
            using var outputStream = File.OpenWrite(destPath);
            data.SaveTo(outputStream);

            return true;
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

        void PublishAssemblies(string buildType)
        {
            string[] architectures = { "linux-bionic-arm64", "linux-bionic-arm", "linux-bionic-x64" };

            // Determine configuration
            string configuration = buildType == "release" ? "Release" : "Debug";

            // Add scripts directory to PATH for android_fake_clang.cmd access on Windows
            string sokolNetHome = GetSokolNetHome();
            string scriptsDir = Path.Combine(sokolNetHome, "scripts");
            string currentPath = Environment.GetEnvironmentVariable("PATH") ?? "";
            string newPath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) 
                ? $"{scriptsDir};{currentPath}" 
                : $"{scriptsDir}:{currentPath}";

            // Check for NDK in environment variables and validate version
            string ndkHome = Environment.GetEnvironmentVariable("ANDROID_NDK_HOME");
            string ndkRoot = Environment.GetEnvironmentVariable("ANDROID_NDK_ROOT");
            
            // FindBestAndroidNDK will check if environment NDK is suitable (≥25)
            // If not, it will search for a better one
            string bestNdk = FindBestAndroidNDK(ndkHome, ndkRoot);
            
            if (!string.IsNullOrEmpty(bestNdk))
            {
                ndkHome = bestNdk;
                ndkRoot = bestNdk;
            }
            else
            {
                Log.LogError("❌ No suitable Android NDK found! Please install NDK 25 or higher.");
                Log.LogError("   You can install it via Android Studio SDK Manager or set ANDROID_NDK_HOME environment variable.");
                throw new Exception("No suitable Android NDK found");
            }

            foreach (string arch in architectures)
            {
                Log.LogMessage(MessageImportance.High, $"Publishing for {arch}...");

                try
                {
                    string projectFile = Path.Combine(opts.ProjectPath, $"{PROJECT_NAME}.csproj");

                    // Include DEBUG symbol for Debug builds (semicolon must be URL-encoded for MSBuild)
                    string defineConstants = configuration == "Debug" ? "__ANDROID__%3BDEBUG" : "__ANDROID__";

                    var result = Cli.Wrap("dotnet")
                        .WithArguments($"publish \"{projectFile}\" -r {arch} -c {configuration} -p:BuildAsLibrary=true -p:DisableUnsupportedError=true -p:PublishAotUsingRuntimePack=true -p:RemoveSections=true -p:DefineConstants=\"{defineConstants}\" --verbosity quiet")
                        .WithWorkingDirectory(opts.ProjectPath)
                        .WithEnvironmentVariables(env => 
                        {
                            env.Set("PATH", newPath);
                            if (!string.IsNullOrEmpty(ndkHome)) env.Set("ANDROID_NDK_HOME", ndkHome);
                            if (!string.IsNullOrEmpty(ndkRoot)) env.Set("ANDROID_NDK_ROOT", ndkRoot);
                        })
                        .WithStandardOutputPipe(PipeTarget.ToDelegate(s => Log.LogMessage(MessageImportance.Normal, s)))
                        .WithStandardErrorPipe(PipeTarget.ToDelegate(s => Log.LogError(s)))
                        .ExecuteAsync()
                        .GetAwaiter()
                        .GetResult();

                    if (result.ExitCode == 0)
                    {
                        Log.LogMessage(MessageImportance.High, $"Publishing for {arch} completed successfully");
                        
                        // Copy the published library to the Android libs directory
                        string publishDir = Path.Combine(opts.ProjectPath, "bin", configuration, "net10.0", arch, "publish");
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

        void CopyNativeLibraries(string buildType)
        {
            Log.LogMessage(MessageImportance.High, "📚 Processing native libraries configuration...");
            
            string libsDir = Path.Combine(opts.ProjectPath, "Android", "native-activity", "app", "libs");
            
            // Architectures to process
            var architectures = new[]
            {
                ("linux-bionic-arm64", "arm64-v8a"),
                ("linux-bionic-arm", "armeabi-v7a"),
                ("linux-bionic-x64", "x86_64")
            };

            int librariesProcessed = 0;

            // Scan all Android properties looking for AndroidNativeLibrary_*Path pattern
            foreach (var property in androidProperties)
            {
                if (property.Key.StartsWith("AndroidNativeLibrary_", StringComparison.OrdinalIgnoreCase) &&
                    property.Key.EndsWith("Path", StringComparison.OrdinalIgnoreCase))
                {
                    // Extract library name from property name
                    // AndroidNativeLibrary_OzzUtilPath -> OzzUtil -> ozzutil
                    string libraryNamePart = property.Key.Substring("AndroidNativeLibrary_".Length);
                    libraryNamePart = libraryNamePart.Substring(0, libraryNamePart.Length - "Path".Length);
                    string libraryName = libraryNamePart.ToLowerInvariant();
                    
                    string libraryBasePath = property.Value;
                    
                    // Make path relative to project if needed
                    if (!Path.IsPathRooted(libraryBasePath))
                    {
                        libraryBasePath = Path.GetFullPath(Path.Combine(opts.ProjectPath, libraryBasePath));
                    }
                    
                    Log.LogMessage(MessageImportance.High, $"🔧 Processing library '{libraryName}' from: {libraryBasePath} (build: {buildType})");

                    // Copy library for each architecture
                    foreach (var (runtimeId, abiName) in architectures)
                    {
                        string sourceLibFile = null;
                        string sourceLibDir = Path.Combine(libraryBasePath, abiName);
                        
                        // Try build-type specific subdirectory first (debug/release)
                        string buildTypeDir = Path.Combine(sourceLibDir, buildType);
                        string buildTypeLibFile = Path.Combine(buildTypeDir, $"lib{libraryName}.so");
                        
                        if (File.Exists(buildTypeLibFile))
                        {
                            sourceLibFile = buildTypeLibFile;
                            sourceLibDir = buildTypeDir;
                        }
                        else
                        {
                            // Fallback to direct architecture directory
                            string directLibFile = Path.Combine(sourceLibDir, $"lib{libraryName}.so");
                            if (File.Exists(directLibFile))
                            {
                                sourceLibFile = directLibFile;
                            }
                            else
                            {
                                // Try common subdirectories as fallback
                                string[] fallbackDirs = { "release", "debug" };
                                foreach (string fallbackDir in fallbackDirs)
                                {
                                    string fallbackPath = Path.Combine(sourceLibDir, fallbackDir, $"lib{libraryName}.so");
                                    if (File.Exists(fallbackPath))
                                    {
                                        sourceLibFile = fallbackPath;
                                        Log.LogMessage(MessageImportance.Normal, $"ℹ️  Using fallback {fallbackDir} library for {libraryName} on {abiName}");
                                        break;
                                    }
                                }
                            }
                        }
                        
                        if (sourceLibFile != null && File.Exists(sourceLibFile))
                        {
                            string destLibDir = Path.Combine(libsDir, abiName);
                            string destLibFile = Path.Combine(destLibDir, $"lib{libraryName}.so");
                            
                            Directory.CreateDirectory(destLibDir);
                            File.Copy(sourceLibFile, destLibFile, true);
                            
                            Log.LogMessage(MessageImportance.Normal, $"✅ Copied {libraryName} for {abiName}: {sourceLibFile} -> {destLibFile}");
                        }
                        else
                        {
                            Log.LogMessage(MessageImportance.Normal, $"ℹ️  Library {libraryName} not found for {abiName} in any location");
                        }
                    }
                    
                    librariesProcessed++;
                }
            }
            
            if (librariesProcessed > 0)
            {
                Log.LogMessage(MessageImportance.High, $"✅ Processed {librariesProcessed} native library configuration(s)");
                
                // Update CMakeLists.txt to include the native libraries
                UpdateCMakeListsForNativeLibraries();
                
                // Configure build for native libraries (shared C++ runtime and Java loading)
                ConfigureForNativeLibraries();
            }
            else
            {
                Log.LogMessage(MessageImportance.Normal, "ℹ️  No native library configurations found (AndroidNativeLibrary_*Path properties)");
            }
        }

        void UpdateCMakeListsForNativeLibraries()
        {
            string cmakeListsPath = Path.Combine(opts.ProjectPath, "Android", "native-activity", "app", "src", "main", "cpp", "CMakeLists.txt");
            
            if (!File.Exists(cmakeListsPath))
            {
                Log.LogMessage(MessageImportance.Normal, "⚠️  CMakeLists.txt not found, skipping native library integration");
                return;
            }

            string cmakeContent = File.ReadAllText(cmakeListsPath);
            
            // Build the additional library configurations
            var additionalLibraries = new List<string>();
            var additionalLinkLibraries = new List<string>();

            foreach (var property in androidProperties)
            {
                if (property.Key.StartsWith("AndroidNativeLibrary_", StringComparison.OrdinalIgnoreCase) &&
                    property.Key.EndsWith("Path", StringComparison.OrdinalIgnoreCase))
                {
                    // Extract library name
                    string libraryNamePart = property.Key.Substring("AndroidNativeLibrary_".Length);
                    libraryNamePart = libraryNamePart.Substring(0, libraryNamePart.Length - "Path".Length);
                    string libraryName = libraryNamePart.ToLowerInvariant();
                    
                    additionalLibraries.Add($@"
# Add {libraryName} library
add_library({libraryName} SHARED IMPORTED)
set_target_properties({libraryName} PROPERTIES 
    IMPORTED_LOCATION ${{PREBUILT_LIB_PATH}}/${{ANDROID_ABI}}/lib{libraryName}.so
    IMPORTED_SONAME ""lib{libraryName}.so"")");
                    
                    additionalLinkLibraries.Add($"    {libraryName}");
                }
            }

            if (additionalLibraries.Count > 0)
            {
                // Insert additional libraries after the main app library definition
                string insertionPoint = "set_target_properties(";
                int lastSetTargetProps = cmakeContent.LastIndexOf(insertionPoint);
                
                if (lastSetTargetProps >= 0)
                {
                    // Find the closing ) for this set_target_properties
                    int openParen = cmakeContent.IndexOf('(', lastSetTargetProps);
                    int closeParen = -1;
                    int parenCount = 0;
                    for (int i = openParen; i < cmakeContent.Length; i++)
                    {
                        if (cmakeContent[i] == '(') parenCount++;
                        else if (cmakeContent[i] == ')')
                        {
                            parenCount--;
                            if (parenCount == 0)
                            {
                                closeParen = i;
                                break;
                            }
                        }
                    }
                    
                    if (closeParen >= 0)
                    {
                        string additionalLibsText = string.Join("", additionalLibraries);
                        cmakeContent = cmakeContent.Insert(closeParen + 1, additionalLibsText);
                        
                        // Also add to target_link_libraries
                        string linkLibrariesPattern = "target_link_libraries(sokol";
                        int linkIndex = cmakeContent.LastIndexOf(linkLibrariesPattern);
                        if (linkIndex >= 0)
                        {
                            // Find the closing parenthesis
                            int linkOpenParen = cmakeContent.IndexOf('(', linkIndex);
                            int linkCloseParen = cmakeContent.IndexOf(')', linkOpenParen);
                            if (linkOpenParen >= 0 && linkCloseParen >= 0)
                            {
                                string additionalLinks = string.Join("\n", additionalLinkLibraries);
                                cmakeContent = cmakeContent.Insert(linkCloseParen, "\n" + additionalLinks);
                            }
                        }
                        
                        File.WriteAllText(cmakeListsPath, cmakeContent);
                        Log.LogMessage(MessageImportance.Normal, $"📝 Updated CMakeLists.txt with {additionalLibraries.Count} additional native libraries");
                    }
                    else
                    {
                        Log.LogMessage(MessageImportance.Normal, "⚠️  Could not find insertion point in CMakeLists.txt");
                    }
                }
            }
        }

        void ConfigureForNativeLibraries()
        {
            Log.LogMessage(MessageImportance.High, "🔧 Configuring build for native library dependencies...");
            
            // 1. Update build.gradle to use shared C++ runtime
            ConfigureBuildGradleForSharedCppRuntime();
            
            // 2. Update Java activity to load native libraries in correct order
            ConfigureJavaLibraryLoading();
        }

        void ConfigureBuildGradleForSharedCppRuntime()
        {
            string buildGradlePath = Path.Combine(opts.ProjectPath, "Android", "native-activity", "app", "build.gradle");
            
            if (!File.Exists(buildGradlePath))
            {
                Log.LogMessage(MessageImportance.Normal, "⚠️  build.gradle not found, skipping C++ runtime configuration");
                return;
            }

            string content = File.ReadAllText(buildGradlePath);
            
            // Replace c++_static with c++_shared for native library compatibility
            bool modified = false;
            if (content.Contains("'-DANDROID_STL=c++_static'"))
            {
                content = content.Replace("'-DANDROID_STL=c++_static'", "'-DANDROID_STL=c++_shared'");
                modified = true;
                Log.LogMessage(MessageImportance.Normal, "📝 Updated build.gradle to use c++_shared runtime for native library compatibility");
            }
            
            if (modified)
            {
                File.WriteAllText(buildGradlePath, content);
            }
        }

        void ConfigureJavaLibraryLoading()
        {
            // Collect all native library names from AndroidNativeLibrary_*Path properties
            var nativeLibraries = new List<string>();
            
            foreach (var property in androidProperties)
            {
                if (property.Key.StartsWith("AndroidNativeLibrary_", StringComparison.OrdinalIgnoreCase) &&
                    property.Key.EndsWith("Path", StringComparison.OrdinalIgnoreCase))
                {
                    // Extract library name from property name
                    // AndroidNativeLibrary_OzzUtilPath -> OzzUtil -> ozzutil
                    string libraryNamePart = property.Key.Substring("AndroidNativeLibrary_".Length);
                    libraryNamePart = libraryNamePart.Substring(0, libraryNamePart.Length - "Path".Length);
                    string libraryName = libraryNamePart.ToLowerInvariant();
                    nativeLibraries.Add(libraryName);
                }
            }
            
            // Only modify Java loading if we have native libraries
            if (nativeLibraries.Count == 0)
            {
                Log.LogMessage(MessageImportance.Normal, "ℹ️  No native libraries detected, keeping default sokol loading");
                return;
            }
            
            string javaActivityPath = Path.Combine(opts.ProjectPath, "Android", "native-activity", "app", "src", "main", "java", "com", "sokol", "app", "SokolNativeActivity.java");
            
            if (!File.Exists(javaActivityPath))
            {
                Log.LogMessage(MessageImportance.Normal, "⚠️  SokolNativeActivity.java not found, skipping library loading configuration");
                return;
            }

            string content = File.ReadAllText(javaActivityPath);
            
            // Find and replace the simple sokol loading with dynamic loading
            string oldPattern = @"    // Load native library early so JNI methods are available\s*\n\s*static\s*\{\s*\n\s*System\.loadLibrary\(""sokol""\);\s*\n\s*\}";
            
            var regex = new System.Text.RegularExpressions.Regex(oldPattern, System.Text.RegularExpressions.RegexOptions.Multiline);
            if (regex.IsMatch(content))
            {
                // Build library loading statements dynamically
                var loadStatements = new List<string>();
                
                // Add c++_shared first if we have native libraries (they likely need it)
                loadStatements.Add("            // Load C++ shared runtime first (dependency for native libraries)");
                loadStatements.Add("            System.loadLibrary(\"c++_shared\");");
                
                // Add each detected native library with comments
                foreach (string libName in nativeLibraries)
                {
                    loadStatements.Add($"            // Load {libName} library");
                    loadStatements.Add($"            System.loadLibrary(\"{libName}\");");
                }
                
                // Add sokol last
                loadStatements.Add("            // Load main sokol library last");
                loadStatements.Add("            System.loadLibrary(\"sokol\");");
                
                // Create the new static block with proper error handling
                string newPattern = $@"    // Load native library early so JNI methods are available
    static {{
        try {{
{string.Join("\n", loadStatements)}
        }} catch (UnsatisfiedLinkError e) {{
            // Fallback: try loading just sokol if other libraries are not available
            System.loadLibrary(""sokol"");
        }}
    }}";
                
                content = regex.Replace(content, newPattern);
                File.WriteAllText(javaActivityPath, content);
                
                Log.LogMessage(MessageImportance.Normal, $"📝 Updated Java library loading for {nativeLibraries.Count} native libraries: {string.Join(", ", nativeLibraries)}");
            }
            else
            {
                Log.LogMessage(MessageImportance.Normal, "⚠️  Could not find library loading pattern in SokolNativeActivity.java");
            }
        }

        void BuildAndroidApp(string appName, string buildType)
        {
            string androidPath = Path.Combine(opts.ProjectPath, "Android", "native-activity");

            Log.LogMessage(MessageImportance.Normal, $"Android path: {androidPath}");
            Log.LogMessage(MessageImportance.Normal, $"Android path exists: {Directory.Exists(androidPath)}");
            string gradlewScript = GetGradlewScriptName();
            Log.LogMessage(MessageImportance.Normal, $"Gradlew path: {Path.Combine(androidPath, gradlewScript)}");
            Log.LogMessage(MessageImportance.Normal, $"Gradlew exists: {File.Exists(Path.Combine(androidPath, gradlewScript))}");

            // Build Gradle arguments with NDK version if available
            string ndkVersionArg = !string.IsNullOrEmpty(DETECTED_NDK_VERSION) 
                ? $"-PndkVersionArg=\"{DETECTED_NDK_VERSION}\"" 
                : "";
            
            // Build CMake arguments
            string cmakeArgs = $"-DAPP_NAME={appName}";
            
            if (!string.IsNullOrEmpty(DETECTED_NDK_VERSION))
            {
                Log.LogMessage(MessageImportance.High, $"📦 Configuring Gradle to use NDK version: {DETECTED_NDK_VERSION}");
            }

            if (buildType == "release")
            {
                Log.LogMessage(MessageImportance.High, "Building release APK...");
                string gradlewPath = Path.Combine(androidPath, gradlewScript);
                Log.LogMessage(MessageImportance.Normal, $"Using gradlew path: {gradlewPath}");

                var result = Cli.Wrap(gradlewPath)
                    .WithArguments($"assembleRelease -PcmakeArgs=\"{cmakeArgs}\" {ndkVersionArg}")
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
                string gradlewPath = Path.Combine(androidPath, gradlewScript);
                Log.LogMessage(MessageImportance.Normal, $"Using gradlew path: {gradlewPath}");

                var result = Cli.Wrap(gradlewPath)
                    .WithArguments($"assembleDebug -PcmakeArgs=\"{cmakeArgs}\" {ndkVersionArg}")
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
            string gradlewScript = GetGradlewScriptName();
            Log.LogMessage(MessageImportance.Normal, $"Gradlew path: {Path.Combine(androidPath, gradlewScript)}");
            Log.LogMessage(MessageImportance.Normal, $"Gradlew exists: {File.Exists(Path.Combine(androidPath, gradlewScript))}");

            // Build Gradle arguments with NDK version if available
            string ndkVersionArg = !string.IsNullOrEmpty(DETECTED_NDK_VERSION) 
                ? $"-PndkVersionOverride=\"{DETECTED_NDK_VERSION}\"" 
                : "";
            
            // Build CMake arguments
            string cmakeArgs = $"-DAPP_NAME={appName}";
            
            if (!string.IsNullOrEmpty(DETECTED_NDK_VERSION))
            {
                Log.LogMessage(MessageImportance.High, $"📦 Configuring Gradle to use NDK version: {DETECTED_NDK_VERSION}");
            }

            if (buildType == "release")
            {
                Log.LogMessage(MessageImportance.High, "Building release AAB...");
                string gradlewPath = Path.Combine(androidPath, gradlewScript);
                Log.LogMessage(MessageImportance.Normal, $"Using gradlew path: {gradlewPath}");

                var result = Cli.Wrap(gradlewPath)
                    .WithArguments($"bundleRelease -PcmakeArgs=\"{cmakeArgs}\" {ndkVersionArg}")
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
                string gradlewPath = Path.Combine(androidPath, gradlewScript);
                Log.LogMessage(MessageImportance.Normal, $"Using gradlew path: {gradlewPath}");

                var result = Cli.Wrap(gradlewPath)
                    .WithArguments($"bundleDebug -PcmakeArgs=\"{cmakeArgs}\" {ndkVersionArg}")
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
                string androidSdkPath = GetAndroidSdkPath();
                if (!string.IsNullOrEmpty(androidSdkPath) && Directory.Exists(androidSdkPath))
                {
                    var stringBuilder = new System.Text.StringBuilder();
                    var findApksignerResult = Cli.Wrap("find")
                        .WithArguments(new[] { androidSdkPath, "-name", "apksigner", "-type", "f" })
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
        Dictionary<string, string> ReadAndroidPropertiesFromDirectoryBuildProps()
        {
            var properties = new Dictionary<string, string>();
            string directoryBuildPropsPath = Path.Combine(opts.ProjectPath, "Directory.Build.props");

            if (!File.Exists(directoryBuildPropsPath))
            {
                Log.LogMessage(MessageImportance.Normal, "ℹ️  No Directory.Build.props found, using default Android configuration");
                return properties;
            }

            try
            {
                XDocument doc = XDocument.Load(directoryBuildPropsPath);
                
                // Read from ALL PropertyGroup elements, not just the first one
                var propertyGroups = doc.Root?.Elements("PropertyGroup");

                if (propertyGroups != null)
                {
                    foreach (var propertyGroup in propertyGroups)
                    {
                        // Read all properties that start with "Android"
                        foreach (var element in propertyGroup.Elements())
                        {
                            if (element.Name.LocalName.StartsWith("Android", StringComparison.OrdinalIgnoreCase))
                            {
                                properties[element.Name.LocalName] = element.Value;
                            }
                            // Also read AppVersion property (used across all platforms)
                            if (element.Name.LocalName.Equals("AppVersion", StringComparison.OrdinalIgnoreCase))
                            {
                                properties[element.Name.LocalName] = element.Value;
                            }
                        }
                    }
                }

                Log.LogMessage(MessageImportance.Normal, $"📋 Read {properties.Count} Android properties from Directory.Build.props");
                foreach (var prop in properties)
                {
                    Log.LogMessage(MessageImportance.Normal, $"   - {prop.Key}: {prop.Value}");
                }
            }
            catch (Exception ex)
            {
                Log.LogWarning($"⚠️  Failed to parse Directory.Build.props: {ex.Message}");
            }

            return properties;
        }

        string GenerateAndroidManifest(string appName, Dictionary<string, string> androidProperties)
        {
            var manifest = new StringBuilder();
            manifest.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            manifest.AppendLine("<!-- BEGIN_INCLUDE(manifest) -->");
            manifest.AppendLine("<manifest xmlns:android=\"http://schemas.android.com/apk/res/android\"");
            
            // Version from Directory.Build.props (defaults to "1.0")
            string appVersion = androidProperties.GetValueOrDefault("AppVersion", "1.0");
            // versionCode is derived from version string (e.g., "1.0" -> 1, "1.2.3" -> 1)
            string versionCode = appVersion.Split('.')[0];
            
            manifest.AppendLine($"    android:versionCode=\"{versionCode}\"");
            manifest.AppendLine($"          android:versionName=\"{appVersion}\">");
            manifest.AppendLine();

            // SDK versions
            string minSdk = androidProperties.GetValueOrDefault("AndroidMinSdkVersion", "26");
            string targetSdk = androidProperties.GetValueOrDefault("AndroidTargetSdkVersion", "34");
            manifest.AppendLine($"  <uses-sdk android:minSdkVersion=\"{minSdk}\" android:targetSdkVersion=\"{targetSdk}\"/>");

            // Permissions - read from Directory.Build.props
            var permissions = GetAndroidPermissions(androidProperties);
            foreach (var permission in permissions)
            {
                manifest.AppendLine($"  <uses-permission android:name=\"{permission}\"/>");
            }

            // Features (optional)
            var features = GetAndroidFeatures(androidProperties);
            foreach (var feature in features)
            {
                bool required = !feature.Contains("not-required");
                string featureName = feature.Replace(":not-required", "");
                manifest.AppendLine($"  <uses-feature android:name=\"{featureName}\" android:required=\"{required.ToString().ToLower()}\"/>");
            }

            manifest.AppendLine("  <!--");
            manifest.AppendLine("  This .apk has no Java/Kotlin code, so set hasCode to false.");
            manifest.AppendLine();
            manifest.AppendLine("  If you copy from this sample and later add Java/Kotlin code, or add a");
            manifest.AppendLine("  dependency on a library that does (such as androidx), be sure to set");
            manifest.AppendLine("  `android:hasCode` to `true` (or just remove it, since that's the default).");
            manifest.AppendLine("  -->");

            // Application section
            bool allowBackup = bool.Parse(androidProperties.GetValueOrDefault("AndroidAllowBackup", "false"));
            bool fullBackupContent = bool.Parse(androidProperties.GetValueOrDefault("AndroidFullBackupContent", "false"));
            bool keepScreenOn = bool.Parse(androidProperties.GetValueOrDefault("AndroidKeepScreenOn", "true"));
            bool fullscreen = bool.Parse(androidProperties.GetValueOrDefault("AndroidFullscreen", "false"));

            // Always set hasCode to true since we include SokolNativeActivity.java in the template
            // (even if fullscreen is disabled, the Java file is present)
            bool hasCode = true;

            manifest.AppendLine("  <application");
            manifest.AppendLine($"      android:allowBackup=\"{allowBackup.ToString().ToLower()}\"");
            manifest.AppendLine($"      android:fullBackupContent=\"{fullBackupContent.ToString().ToLower()}\"");
            manifest.AppendLine($"      android:icon=\"@mipmap/ic_launcher\"");
            manifest.AppendLine($"      android:label=\"{appName}\"");
            manifest.AppendLine($"      android:hasCode=\"{hasCode.ToString().ToLower()}\"");
            
            // Add fullscreen theme if enabled - use custom immersive theme
            if (fullscreen)
            {
                manifest.AppendLine($"      android:theme=\"@style/AppTheme.Fullscreen\">");
            }
            else
            {
                manifest.AppendLine($"      android:theme=\"@style/AppTheme\">");
            }
            manifest.AppendLine();

            // Activity
            manifest.AppendLine("    <!-- Our activity is the built-in NativeActivity framework class.");
            manifest.AppendLine("         This will take care of integrating with our NDK code. -->");

            // Configure orientation - command-line flag takes precedence over Directory.Build.props
            string androidOrientation;
            if (!string.IsNullOrEmpty(opts.Orientation) && opts.Orientation != "both")
            {
                // Use command-line orientation if explicitly set (and not default "both")
                androidOrientation = opts.ValidatedOrientation switch
                {
                    "portrait" => "portrait",
                    "landscape" => "landscape",
                    "both" => "unspecified",
                    _ => "unspecified"
                };
            }
            else if (androidProperties.TryGetValue("AndroidScreenOrientation", out string? propOrientation) && !string.IsNullOrWhiteSpace(propOrientation))
            {
                // Use orientation from Directory.Build.props
                androidOrientation = propOrientation.ToLower() switch
                {
                    "portrait" => "portrait",
                    "landscape" => "landscape",
                    "reverselandscape" => "reverseLandscape",
                    "reverseportrait" => "reversePortrait",
                    "sensorlandscape" => "sensorLandscape",
                    "sensorportrait" => "sensorPortrait",
                    "sensor" => "sensor",
                    "fullsensor" => "fullSensor",
                    "nosensor" => "nosensor",
                    "user" => "user",
                    "fulluser" => "fullUser",
                    "locked" => "locked",
                    "unspecified" => "unspecified",
                    "behind" => "behind",
                    _ => "unspecified"
                };
            }
            else
            {
                // Default fallback
                androidOrientation = "unspecified";
            }

            // Use custom SokolNativeActivity for fullscreen to enable immersive mode
            string activityName = fullscreen ? "com.sokol.app.SokolNativeActivity" : "android.app.NativeActivity";
            
            manifest.AppendLine($"    <activity android:name=\"{activityName}\"");
            manifest.AppendLine($"              android:label=\"{appName}\"");
            manifest.AppendLine("              android:configChanges=\"orientation|keyboardHidden|screenSize|screenLayout\"");
            manifest.AppendLine($"              android:screenOrientation=\"{androidOrientation}\"");
            manifest.AppendLine($"              android:keepScreenOn=\"{keepScreenOn.ToString().ToLower()}\"");
            manifest.AppendLine("        android:exported=\"true\">");
            manifest.AppendLine("      <!-- Tell NativeActivity the name of our .so -->");
            manifest.AppendLine("      <meta-data android:name=\"android.app.lib_name\"");
            manifest.AppendLine("                 android:value=\"sokol\" />");
            manifest.AppendLine("      <intent-filter>");
            manifest.AppendLine("        <action android:name=\"android.intent.action.MAIN\" />");
            manifest.AppendLine("        <category android:name=\"android.intent.category.LAUNCHER\" />");
            manifest.AppendLine("      </intent-filter>");
            manifest.AppendLine("    </activity>");
            manifest.AppendLine("  </application>");
            manifest.AppendLine();
            manifest.AppendLine("</manifest>");
            manifest.AppendLine("<!-- END_INCLUDE(manifest) -->");

            return manifest.ToString();
        }

        List<string> GetAndroidPermissions(Dictionary<string, string> properties)
        {
            var permissions = new List<string>();

            // Check for AndroidPermissions property (semicolon-separated list)
            if (properties.TryGetValue("AndroidPermissions", out string? permissionsStr) && !string.IsNullOrWhiteSpace(permissionsStr))
            {
                var perms = permissionsStr.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p.Trim())
                    .Where(p => !string.IsNullOrEmpty(p));
                permissions.AddRange(perms);
            }

            // If no permissions specified, use defaults
            if (permissions.Count == 0)
            {
                permissions.AddRange(new[]
                {
                    "android.permission.RECORD_AUDIO",
                    "android.permission.WAKE_LOCK",
                    "android.permission.INTERNET",
                    "android.permission.WRITE_EXTERNAL_STORAGE"
                });
            }

            return permissions;
        }

        List<string> GetAndroidFeatures(Dictionary<string, string> properties)
        {
            var features = new List<string>();

            // Check for AndroidFeatures property (semicolon-separated list)
            if (properties.TryGetValue("AndroidFeatures", out string? featuresStr) && !string.IsNullOrWhiteSpace(featuresStr))
            {
                var feats = featuresStr.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(f => f.Trim())
                    .Where(f => !string.IsNullOrEmpty(f));
                features.AddRange(feats);
            }

            return features;
        }

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
                    // Split by any whitespace (tab or spaces)
                    var parts = line.Split(new[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2 && parts[1].Trim() == "device")
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

            // Read Android properties to get package prefix
            var androidProperties = ReadAndroidPropertiesFromDirectoryBuildProps();
            string packagePrefix = androidProperties.GetValueOrDefault("AndroidPackagePrefix", "com.elix22");

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
                
                // Uninstall existing app to avoid signature mismatch errors
                string packageName = $"{packagePrefix}.{appName}";
                Log.LogMessage(MessageImportance.Normal, $"Uninstalling existing app (if any): {packageName}");
                try
                {
                    var uninstallResult = Cli.Wrap("adb")
                        .WithArguments($"-s {selectedDeviceId} uninstall {packageName}")
                        .WithStandardOutputPipe(PipeTarget.ToDelegate(s => Log.LogMessage(MessageImportance.Normal, s)))
                        .WithStandardErrorPipe(PipeTarget.ToDelegate(s => Log.LogMessage(MessageImportance.Normal, s)))
                        .ExecuteAsync()
                        .GetAwaiter()
                        .GetResult();
                    
                    if (uninstallResult.ExitCode == 0)
                    {
                        Log.LogMessage(MessageImportance.Normal, "✅ Existing app uninstalled");
                    }
                }
                catch
                {
                    // Ignore uninstall errors (app might not be installed)
                    Log.LogMessage(MessageImportance.Normal, "ℹ️  No existing app to uninstall");
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
                    // packageName already declared above for uninstall

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

            // Read Android properties to get package prefix
            var androidProperties = ReadAndroidPropertiesFromDirectoryBuildProps();
            string packagePrefix = androidProperties.GetValueOrDefault("AndroidPackagePrefix", "com.elix22");

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

                    // Convert AAB to APK using bundletool (device-specific for smaller size)
                    string apksPath = Path.Combine(tempDir, "app.apks");
                    
                    var bundletoolResult = Cli.Wrap("java")
                        .WithArguments($"-jar \"{bundletoolPath}\" build-apks --bundle=\"{aabPath}\" --output=\"{apksPath}\" --device-spec=\"{deviceSpecFile}\"")
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

                    // Install APKs directly from the .apks file using bundletool
                    Log.LogMessage(MessageImportance.High, "Installing device-specific APKs...");
                    
                    var installResult = Cli.Wrap("java")
                        .WithArguments($"-jar \"{bundletoolPath}\" install-apks --apks=\"{apksPath}\" --device-id={selectedDeviceId}")
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
                        string packageName = $"{packagePrefix}.{appName}";
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
            // First check local tools folder using SokolNet home
            string sokolNetHome = GetSokolNetHome();
            string localBundletool = Path.Combine(sokolNetHome, "tools", "bundletool.jar");
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

        void CopyToOutputPath(string appName, string buildType, bool isAAB)
        {
            string androidPath = Path.Combine(opts.ProjectPath, "Android", "native-activity");
            string sourceFile;
            string fileName;

            if (isAAB)
            {
                // AAB file
                string aabSubPath = buildType == "release" ? "release" : "debug";
                sourceFile = Path.Combine(androidPath, "app", "build", "outputs", "bundle", aabSubPath, $"app-{aabSubPath}.aab");
                fileName = $"{appName}-{buildType}.aab";
            }
            else
            {
                // APK file
                string apkSubPath = buildType == "release" ? "release" : "debug";
                sourceFile = Path.Combine(androidPath, "app", "build", "outputs", "apk", apkSubPath, $"app-{apkSubPath}.apk");
                fileName = $"{appName}-{buildType}.apk";
            }

            if (!File.Exists(sourceFile))
            {
                Log.LogWarning($"Build output file not found: {sourceFile}");
                return;
            }

            // Determine output base path: use custom path if specified, otherwise use project's output folder
            string outputBasePath = string.IsNullOrEmpty(opts.OutputPath) 
                ? Path.Combine(opts.ProjectPath, "output") 
                : opts.OutputPath;

            // Create output directory structure: {basePath}/Android/buildType/
            string outputDir = Path.Combine(outputBasePath, "Android", buildType);
            Directory.CreateDirectory(outputDir);

            string destFile = Path.Combine(outputDir, fileName);
            File.Copy(sourceFile, destFile, true);

            Log.LogMessage(MessageImportance.High, $"✅ Copied {(isAAB ? "AAB" : "APK")} to: {destFile}");
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

