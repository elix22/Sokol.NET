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

        // Returns platform/android/ paths for plugins this project uses,
        // detected by AndroidNativeLibrary_*Path props pointing into the plugin's directory.
        private IEnumerable<string> GetActivePluginAndroidPlatformPaths()
        {
            string pluginsDir = Path.Combine(Utils.GetSokolNetHome(), "plugins");
            if (!Directory.Exists(pluginsDir))
                yield break;

            var nativeLibPaths = androidProperties
                .Where(kv => kv.Key.StartsWith("AndroidNativeLibrary_", StringComparison.OrdinalIgnoreCase)
                          && kv.Key.EndsWith("Path", StringComparison.OrdinalIgnoreCase))
                .Select(kv => Path.IsPathRooted(kv.Value)
                    ? kv.Value
                    : Path.GetFullPath(Path.Combine(opts.ProjectPath, kv.Value)))
                .ToList();

            foreach (string pluginDir in Directory.GetDirectories(pluginsDir))
            {
                string pluginAndroid = Path.Combine(pluginDir, "platform", "android");
                if (!Directory.Exists(pluginAndroid))
                    continue;
                string pluginDirNorm = pluginDir.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (nativeLibPaths.Any(p => p.StartsWith(pluginDirNorm, StringComparison.OrdinalIgnoreCase)))
                    yield return pluginAndroid;
            }
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
            // Store environment NDK info for potential fallback
            string envNdk = currentNdkHome ?? currentNdkRoot;
            int envNdkMajorVersion = 0;
            string envNdkFullVersion = null;
            
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
                            envNdkMajorVersion = int.Parse(match.Groups[1].Value);
                            envNdkFullVersion = $"{match.Groups[1].Value}.{match.Groups[2].Value}.{match.Groups[3].Value}";
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.LogWarning($"Failed to parse NDK version from environment: {ex.Message}");
                    }
                }
            }

            // Search Android SDK for NDK 27+ (REQUIRED for 16KB page size support)
            Log.LogMessage(MessageImportance.High, "🔍 Searching for Android NDK 27+ (required for Google Play submission)...");
            
            // Use the helper method to get Android SDK path
            string androidSdk = GetAndroidSdkPath();

            if (!Directory.Exists(androidSdk))
            {
                Log.LogError($"❌ Android SDK not found at: {androidSdk}");
                Log.LogError($"❌ Please install Android SDK and NDK 27+");
                return null;
            }

            string ndkDir = Path.Combine(androidSdk, "ndk");
            if (!Directory.Exists(ndkDir))
            {
                Log.LogError($"❌ Android NDK directory not found at: {ndkDir}");
                Log.LogError($"❌ Please install NDK 27+ via Android Studio SDK Manager or Android command-line tools");
                Log.LogError($"   Example: sdkmanager --install 'ndk;27.1.12297006'");
                return null;
            }

            // Find all NDK versions 27+
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

            // Filter to only NDK 27+
            var validNdks = ndkVersions.Where(ndk => ndk.majorVersion >= 27).ToList();

            // Check environment NDK if it exists
            if (envNdkMajorVersion >= 27)
            {
                // Add environment NDK to the list if it's 27+
                validNdks.Add((envNdk, envNdkMajorVersion, envNdkFullVersion));
            }

            if (validNdks.Count == 0)
            {
                Log.LogError("❌ NDK 27+ is REQUIRED for building Android applications");
                Log.LogError("❌ Google Play requires 16KB page size support (Android 15+ / API 35+)");
                Log.LogError("");
                Log.LogError("📥 Please install NDK 27 or higher:");
                Log.LogError("   • Via Android Studio: Tools → SDK Manager → SDK Tools → NDK (Side by side)");
                Log.LogError("   • Via command line: sdkmanager --install 'ndk;27.1.12297006'");
                Log.LogError("   • Recommended: Install NDK 29+ for best compatibility");
                Log.LogError("");
                
                if (ndkVersions.Count > 0)
                {
                    var latestNdk = ndkVersions.OrderByDescending(ndk => ndk.majorVersion).First();
                    Log.LogError($"❌ Found NDK {latestNdk.fullVersion}, but this version is too old");
                    Log.LogError($"   NDK 27+ is required for 16KB page size support");
                }
                else if (envNdkMajorVersion > 0)
                {
                    Log.LogError($"❌ Found NDK {envNdkFullVersion} in environment, but this version is too old");
                    Log.LogError($"   NDK 27+ is required for 16KB page size support");
                }
                else
                {
                    Log.LogError("❌ No NDK installation found at all");
                }
                
                return null;
            }

            // Select the best NDK 27+ (prefer higher versions)
            var selectedNdk = validNdks
                .OrderByDescending(ndk => ndk.majorVersion)
                .ThenByDescending(ndk => ndk.fullVersion)
                .First();

            DETECTED_NDK_VERSION = selectedNdk.fullVersion;
            Log.LogMessage(MessageImportance.High, $"✅ Selected NDK version {selectedNdk.fullVersion} (includes 16KB page size support)");
            
            if (validNdks.Count > 1)
            {
                Log.LogMessage(MessageImportance.Normal, $"   Found {validNdks.Count} compatible NDK versions, using the latest");
            }
            
            return selectedNdk.path;
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

                // Generate RegisteredScripts.g.cs for NativeAOT (Android always needs this)
                Log.LogMessage(MessageImportance.High, "📝 Scanning for GameBehaviour subclasses...");
                Utils.GenerateRegisteredScripts(Log, opts.ProjectPath, appName);

                // Compile shaders
                CompileShaders();

                // Publish .NET assemblies for different architectures
                PublishAssemblies(buildType);

                // Copy additional native libraries before Gradle build
                CopyNativeLibraries(buildType);

                // Copy additional Java sources (e.g. JNI bridge classes) into the APK
                CopyJavaSources();

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

            // Detect which plugins this project uses (by AndroidNativeLibrary_*Path pointing into plugins/)
            var activePluginAndroidPaths = GetActivePluginAndroidPlatformPaths().ToList();

            // Update AndroidManifest.xml
            string manifestPath = Path.Combine(androidPath, "app", "src", "main", "AndroidManifest.xml");
            if (File.Exists(manifestPath))
            {
                // Generate manifest content dynamically
                string manifestContent = GenerateAndroidManifest(appName, androidProperties, activePluginAndroidPaths);
                File.WriteAllText(manifestPath, manifestContent);
                Log.LogMessage(MessageImportance.High, "✅ Generated AndroidManifest.xml with properties from Directory.Build.props");
            }

            // Copy platform/android/res/ → app/src/main/res/ (e.g. xml/file_provider_paths.xml)
            string platformResPath = Path.Combine(opts.ProjectPath, "platform", "android", "res");
            string destResPath = Path.Combine(androidPath, "app", "src", "main", "res");
            if (Directory.Exists(platformResPath))
            {
                Utils.CopyDirectory(platformResPath, destResPath);
                Log.LogMessage(MessageImportance.High, "✅ Copied platform/android/res/ → app/src/main/res/");
            }
            else
            {
                foreach (string pluginAndroid in activePluginAndroidPaths)
                {
                    string pluginRes = Path.Combine(pluginAndroid, "res");
                    if (Directory.Exists(pluginRes))
                    {
                        Utils.CopyDirectory(pluginRes, destResPath);
                        string pluginName = Path.GetFileName(Directory.GetParent(pluginAndroid)!.FullName);
                        Log.LogMessage(MessageImportance.High, $"✅ Copied {pluginName} plugin platform/android/res/ → app/src/main/res/ (fallback)");
                    }
                }
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
                
                // Update versionCode and versionName from Directory.Build.props
                string versionCode = androidProperties.GetValueOrDefault("AndroidVersionCode", "1");
                string versionName = androidProperties.GetValueOrDefault("AppVersion", "1.0");
                
                // Update versionCode using regex to handle any existing value
                var versionCodeRegex = new System.Text.RegularExpressions.Regex(@"versionCode\s+\d+", System.Text.RegularExpressions.RegexOptions.Multiline);
                if (versionCodeRegex.IsMatch(content))
                {
                    content = versionCodeRegex.Replace(content, $"versionCode {versionCode}");
                }
                
                // Update versionName using regex to handle any existing value
                var versionNameRegex = new System.Text.RegularExpressions.Regex(@"versionName\s+""[^""]*""", System.Text.RegularExpressions.RegexOptions.Multiline);
                if (versionNameRegex.IsMatch(content))
                {
                    content = versionNameRegex.Replace(content, $"versionName \"{versionName}\"");
                }
                
                // Collect Gradle deps from three sources (duplicates skipped):
                //   1. AndroidGradleDependency_* properties in Directory.Build.props
                //   2. platform/android/gradle-deps.txt in the project itself
                //   3. gradle-deps.txt from each active plugin
                var gradleDeps = androidProperties
                    .Where(kv => kv.Key.StartsWith("AndroidGradleDependency_"))
                    .Select(kv => kv.Value)
                    .ToList();

                string projectGradleDepsFile = Path.Combine(opts.ProjectPath, "platform", "android", "gradle-deps.txt");
                if (File.Exists(projectGradleDepsFile))
                {
                    var projectDeps = File.ReadAllLines(projectGradleDepsFile)
                        .Select(l => l.Trim())
                        .Where(l => l.Length > 0 && !l.StartsWith('#'))
                        .Where(d => !gradleDeps.Contains(d));
                    gradleDeps.AddRange(projectDeps);
                    Log.LogMessage(MessageImportance.High, "✅ Loaded Gradle deps from project platform/android/gradle-deps.txt");
                }

                foreach (string pluginAndroid in activePluginAndroidPaths)
                {
                    string gradleDepsFile = Path.Combine(pluginAndroid, "gradle-deps.txt");
                    if (!File.Exists(gradleDepsFile))
                        continue;
                    string pluginName = Path.GetFileName(Directory.GetParent(pluginAndroid)!.FullName);
                    var pluginDeps = File.ReadAllLines(gradleDepsFile)
                        .Select(l => l.Trim())
                        .Where(l => l.Length > 0 && !l.StartsWith('#'))
                        .Where(d => !gradleDeps.Contains(d));
                    gradleDeps.AddRange(pluginDeps);
                    Log.LogMessage(MessageImportance.High, $"✅ Loaded Gradle deps from {pluginName} plugin gradle-deps.txt");
                }
                if (gradleDeps.Count > 0)
                {
                    var depsBlock = new StringBuilder();
                    depsBlock.AppendLine("dependencies {");
                    foreach (var dep in gradleDeps)
                        depsBlock.AppendLine($"    implementation '{dep}'");
                    depsBlock.AppendLine("}");
                    depsBlock.AppendLine();
                    // Insert before the closing of the file (before afterEvaluate or at end)
                    int afterEvalIdx = content.IndexOf("afterEvaluate");
                    if (afterEvalIdx >= 0)
                        content = content.Insert(afterEvalIdx, depsBlock.ToString());
                    else
                        content += depsBlock.ToString();
                    Log.LogMessage(MessageImportance.High, $"✅ Injected {gradleDeps.Count} Gradle dependenc{(gradleDeps.Count == 1 ? "y" : "ies")} into build.gradle");
                }

                File.WriteAllText(buildGradlePath, content);
                Log.LogMessage(MessageImportance.High, $"✅ Updated build.gradle: versionCode={versionCode}, versionName={versionName}");
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
                    
                    // Ensure android.useAndroidX=true is present (required for androidx.* deps)
                    if (!content.Contains("android.useAndroidX"))
                        content += "\nandroid.useAndroidX=true\n";

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

            // Inject runtime permission requests for any declared dangerous permissions
            ConfigureJavaRuntimePermissions(androidPath, androidProperties);
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

            string projectFile = Path.GetFullPath(Path.Combine(opts.ProjectPath, $"{PROJECT_NAME}.csproj"));

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

        // All three (runtimeId, abiName) pairs supported by the build.
        private static readonly (string runtimeId, string abiName)[] AllArchitectures =
        {
            ("linux-bionic-arm64", "arm64-v8a"),
            ("linux-bionic-arm",   "armeabi-v7a"),
            ("linux-bionic-x64",   "x86_64")
        };

        /// <summary>
        /// Locates the adb executable via the Android SDK path or system PATH.
        /// Returns an empty string if adb cannot be found.
        /// </summary>
        private string FindAdbPath()
        {
            var candidates = new List<string> { "adb" };
            string androidSdk = GetAndroidSdkPath();
            if (!string.IsNullOrEmpty(androidSdk))
            {
                candidates.Add(Path.Combine(androidSdk, "platform-tools", "adb"));
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    candidates.Add(Path.Combine(androidSdk, "platform-tools", "adb.exe"));
            }
            foreach (var candidate in candidates)
            {
                try
                {
                    if (File.Exists(candidate))
                        return candidate;
                    var r = Cli.Wrap(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "where" : "which")
                        .WithArguments(candidate)
                        .WithValidation(CommandResultValidation.None)
                        .ExecuteBufferedAsync().GetAwaiter().GetResult();
                    if (r.ExitCode == 0)
                        return candidate;
                }
                catch { }
            }
            return string.Empty;
        }

        /// <summary>
        /// Returns the set of (runtimeId, abiName) pairs that must be built.
        /// When <c>opts.Install</c> is true, queries the connected device(s) via adb and returns only
        /// the architectures those devices actually need, shortening compile time.
        /// Falls back to all three architectures if adb is unavailable, no devices are found,
        /// or ABI detection fails.
        /// When <c>opts.Install</c> is false (pure build / distribution), always returns all three.
        /// </summary>
        private (string runtimeId, string abiName)[] GetTargetArchitectures()
        {
            if (!opts.DeviceArchs)
                return AllArchitectures;

            string adbPath = FindAdbPath();
            if (string.IsNullOrEmpty(adbPath))
            {
                Log.LogMessage(MessageImportance.High, "⚠️  adb not found – building all architectures");
                return AllArchitectures;
            }

            // Collect target device IDs
            var targetDeviceIds = new List<string>();
            if (!string.IsNullOrEmpty(opts.DeviceId))
            {
                targetDeviceIds.Add(opts.DeviceId);
            }
            else
            {
                try
                {
                    var r = Cli.Wrap(adbPath)
                        .WithArguments("devices")
                        .WithValidation(CommandResultValidation.None)
                        .ExecuteBufferedAsync().GetAwaiter().GetResult();
                    foreach (var line in r.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (line.Contains("List of devices") || string.IsNullOrWhiteSpace(line)) continue;
                        var parts = System.Text.RegularExpressions.Regex.Split(line.Trim(), @"\s+");
                        if (parts.Length >= 2 && parts[1] == "device")
                            targetDeviceIds.Add(parts[0]);
                    }
                }
                catch (Exception ex)
                {
                    Log.LogMessage(MessageImportance.High, $"⚠️  Failed to enumerate Android devices: {ex.Message} – building all architectures");
                    return AllArchitectures;
                }
            }

            if (targetDeviceIds.Count == 0)
            {
                Log.LogMessage(MessageImportance.High, "⚠️  No connected Android devices found – building all architectures");
                return AllArchitectures;
            }

            // Union of primary ABIs across all target devices.
            // We use ro.product.cpu.abi (the single native ABI, e.g. "arm64-v8a") rather than
            // ro.product.cpu.abilist, which lists all *compatibility* ABIs and would cause us to
            // build armeabi-v7a even on a pure arm64 device.
            var requiredAbis = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string deviceId in targetDeviceIds)
            {
                try
                {
                    var r = Cli.Wrap(adbPath)
                        .WithArguments($"-s {deviceId} shell getprop ro.product.cpu.abi")
                        .WithValidation(CommandResultValidation.None)
                        .ExecuteBufferedAsync().GetAwaiter().GetResult();
                    string primaryAbi = r.StandardOutput.Trim().Replace("\r", "").Replace("\n", "");
                    if (!string.IsNullOrEmpty(primaryAbi))
                    {
                        requiredAbis.Add(primaryAbi);
                        Log.LogMessage(MessageImportance.High, $"📱 Device {deviceId}: primary ABI = {primaryAbi}");
                    }
                }
                catch (Exception ex)
                {
                    Log.LogMessage(MessageImportance.High, $"⚠️  Failed to query ABI for device {deviceId}: {ex.Message}");
                }
            }

            if (requiredAbis.Count == 0)
            {
                Log.LogMessage(MessageImportance.High, "⚠️  Could not determine device ABI(s) – building all architectures");
                return AllArchitectures;
            }

            // armeabi devices are covered by the linux-bionic-arm (armeabi-v7a) build
            var filtered = AllArchitectures
                .Where(a => requiredAbis.Contains(a.abiName) ||
                            (a.abiName == "armeabi-v7a" && requiredAbis.Contains("armeabi")))
                .ToArray();

            if (filtered.Length == 0)
            {
                Log.LogMessage(MessageImportance.High,
                    $"⚠️  No architecture match for device ABI(s) [{string.Join(", ", requiredAbis)}] – building all architectures");
                return AllArchitectures;
            }

            var skipped = AllArchitectures.Except(filtered).Select(a => a.abiName);
            Log.LogMessage(MessageImportance.High,
                $"🎯 Device ABI filter: building [{string.Join(", ", filtered.Select(a => a.abiName))}], skipping [{string.Join(", ", skipped)}]");
            return filtered;
        }

        void PublishAssemblies(string buildType)
        {
            var architectures = GetTargetArchitectures();

            // Determine configuration
            string configuration = buildType == "release" ? "Release" : "Debug";

            // Add scripts directory to PATH for android_fake_clang.cmd access on Windows
            string sokolNetHome = Utils.GetSokolNetHome();
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

            foreach (var (arch, abiName) in architectures)
            {
                Log.LogMessage(MessageImportance.High, $"Publishing for {arch} ({abiName})...");

                try
                {
                    string projectFile = Path.GetFullPath(Path.Combine(opts.ProjectPath, $"{PROJECT_NAME}.csproj"));

                    // Include DEBUG symbol for Debug builds (semicolon must be URL-encoded for MSBuild)
                    // Also include __ANDROID_ARM32__ for 32-bit ARM builds — needed for struct layouts
                    // (e.g. NSVGpaint) that differ between 32-bit and 64-bit pointer sizes.
                    string arm32Extra = arch == "linux-bionic-arm" ? "__ANDROID_ARM32__%3B" : "";
                    string defineConstants = configuration == "Debug" ? $"{arm32Extra}__ANDROID__%3BDEBUG" : $"{arm32Extra}__ANDROID__";

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
                        
                        // Copy the published library to the Android libs directory.
                        // The CMakeLists.txt template uses PREBUILT_LIB_PATH = ../../../libs (relative to
                        // app/src/main/cpp/) which resolves to app/libs/ - so we must copy here.
                        string publishDir = Path.Combine(opts.ProjectPath, "bin", configuration, "net10.0", arch, "publish");
                        string libsDir = Path.Combine(opts.ProjectPath, "Android", "native-activity", "app", "libs");
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
            
            // jniLibs is the standard Gradle source set that gets packaged into the APK automatically.
            // Using app/libs/ would NOT package the .so files into the APK.
            string libsDir = Path.Combine(opts.ProjectPath, "Android", "native-activity", "app", "src", "main", "jniLibs");
            
            // Architectures to process – filtered to connected device(s) when installing
            var architectures = GetTargetArchitectures();

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
                    
                    // Check if there's an explicit library name override (for names with special characters like c++)
                    string libraryNamePropertyKey = $"AndroidNativeLibrary_{libraryNamePart}LibraryName";
                    string libraryName = androidProperties.ContainsKey(libraryNamePropertyKey)
                        ? androidProperties[libraryNamePropertyKey]
                        : libraryNamePart.ToLowerInvariant();
                    
                    string libraryBasePath = property.Value;

                    // Expand MSBuild variables that the XML reader leaves unexpanded
                    libraryBasePath = libraryBasePath.Replace("$(SokolNetHome)", Utils.GetSokolNetHome(), StringComparison.OrdinalIgnoreCase);
                    libraryBasePath = libraryBasePath.Replace("$(HomeDir)", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), StringComparison.OrdinalIgnoreCase);

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

        // Copies extra Java/Kotlin source trees into the app's src/main/java so they
        // are compiled into the APK. Driven by AndroidJavaSource_<name>Path properties
        // (mirrors AndroidNativeLibrary_*Path). Used e.g. for the NearNet JNI bridge
        // class (com.sokol.nearnet.NearNetBle), which the native .so calls via JNI.
        void CopyJavaSources()
        {
            string javaDestDir = Path.Combine(opts.ProjectPath, "Android", "native-activity", "app", "src", "main", "java");
            int sourcesProcessed = 0;

            foreach (var property in androidProperties)
            {
                if (!property.Key.StartsWith("AndroidJavaSource_", StringComparison.OrdinalIgnoreCase) ||
                    !property.Key.EndsWith("Path", StringComparison.OrdinalIgnoreCase))
                    continue;

                string name = property.Key.Substring("AndroidJavaSource_".Length);
                name = name.Substring(0, name.Length - "Path".Length);

                // Resolve the path the same way native library paths are resolved.
                string basePath = property.Value;
                basePath = basePath.Replace("$(SokolNetHome)", Utils.GetSokolNetHome(), StringComparison.OrdinalIgnoreCase);
                basePath = basePath.Replace("$(HomeDir)", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), StringComparison.OrdinalIgnoreCase);
                if (!Path.IsPathRooted(basePath))
                    basePath = Path.GetFullPath(Path.Combine(opts.ProjectPath, basePath));

                if (!Directory.Exists(basePath))
                {
                    Log.LogMessage(MessageImportance.Normal, $"ℹ️  Java source path for '{name}' not found: {basePath}");
                    continue;
                }

                Log.LogMessage(MessageImportance.High, $"☕ Copying Java sources '{name}' from: {basePath}");
                int copied = 0;
                foreach (string srcFile in Directory.GetFiles(basePath, "*.java", SearchOption.AllDirectories))
                {
                    string relative = Path.GetRelativePath(basePath, srcFile);   // preserves package dirs
                    string destFile = Path.Combine(javaDestDir, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(destFile)!);
                    File.Copy(srcFile, destFile, true);
                    copied++;
                }
                Log.LogMessage(MessageImportance.Normal, $"✅ Copied {copied} Java file(s) for '{name}' into app/src/main/java");
                sourcesProcessed++;
            }

            if (sourcesProcessed == 0)
                Log.LogMessage(MessageImportance.Normal, "ℹ️  No extra Java source configurations found (AndroidJavaSource_*Path properties)");
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
            
            // Build the library directory addition and link library names
            var libraryNames = new List<string>();
            var additionalLinkLibraries = new List<string>();
            bool hasLinkDirectories = false;

            foreach (var property in androidProperties)
            {
                if (property.Key.StartsWith("AndroidNativeLibrary_", StringComparison.OrdinalIgnoreCase) &&
                    property.Key.EndsWith("Path", StringComparison.OrdinalIgnoreCase))
                {
                    // Extract library name
                    string libraryNamePart = property.Key.Substring("AndroidNativeLibrary_".Length);
                    libraryNamePart = libraryNamePart.Substring(0, libraryNamePart.Length - "Path".Length);
                    
                    // Check if there's an explicit library name override (for names with special characters like c++)
                    string libraryNamePropertyKey = $"AndroidNativeLibrary_{libraryNamePart}LibraryName";
                    string libraryName = androidProperties.ContainsKey(libraryNamePropertyKey)
                        ? androidProperties[libraryNamePropertyKey]
                        : libraryNamePart.ToLowerInvariant();
                    
                    libraryNames.Add(libraryName);
                    
                    // For linking, use just the library name (without lib prefix or .so suffix)
                    // CMake will add -l<name> which will use runtime library search paths
                    additionalLinkLibraries.Add($"    {libraryName}");
                    
                    if (!hasLinkDirectories)
                    {
                        hasLinkDirectories = true;
                    }
                }
            }

            if (libraryNames.Count > 0)
            {
                // Add link_directories command BEFORE add_library(sokol ...) so it takes effect
                // link_directories only affects targets defined AFTER it's called
                string insertionPoint = "set(CMAKE_SHARED_LINKER_FLAGS";
                int insertIndex = cmakeContent.IndexOf(insertionPoint);
                
                if (insertIndex >= 0)
                {
                    // Find the end of the next line (after the closing parenthesis)
                    int searchStart = cmakeContent.IndexOf(')', insertIndex) + 1;
                    int lineEnd = cmakeContent.IndexOf('\n', searchStart);
                    if (lineEnd >= 0)
                    {
                        string linkDirectoriesText = $@"

# Define library path for prebuilt libraries - points to jniLibs which Gradle packages into the APK.
# CMakeLists.txt is at app/src/main/cpp/, so ../jniLibs = app/src/main/jniLibs/
set(PREBUILT_LIB_PATH ${{CMAKE_CURRENT_SOURCE_DIR}}/../jniLibs)

# Add search directory for native libraries so they are linked with -l instead of absolute paths
# This prevents absolute build paths from being embedded in the .so file
# IMPORTANT: This must come BEFORE add_library(sokol ...) to take effect
link_directories(${{PREBUILT_LIB_PATH}}/${{ANDROID_ABI}})

# Note: Libraries will be linked by name (e.g., -lcamerac) which uses runtime linker
# The runtime linker will search using RPATH ($ORIGIN) set in target_link_options below
";
                        cmakeContent = cmakeContent.Insert(lineEnd + 1, linkDirectoriesText);
                        
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
                        Log.LogMessage(MessageImportance.Normal, $"📝 Updated CMakeLists.txt with {libraryNames.Count} additional native libraries using link_directories");
                        Log.LogMessage(MessageImportance.Normal, $"   Libraries: {string.Join(", ", libraryNames)}");
                    }
                    else
                    {
                        Log.LogMessage(MessageImportance.Normal, "⚠️  Could not find line end after CMAKE_SHARED_LINKER_FLAGS");
                    }
                }
                else
                {
                    Log.LogMessage(MessageImportance.Normal, "⚠️  Could not find CMAKE_SHARED_LINKER_FLAGS in CMakeLists.txt");
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
                    
                    // Check if there's an explicit library name override (for names with special characters like c++)
                    string libraryNamePropertyKey = $"AndroidNativeLibrary_{libraryNamePart}LibraryName";
                    string libraryName = androidProperties.ContainsKey(libraryNamePropertyKey)
                        ? androidProperties[libraryNamePropertyKey]
                        : libraryNamePart.ToLowerInvariant();
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
                
                // Load only explicitly declared native libraries (in declaration order)
                // c++_shared is only loaded if explicitly listed via AndroidNativeLibrary_*LibraryName
                foreach (string libName in nativeLibraries)
                {
                    loadStatements.Add($"            // Load {libName} library");
                    loadStatements.Add($"            System.loadLibrary(\"{libName}\");");
                }
                
                // Add sokol last
                loadStatements.Add("            // Load main sokol library last");
                loadStatements.Add("            System.loadLibrary(\"sokol\");");
                
                // Build fallback statements (try each library individually so sokol still loads)
                var fallbackStatements = new List<string>();
                foreach (string libName in nativeLibraries)
                {
                    fallbackStatements.Add($"            try {{ System.loadLibrary(\"{libName}\"); }} catch (UnsatisfiedLinkError ignored) {{}}");
                }
                fallbackStatements.Add("            System.loadLibrary(\"sokol\");");
                
                // Create the new static block with per-library fallback handling
                string newPattern = $@"    // Load native library early so JNI methods are available
    static {{
        try {{
{string.Join("\n", loadStatements)}
        }} catch (UnsatisfiedLinkError e) {{
            // Fallback: attempt each library individually so sokol can still load
{string.Join("\n", fallbackStatements)}
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

        // Map of Android dangerous permissions that require runtime requests (API 23+)
        // Key: full permission name, Value: java Manifest constant (fully qualified, no import needed)
        static readonly Dictionary<string, string> DangerousPermissions = new(StringComparer.OrdinalIgnoreCase)
        {
            { "android.permission.CAMERA",                    "android.Manifest.permission.CAMERA" },
            { "android.permission.RECORD_AUDIO",              "android.Manifest.permission.RECORD_AUDIO" },
            { "android.permission.ACCESS_FINE_LOCATION",      "android.Manifest.permission.ACCESS_FINE_LOCATION" },
            { "android.permission.ACCESS_COARSE_LOCATION",    "android.Manifest.permission.ACCESS_COARSE_LOCATION" },
            { "android.permission.ACCESS_BACKGROUND_LOCATION","android.Manifest.permission.ACCESS_BACKGROUND_LOCATION" },
            { "android.permission.READ_EXTERNAL_STORAGE",     "android.Manifest.permission.READ_EXTERNAL_STORAGE" },
            { "android.permission.WRITE_EXTERNAL_STORAGE",    "android.Manifest.permission.WRITE_EXTERNAL_STORAGE" },
            { "android.permission.READ_MEDIA_IMAGES",         "android.Manifest.permission.READ_MEDIA_IMAGES" },
            { "android.permission.READ_MEDIA_VIDEO",          "android.Manifest.permission.READ_MEDIA_VIDEO" },
            { "android.permission.READ_MEDIA_AUDIO",          "android.Manifest.permission.READ_MEDIA_AUDIO" },
            { "android.permission.READ_CONTACTS",             "android.Manifest.permission.READ_CONTACTS" },
            { "android.permission.WRITE_CONTACTS",            "android.Manifest.permission.WRITE_CONTACTS" },
            { "android.permission.GET_ACCOUNTS",              "android.Manifest.permission.GET_ACCOUNTS" },
            { "android.permission.CALL_PHONE",                "android.Manifest.permission.CALL_PHONE" },
            { "android.permission.READ_PHONE_STATE",          "android.Manifest.permission.READ_PHONE_STATE" },
            { "android.permission.READ_CALL_LOG",             "android.Manifest.permission.READ_CALL_LOG" },
            { "android.permission.WRITE_CALL_LOG",            "android.Manifest.permission.WRITE_CALL_LOG" },
            { "android.permission.SEND_SMS",                  "android.Manifest.permission.SEND_SMS" },
            { "android.permission.RECEIVE_SMS",               "android.Manifest.permission.RECEIVE_SMS" },
            { "android.permission.READ_SMS",                  "android.Manifest.permission.READ_SMS" },
            { "android.permission.BODY_SENSORS",              "android.Manifest.permission.BODY_SENSORS" },
            { "android.permission.ACTIVITY_RECOGNITION",      "android.Manifest.permission.ACTIVITY_RECOGNITION" },
            { "android.permission.BLUETOOTH_SCAN",            "android.Manifest.permission.BLUETOOTH_SCAN" },
            { "android.permission.BLUETOOTH_CONNECT",         "android.Manifest.permission.BLUETOOTH_CONNECT" },
            { "android.permission.NEARBY_WIFI_DEVICES",       "android.Manifest.permission.NEARBY_WIFI_DEVICES" },
            { "android.permission.POST_NOTIFICATIONS",        "android.Manifest.permission.POST_NOTIFICATIONS" },
            { "android.permission.UWB_RANGING",               "android.Manifest.permission.UWB_RANGING" },
        };

        /// <summary>
        /// Dynamically injects runtime permission request code into SokolNativeActivity.java for any
        /// "dangerous" permissions declared in AndroidPermissions (Directory.Build.props).
        /// Normal/install-time permissions need only a manifest entry and are skipped.
        /// </summary>
        void ConfigureJavaRuntimePermissions(string androidPath, Dictionary<string, string> androidProperties)
        {
            string javaActivityPath = Path.Combine(androidPath, "app", "src", "main", "java", "com", "sokol", "app", "SokolNativeActivity.java");

            if (!File.Exists(javaActivityPath))
            {
                Log.LogMessage(MessageImportance.Normal, "⚠️  SokolNativeActivity.java not found, skipping runtime permissions configuration");
                return;
            }

            string content = File.ReadAllText(javaActivityPath);

            // Verify placeholders are present (they come from the template)
            bool hasRequestPlaceholder  = content.Contains("// @TEMPLATE_RUNTIME_PERMISSIONS_REQUEST@");
            bool hasCallbackPlaceholder = content.Contains("// @TEMPLATE_RUNTIME_PERMISSIONS_CALLBACK@");

            if (!hasRequestPlaceholder && !hasCallbackPlaceholder)
            {
                Log.LogMessage(MessageImportance.Normal, "ℹ️  Runtime permission placeholders not found in SokolNativeActivity.java (pre-template file?)");
                return;
            }

            // Determine which declared permissions are dangerous (need runtime request)
            var declaredPermissions = GetAndroidPermissions(androidProperties);
            var runtimePermissions = declaredPermissions
                .Where(p => DangerousPermissions.ContainsKey(p))
                .ToList();

            if (runtimePermissions.Count == 0)
            {
                // No dangerous permissions – remove placeholders cleanly
                content = content.Replace("\n        // @TEMPLATE_RUNTIME_PERMISSIONS_REQUEST@\n", "\n");
                content = content.Replace("\n    // @TEMPLATE_RUNTIME_PERMISSIONS_CALLBACK@\n", "\n");
                File.WriteAllText(javaActivityPath, content);
                Log.LogMessage(MessageImportance.Normal, "ℹ️  No dangerous permissions declared – no runtime permission request needed");
                return;
            }

            // ── Build the permission request block ──────────────────────────────────
            var requestLines = new List<string>();
            requestLines.Add("        // Request dangerous permissions at runtime (Activity.requestPermissions, API 23+)");
            requestLines.Add("        {");
            requestLines.Add("            java.util.ArrayList<String> _permsToRequest = new java.util.ArrayList<>();");
            foreach (string perm in runtimePermissions)
            {
                string manifestConst = DangerousPermissions[perm];
                requestLines.Add($"            if (checkSelfPermission({manifestConst}) != android.content.pm.PackageManager.PERMISSION_GRANTED) {{");
                requestLines.Add($"                _permsToRequest.add({manifestConst});");
                requestLines.Add("            }");
            }
            requestLines.Add("            if (!_permsToRequest.isEmpty()) {");
            requestLines.Add("                requestPermissions(_permsToRequest.toArray(new String[0]), 1001);");
            requestLines.Add("            }");
            requestLines.Add("        }");

            string requestCode = string.Join("\n", requestLines);

            // ── Build the onRequestPermissionsResult callback ───────────────────────
            var callbackLines = new List<string>();
            callbackLines.Add("    @Override");
            callbackLines.Add("    public void onRequestPermissionsResult(int requestCode, String[] permissions, int[] grantResults) {");
            callbackLines.Add("        super.onRequestPermissionsResult(requestCode, permissions, grantResults);");
            callbackLines.Add("        // Permission result received; native code will react on the next cam_update() / sensor poll cycle");
            callbackLines.Add("    }");

            string callbackCode = string.Join("\n", callbackLines);

            // Replace placeholders
            if (hasRequestPlaceholder)
                content = content.Replace("        // @TEMPLATE_RUNTIME_PERMISSIONS_REQUEST@", requestCode);

            if (hasCallbackPlaceholder)
                content = content.Replace("    // @TEMPLATE_RUNTIME_PERMISSIONS_CALLBACK@", callbackCode);

            File.WriteAllText(javaActivityPath, content);
            Log.LogMessage(MessageImportance.High, $"✅ Injected runtime permission requests for: {string.Join(", ", runtimePermissions)}");
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

        class KeystoreInfo
        {
            public string KeystorePath { get; set; }
            public string StorePassword { get; set; }
            public string KeyPassword { get; set; }
            public string KeyAlias { get; set; }
        }

        KeystoreInfo EnsureReleaseKeystore()
        {
            string configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".sokolnet_config");
            string configFile = Path.Combine(configDir, "release_keystore.config");
            
            // Try to load existing configuration
            if (File.Exists(configFile))
            {
                try
                {
                    var lines = File.ReadAllLines(configFile);
                    var config = new Dictionary<string, string>();
                    foreach (var line in lines)
                    {
                        var parts = line.Split('=', 2);
                        if (parts.Length == 2)
                        {
                            config[parts[0].Trim()] = parts[1].Trim();
                        }
                    }
                    
                    if (config.ContainsKey("KeystorePath") && 
                        config.ContainsKey("StorePassword") && 
                        config.ContainsKey("KeyPassword") && 
                        config.ContainsKey("KeyAlias"))
                    {
                        string keystorePath = config["KeystorePath"];
                        if (File.Exists(keystorePath))
                        {
                            Log.LogMessage(MessageImportance.High, $"✅ Using existing release keystore: {keystorePath}");
                            return new KeystoreInfo
                            {
                                KeystorePath = keystorePath,
                                StorePassword = config["StorePassword"],
                                KeyPassword = config["KeyPassword"],
                                KeyAlias = config["KeyAlias"]
                            };
                        }
                        else
                        {
                            Log.LogWarning($"⚠️ Configured keystore not found: {keystorePath}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.LogWarning($"Failed to read keystore config: {ex.Message}");
                }
            }
            
            // Prompt user for keystore setup
            Log.LogMessage(MessageImportance.High, "");
            Log.LogMessage(MessageImportance.High, "==============================================");
            Log.LogMessage(MessageImportance.High, "  RELEASE KEYSTORE SETUP REQUIRED");
            Log.LogMessage(MessageImportance.High, "==============================================");
            Log.LogMessage(MessageImportance.High, "");

            // In CI environments there is no interactive terminal.
            // Auto-generate a temporary keystore so the APK can be built and
            // the native .so libraries extracted.  The resulting APK is never
            // published, so proper Google Play signing is not required here.
            bool isCi = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI"))
                     || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"));
            if (isCi)
            {
                Log.LogMessage(MessageImportance.High, "CI environment detected — generating temporary keystore for library extraction build.");
                string ciKeystoreDir = Path.Combine(Path.GetTempPath(), "sokol_ci_keystore");
                Directory.CreateDirectory(ciKeystoreDir);
                string ciKeystorePath = Path.Combine(ciKeystoreDir, "ci-release.keystore");
                string ciStorePass    = "cikeystorepass";
                string ciKeyAlias     = "releasekey";
                string ciKeyPass      = "cikeystorepass";

                if (!File.Exists(ciKeystorePath))
                {
                    Log.LogMessage(MessageImportance.High, $"Generating temporary CI keystore at: {ciKeystorePath}");
                    var keytoolResult = Cli.Wrap("keytool")
                        .WithArguments(new[]
                        {
                            "-genkeypair", "-v",
                            "-keystore",   ciKeystorePath,
                            "-alias",      ciKeyAlias,
                            "-keyalg",     "RSA",
                            "-keysize",    "2048",
                            "-validity",   "1",
                            "-storepass",  ciStorePass,
                            "-keypass",    ciKeyPass,
                            "-dname",      "CN=CI Build, OU=CI, O=CI, L=CI, S=CI, C=US",
                            "-noprompt"
                        })
                        .WithStandardOutputPipe(PipeTarget.ToDelegate(s => Log.LogMessage(MessageImportance.Normal, s)))
                        .WithStandardErrorPipe(PipeTarget.ToDelegate(s => Log.LogMessage(MessageImportance.Normal, s)))
                        .WithValidation(CommandResultValidation.None)
                        .ExecuteAsync()
                        .GetAwaiter()
                        .GetResult();

                    if (keytoolResult.ExitCode != 0 || !File.Exists(ciKeystorePath))
                    {
                        Log.LogError("CI: Failed to generate temporary keystore with keytool.");
                        return null;
                    }
                }

                Log.LogMessage(MessageImportance.High, "✅ CI temporary keystore ready.");
                return new KeystoreInfo
                {
                    KeystorePath  = ciKeystorePath,
                    StorePassword = ciStorePass,
                    KeyPassword   = ciKeyPass,
                    KeyAlias      = ciKeyAlias
                };
            }

            Log.LogMessage(MessageImportance.High, "To sign your app for Google Play release, you need a release keystore.");
            Log.LogMessage(MessageImportance.High, "");
            Log.LogMessage(MessageImportance.High, "Options:");
            Log.LogMessage(MessageImportance.High, "  1. Create a new release keystore (recommended for first-time release)");
            Log.LogMessage(MessageImportance.High, "  2. Use an existing release keystore (if you've released this app before)");
            Log.LogMessage(MessageImportance.High, "");
            Console.Write("Enter your choice (1 or 2): ");
            string choice = Console.ReadLine()?.Trim();
            
            KeystoreInfo keystoreInfo = null;
            
            if (choice == "1")
            {
                // Create new keystore
                Log.LogMessage(MessageImportance.High, "");
                Log.LogMessage(MessageImportance.High, "Creating new release keystore...");
                Log.LogMessage(MessageImportance.High, "");
                
                Console.Write("Enter keystore file path (or press Enter for default ~/.android/release.keystore): ");
                string keystorePath = Console.ReadLine()?.Trim();
                if (string.IsNullOrEmpty(keystorePath))
                {
                    keystorePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".android", "release.keystore");
                }
                
                // Make sure directory exists
                Directory.CreateDirectory(Path.GetDirectoryName(keystorePath));
                
                Console.Write("Enter keystore password (min 6 characters): ");
                string storePassword = ReadPassword();
                while (storePassword.Length < 6)
                {
                    Log.LogMessage(MessageImportance.High, "Password must be at least 6 characters!");
                    Console.Write("Enter keystore password: ");
                    storePassword = ReadPassword();
                }
                
                Console.Write("Confirm keystore password: ");
                string confirmStorePassword = ReadPassword();
                while (storePassword != confirmStorePassword)
                {
                    Log.LogMessage(MessageImportance.High, "Passwords don't match!");
                    Console.Write("Confirm keystore password: ");
                    confirmStorePassword = ReadPassword();
                }
                
                Console.Write("Enter key alias (default: releasekey): ");
                string keyAlias = Console.ReadLine()?.Trim();
                if (string.IsNullOrEmpty(keyAlias))
                {
                    keyAlias = "releasekey";
                }
                
                Console.Write("Enter key password (min 6 characters, or press Enter to use same as keystore): ");
                string keyPassword = ReadPassword();
                if (string.IsNullOrEmpty(keyPassword))
                {
                    keyPassword = storePassword;
                }
                while (keyPassword.Length < 6)
                {
                    Log.LogMessage(MessageImportance.High, "Password must be at least 6 characters!");
                    Console.Write("Enter key password: ");
                    keyPassword = ReadPassword();
                }
                
                Console.Write("Enter your name (CN): ");
                string cn = Console.ReadLine()?.Trim();
                if (string.IsNullOrEmpty(cn))
                {
                    cn = "Unknown";
                }
                
                Console.Write("Enter your organizational unit (OU, optional): ");
                string ou = Console.ReadLine()?.Trim();
                
                Console.Write("Enter your organization (O, optional): ");
                string o = Console.ReadLine()?.Trim();
                
                Console.Write("Enter your city/locality (L, optional): ");
                string l = Console.ReadLine()?.Trim();
                
                Console.Write("Enter your state/province (ST, optional): ");
                string st = Console.ReadLine()?.Trim();
                
                Console.Write("Enter your country code (C, 2 letters, optional): ");
                string c = Console.ReadLine()?.Trim();
                
                // Build DN string
                var dnParts = new List<string> { $"CN={cn}" };
                if (!string.IsNullOrEmpty(ou)) dnParts.Add($"OU={ou}");
                if (!string.IsNullOrEmpty(o)) dnParts.Add($"O={o}");
                if (!string.IsNullOrEmpty(l)) dnParts.Add($"L={l}");
                if (!string.IsNullOrEmpty(st)) dnParts.Add($"ST={st}");
                if (!string.IsNullOrEmpty(c)) dnParts.Add($"C={c}");
                string dname = string.Join(", ", dnParts);
                
                // Generate keystore
                Log.LogMessage(MessageImportance.High, "");
                Log.LogMessage(MessageImportance.High, "Generating release keystore...");
                
                var keystoreResult = Cli.Wrap("keytool")
                    .WithArguments($"-genkeypair -v -keystore \"{keystorePath}\" -storepass {storePassword} -alias {keyAlias} -keypass {keyPassword} -keyalg RSA -keysize 2048 -validity 10000 -dname \"{dname}\"")
                    .WithStandardOutputPipe(PipeTarget.ToDelegate(s => Log.LogMessage(MessageImportance.Normal, s)))
                    .WithStandardErrorPipe(PipeTarget.ToDelegate(s => Log.LogError(s)))
                    .ExecuteAsync()
                    .GetAwaiter()
                    .GetResult();
                
                if (keystoreResult.ExitCode != 0)
                {
                    Log.LogError("Failed to create keystore!");
                    return null;
                }
                
                keystoreInfo = new KeystoreInfo
                {
                    KeystorePath = keystorePath,
                    StorePassword = storePassword,
                    KeyPassword = keyPassword,
                    KeyAlias = keyAlias
                };
                
                Log.LogMessage(MessageImportance.High, "✅ Release keystore created successfully!");
            }
            else if (choice == "2")
            {
                // Use existing keystore
                Log.LogMessage(MessageImportance.High, "");
                Log.LogMessage(MessageImportance.High, "Using existing release keystore...");
                Log.LogMessage(MessageImportance.High, "");
                
                Console.Write("Enter keystore file path: ");
                string keystorePath = Console.ReadLine()?.Trim();
                
                while (!File.Exists(keystorePath))
                {
                    Log.LogMessage(MessageImportance.High, $"Keystore not found: {keystorePath}");
                    Console.Write("Enter keystore file path: ");
                    keystorePath = Console.ReadLine()?.Trim();
                }
                
                Console.Write("Enter keystore password: ");
                string storePassword = ReadPassword();
                
                Console.Write("Enter key alias: ");
                string keyAlias = Console.ReadLine()?.Trim();
                
                Console.Write("Enter key password (or press Enter if same as keystore password): ");
                string keyPassword = ReadPassword();
                if (string.IsNullOrEmpty(keyPassword))
                {
                    keyPassword = storePassword;
                }
                
                keystoreInfo = new KeystoreInfo
                {
                    KeystorePath = keystorePath,
                    StorePassword = storePassword,
                    KeyPassword = keyPassword,
                    KeyAlias = keyAlias
                };
                
                Log.LogMessage(MessageImportance.High, "✅ Using existing release keystore!");
            }
            else
            {
                Log.LogError("Invalid choice!");
                return null;
            }
            
            // Save configuration
            if (keystoreInfo != null)
            {
                try
                {
                    Directory.CreateDirectory(configDir);
                    File.WriteAllText(configFile, $@"KeystorePath={keystoreInfo.KeystorePath}
StorePassword={keystoreInfo.StorePassword}
KeyPassword={keystoreInfo.KeyPassword}
KeyAlias={keystoreInfo.KeyAlias}
");
                    Log.LogMessage(MessageImportance.High, $"✅ Keystore configuration saved to: {configFile}");
                    Log.LogMessage(MessageImportance.High, "");
                    Log.LogMessage(MessageImportance.High, "⚠️  IMPORTANT: Keep your keystore and passwords secure!");
                    Log.LogMessage(MessageImportance.High, "   If you lose them, you won't be able to update your app on Google Play.");
                    Log.LogMessage(MessageImportance.High, "");
                }
                catch (Exception ex)
                {
                    Log.LogWarning($"Failed to save keystore config: {ex.Message}");
                }
            }
            
            return keystoreInfo;
        }
        
        string ReadPassword()
        {
            var password = new StringBuilder();
            while (true)
            {
                var key = Console.ReadKey(true);
                if (key.Key == ConsoleKey.Enter)
                {
                    Console.WriteLine();
                    break;
                }
                else if (key.Key == ConsoleKey.Backspace && password.Length > 0)
                {
                    password.Remove(password.Length - 1, 1);
                    Console.Write("\b \b");
                }
                else if (!char.IsControl(key.KeyChar))
                {
                    password.Append(key.KeyChar);
                    Console.Write("*");
                }
            }
            return password.ToString();
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

            // Get release keystore information
            var keystoreInfo = EnsureReleaseKeystore();
            if (keystoreInfo == null)
            {
                Log.LogError("Failed to obtain release keystore information!");
                return;
            }

            string debugKeystore = keystoreInfo.KeystorePath;

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
                            .WithArguments($"sign --ks \"{debugKeystore}\" --ks-pass pass:{keystoreInfo.StorePassword} --key-pass pass:{keystoreInfo.KeyPassword} --out \"{signedApkPath}\" \"{unsignedApkPath}\"")
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
                    .WithArguments($"-keystore \"{debugKeystore}\" -storepass {keystoreInfo.StorePassword} -keypass {keystoreInfo.KeyPassword} -digestalg SHA-256 -sigalg SHA256withRSA \"{signedApkPath}\" {keystoreInfo.KeyAlias}")
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

            // Get release keystore information
            var keystoreInfo = EnsureReleaseKeystore();
            if (keystoreInfo == null)
            {
                Log.LogError("Failed to obtain release keystore information!");
                return;
            }

            // Sign the AAB with jarsigner (AAB files use JAR signing)
            var jarsignerResult = Cli.Wrap("jarsigner")
                .WithArguments($"-keystore \"{keystoreInfo.KeystorePath}\" -storepass {keystoreInfo.StorePassword} -keypass {keystoreInfo.KeyPassword} -digestalg SHA-256 -sigalg SHA256withRSA \"{aabPath}\" {keystoreInfo.KeyAlias}")
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

        string GenerateAndroidManifest(string appName, Dictionary<string, string> androidProperties, List<string> activePluginAndroidPaths)
        {
            var manifest = new StringBuilder();
            manifest.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            manifest.AppendLine("<!-- BEGIN_INCLUDE(manifest) -->");
            manifest.AppendLine("<manifest xmlns:android=\"http://schemas.android.com/apk/res/android\"");
            
            // Version from Directory.Build.props (defaults to "1.0")
            string appVersion = androidProperties.GetValueOrDefault("AppVersion", "1.0");
            
            // versionCode (build number) - read from AndroidVersionCode property or derive from version string
            string versionCode;
            if (androidProperties.TryGetValue("AndroidVersionCode", out string? versionCodeValue) && !string.IsNullOrWhiteSpace(versionCodeValue))
            {
                versionCode = versionCodeValue;
                Log.LogMessage(MessageImportance.High, $"📱 Using AndroidVersionCode: {versionCode}");
            }
            else
            {
                // Fallback: derive from version string (e.g., "1.0" -> 1, "1.2.3" -> 1)
                versionCode = appVersion.Split('.')[0];
                Log.LogMessage(MessageImportance.Normal, $"ℹ️  AndroidVersionCode not specified, derived from AppVersion: {versionCode}");
            }
            
            manifest.AppendLine($"    android:versionCode=\"{versionCode}\"");
            manifest.AppendLine($"          android:versionName=\"{appVersion}\">");
            manifest.AppendLine();

            // SDK versions
            string minSdk = androidProperties.GetValueOrDefault("AndroidMinSdkVersion", "26");
            string targetSdk = androidProperties.GetValueOrDefault("AndroidTargetSdkVersion", "35");
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

            // Inject Providers.xml (e.g. FileProvider) before </application>
            string providersXmlPath = Path.Combine(opts.ProjectPath, "platform/android/manifest/Providers.xml");
            if (!File.Exists(providersXmlPath))
            {
                foreach (string pluginAndroid in activePluginAndroidPaths)
                {
                    string pluginProviders = Path.Combine(pluginAndroid, "manifest", "Providers.xml");
                    if (File.Exists(pluginProviders))
                    {
                        providersXmlPath = pluginProviders;
                        break;
                    }
                }
            }
            if (File.Exists(providersXmlPath))
            {
                string extra = File.ReadAllText(providersXmlPath);
                manifest.AppendLine(extra);
            }

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
                    "android.permission.WAKE_LOCK",
                    "android.permission.INTERNET",
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
            Log.LogMessage(MessageImportance.High, "Extracting device-specific splits from AAB via bundletool...");

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
                    Log.LogMessage(MessageImportance.High, "Installing device-specific AAB splits onto device...");
                    
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
            string sokolNetHome = Utils.GetSokolNetHome();
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

            if (File.Exists(Path.Combine(opts.ProjectPath, "platform/android/manifest/Providers.xml")))
            {
                string extra = File.ReadAllText(Path.Combine(opts.ProjectPath, "platform/android/manifest/Providers.xml"));
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

