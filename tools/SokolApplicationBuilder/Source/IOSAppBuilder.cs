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
        private bool _optimizedHarness = false;   // --type release-harness: Release codegen + HARNESS define
        private string JAVA_PACKAGE_PATH = string.Empty;
        private string VERSION_CODE = string.Empty;
        private string VERSION_NAME = string.Empty;

        private string URHONET_HOME_PATH = string.Empty;

        private string DEVELOPMENT_TEAM = string.Empty;
        
        // iOS properties from Directory.Build.props
        private string iOSBundlePrefix = "com.elix22";
        // Optional <IOSBundleName>: when set, the bundle identifier becomes
        // "{IOSBundlePrefix}.{IOSBundleName}" instead of the default (which is derived from the
        // Xcode target's executable name). This decouples the bundle id from the "-ios-app"
        // executable name. When it is EMPTY the default below is used verbatim, so projects that
        // do not set the tag build byte-identically to before.
        private string iOSBundleName = string.Empty;
        // Optional <IOSDisplayName>: the name shown under the icon on the Home screen. When set it
        // becomes CFBundleDisplayName (which iOS prefers) and CFBundleName.
        //
        // It FALLS BACK to <IOSBundleName>, so an app whose brand is a single legal-in-an-identifier
        // word sets one tag, not two. Set it explicitly only when the two must differ — and they often
        // must: a bundle id may not contain a space ("Angry Birds" → com.rovio.AngryBirds), a display
        // name is localizable and freely changeable while a published bundle id is immutable, and iOS
        // truncates the icon label at ~12 characters.
        //
        // With NEITHER tag set, CFBundleDisplayName is omitted entirely and CFBundleName keeps the
        // historical "{project} iOS App", so existing projects build byte-identically to before.
        private string iOSDisplayName = string.Empty;

        /// <summary>The Home-screen label: explicit override, else the bundle name, else empty.</summary>
        private string EffectiveDisplayName =>
            !string.IsNullOrEmpty(iOSDisplayName) ? iOSDisplayName : iOSBundleName;
        // 15.0 is the floor Xcode 27 accepts: it rejects IPHONEOS_DEPLOYMENT_TARGET below 15.0
        // ("the range of supported deployment target versions is 15.0 to 27.0.x"), so a 14.0
        // default makes every iOS build fail at CMake's compiler check. Override per project
        // with <IOSMinVersion>.
        private string iOSMinVersion = "15.0";
        private string iOSScreenOrientation = "both";
        private bool iOSRequiresFullScreen = false;
        private bool iOSStatusBarHidden = false;
        private string iOSDevelopmentTeam = string.Empty;
        private string iOSIcon = string.Empty;
        private string appVersion = "1.0"; // Application version (common across all platforms)
        private Dictionary<string, string> iOSNativeLibraries = new Dictionary<string, string>(); // iOS native library paths
        private Dictionary<string, string> iOSStaticFrameworks = new Dictionary<string, string>(); // dirs of .xcframework bundles linked into the app EXECUTABLE
        // Arbitrary Info.plist key/value pairs from IOSInfoPlistKey_* properties in Directory.Build.props
        private Dictionary<string, string> iOSInfoPlistKeys = new Dictionary<string, string>();
        // Raw Info.plist XML fragments from IOSInfoPlistRawFragment_* properties — for values the
        // string-only IOSInfoPlistKey_ hook can't express (arrays/dicts, e.g. SKAdNetworkItems).
        // The property value is inserted UNESCAPED at top-level <dict> scope; XDocument already
        // un-escapes &lt;-style entities, so escaped XML in the props arrives here as real XML.
        private Dictionary<string, string> iOSInfoPlistRawFragments = new Dictionary<string, string>();

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

            // --type release-harness = Release-OPTIMIZED codegen with the in-app harness (HARNESS define),
            // dev-signed exactly like a release build (automatic signing → sideloadable). Treat it as
            // "release" for all the config / signing / path derivation below; only the .NET publish's
            // DefineConstants swaps DEBUG (used by a debug build) for the dedicated HARNESS flag. Mirrors
            // the Android/desktop release-harness flavor.
            if (opts.Type?.Equals("release-harness", StringComparison.OrdinalIgnoreCase) == true)
            {
                _optimizedHarness = true;
                opts.Type = "release";
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

                // Static app-executable SDKs are declared opt-in — if declared they must exist
                // (they are typically fetched, not vendored, e.g. plugins/Ads/scripts/fetch-googlemobileads-ios.sh)
                foreach (var group in iOSStaticFrameworks)
                {
                    if (!Directory.Exists(group.Value) ||
                        Directory.GetDirectories(group.Value, "*.xcframework", SearchOption.TopDirectoryOnly).Length == 0)
                    {
                        Log.LogError($"❌ IOSStaticFrameworks_{group.Key}Path: no .xcframework bundles found at {group.Value}");
                        Log.LogError("   These SDKs are fetched, not vendored — run the owning plugin's fetch script first");
                        return false;
                    }
                }

                // Setup development team (with caching support)
                if (!SetupDevelopmentTeam(projectName))
                    return false;

                // Create iOS build directory structure
                string iosDir = Path.Combine(projectDir, "ios");
                Directory.CreateDirectory(iosDir);

                // Build sokol framework
                if (!BuildSokolFramework(iosDir, projectDir))
                    return false;

                // Generate RegisteredScripts.g.cs for NativeAOT (iOS always needs this)
                Log.LogMessage(MessageImportance.High, "📝 Scanning for GameBehaviour subclasses...");
                Utils.GenerateRegisteredScripts(Log, projectDir, projectName);

                // Compile shaders
                if (!CompileShaders(projectDir))
                    return false;

                // Publish .NET project for iOS
                if (!PublishDotNetProject(projectDir, projectName))
                    return false;

                // Create app framework
                if (!CreateAppFramework(iosDir, projectDir, projectName))
                    return false;

                // Every embedded framework is now in ios/frameworks — give each the Info.plist keys
                // App Store validation requires
                if (!CompleteFrameworkInfoPlists(Path.Combine(iosDir, "frameworks")))
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

                // Determine build type from opts
                string buildType = opts.Type == "debug" ? "debug" : "release";

                // Always copy to output folder (project's output folder or custom path)
                CopyToOutputPath(iosDir, projectName, buildType);

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

                // Build CMake arguments
                string cmakeArgs = $"-G Xcode -DCMAKE_SYSTEM_NAME=iOS -DCMAKE_OSX_DEPLOYMENT_TARGET={iOSMinVersion} -DCMAKE_OSX_ARCHITECTURES=\"arm64\" \"{extDir}\"";

                // Build sokol framework using CMake
                var cmakeResult = Cli.Wrap("cmake")
                    .WithArguments(cmakeArgs)
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

                // Determine configuration based on build type
                string configuration = string.IsNullOrEmpty(opts.Type) || opts.Type.Equals("release", StringComparison.OrdinalIgnoreCase) 
                    ? "Release" 
                    : "Debug";

                var buildResult = Cli.Wrap("cmake")
                    .WithArguments($"--build . --config {configuration}")
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

                string sourceFramework = Path.Combine(sokolDir, $"{configuration}-iphoneos", "sokol.framework");
                string destFramework = Path.Combine(frameworksDir, "sokol.framework");

                if (Directory.Exists(sourceFramework))
                {
                    CopyDirectory(sourceFramework, destFramework);
                }

                // Copy iOS native libraries to frameworks directory
                CopyIOSNativeLibraries(frameworksDir);

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
                
                // Determine configuration based on build type
                string configuration = string.IsNullOrEmpty(opts.Type) || opts.Type.Equals("release", StringComparison.OrdinalIgnoreCase) 
                    ? "Release" 
                    : "Debug";

                // The in-app harness compiles in via `#if DEBUG || HARNESS`. A debug build defines DEBUG;
                // a release-harness build defines the dedicated HARNESS flag instead — harness present in an
                // OPTIMIZED Release build, no debug overhead. A plain release build defines neither.
                string defineConstants = _optimizedHarness ? "__IOS__%3BHARNESS"
                                       : (configuration == "Debug" ? "__IOS__%3BDEBUG" : "__IOS__");

                string publishArgs = $"publish \"{projectFile}\" -r ios-arm64 -c {configuration} -p:BuildAsLibrary=true -p:DefineConstants=\"{defineConstants}\"";
                if (!string.IsNullOrEmpty(opts.LinkerFlags))
                {
                    publishArgs += $" -p:LinkerFlags=\"{opts.LinkerFlags}\"";
                }

                publishArgs += ObfuscationInjection.BuildArgs(opts, opts.ProjectPath, configuration, Log);

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

                string sanitizedProjectName = projectName.Replace("_", "-");
                string frameworksDir = Path.Combine(iosDir, "frameworks");
                string frameworkDir = Path.Combine(frameworksDir, $"{sanitizedProjectName}.framework");
                Directory.CreateDirectory(frameworkDir);

                // Determine configuration based on build type
                string configuration = string.IsNullOrEmpty(opts.Type) || opts.Type.Equals("release", StringComparison.OrdinalIgnoreCase) 
                    ? "Release" 
                    : "Debug";

                string libPath = Path.Combine(projectDir, "bin", configuration, "net10.0", "ios-arm64", "publish", $"lib{projectName}.dylib");

                if (!File.Exists(libPath))
                {
                    Log.LogError($"Library file not found: {libPath}");
                    return false;
                }

                // Copy and modify the library (use sanitized name)
                string destLib = Path.Combine(frameworkDir, sanitizedProjectName);
                File.Copy(libPath, destLib, true);

                // Use install_name_tool to modify the library
                var idResult = Cli.Wrap("install_name_tool")
                    .WithArguments($"-id @rpath/{sanitizedProjectName}.framework/{sanitizedProjectName} \"{destLib}\"")
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
                // The embedded framework's id must DIFFER from the app's, or installd rejects the whole
                // bundle ("parent bundle has the same identifier as sub-bundle"). Without <IOSBundleName>
                // they can't collide — the app carries the "-ios-app" suffix — so the default is left
                // exactly as it was. With the gate on, the app id is {prefix}.{name} and this template
                // would produce that same string, so the framework takes a ".framework" suffix.
                if (!string.IsNullOrEmpty(iOSBundleName))
                    content = content.Replace("TEMPLATE_BUNDLE_PREFIX.TEMPLATE_PROJECT_NAME",
                                              $"{iOSBundlePrefix}.{iOSBundleName}.framework");
                content = content.Replace("TEMPLATE_PROJECT_NAME", sanitizedProjectName);
                content = content.Replace("TEMPLATE_BUNDLE_PREFIX", iOSBundlePrefix);
                File.WriteAllText(infoPlistDest, content);

                return true;
            }
            catch (Exception ex)
            {
                Log.LogError($"Failed to create app framework: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// App Store upload validation rejects an app whose embedded frameworks lack
        /// CFBundleShortVersionString, CFBundleVersion or MinimumOSVersion (2026-10-06: 19 errors
        /// across the app, sokol, miniaudio, plugin and native-library frameworks — each comes from a
        /// different build, none set all three). A device install does not check
        /// them, so only an upload finds out. Adds the missing keys only — the app version and the
        /// project's minimum iOS version — and never overwrites a value a framework already sets.
        /// </summary>
        private bool CompleteFrameworkInfoPlists(string frameworksDir)
        {
            var required = new (string Key, string Value)[]
            {
                ("CFBundleShortVersionString", appVersion),
                ("CFBundleVersion", appVersion),
                ("MinimumOSVersion", iOSMinVersion),
            };

            foreach (string frameworkDir in Directory.GetDirectories(frameworksDir, "*.framework"))
            {
                string plist = Path.Combine(frameworkDir, "Info.plist");
                if (!File.Exists(plist))
                {
                    Log.LogError($"❌ {Path.GetFileName(frameworkDir)} has no Info.plist — App Store validation would reject it");
                    return false;
                }

                foreach (var (key, value) in required)
                {
                    // PlistBuddy reads binary and XML plists alike; Print fails when the key is absent
                    var print = Cli.Wrap("/usr/libexec/PlistBuddy")
                        .WithArguments(new[] { "-c", $"Print :{key}", plist })
                        .WithValidation(CommandResultValidation.None)
                        .ExecuteBufferedAsync().GetAwaiter().GetResult();
                    if (print.ExitCode == 0)
                        continue;

                    var add = Cli.Wrap("/usr/libexec/PlistBuddy")
                        .WithArguments(new[] { "-c", $"Add :{key} string {value}", plist })
                        .WithValidation(CommandResultValidation.None)
                        .ExecuteBufferedAsync().GetAwaiter().GetResult();
                    if (add.ExitCode != 0)
                    {
                        Log.LogError($"❌ Could not add {key} to {plist}: {add.StandardError}{add.StandardOutput}");
                        return false;
                    }
                    Log.LogMessage(MessageImportance.High, $"   ✅ {Path.GetFileName(frameworkDir)}: added {key} = {value}");
                }
            }
            return true;
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
                string sanitizedProjectName = projectName.Replace("_", "-");

                // Bundle identifier. <IOSBundleName> is an opt-in override: with it the id is
                // "{IOSBundlePrefix}.{IOSBundleName}", decoupled from the "-ios-app" executable
                // name. Without it both files keep the exact value they had before — note those two
                // defaults are NOT the same string (the plist's is the historical literal), so they
                // are spelled out separately rather than shared.
                bool hasBundleNameOverride = !string.IsNullOrEmpty(iOSBundleName);
                string cmakeBundleId = hasBundleNameOverride
                    ? $"{iOSBundlePrefix}.{iOSBundleName}"
                    : $"{iOSBundlePrefix}.{sanitizedProjectName}-ios-app";
                string plistBundleId = hasBundleNameOverride
                    ? $"{iOSBundlePrefix}.{iOSBundleName}"
                    : "com.elix22.${MACOSX_BUNDLE_EXECUTABLE_NAME}";

                // Home-screen label. Neither tag set ⇒ the historical "{project} iOS App".
                string bundleDisplayName = string.IsNullOrEmpty(EffectiveDisplayName)
                    ? $"{sanitizedProjectName} iOS App"
                    : EffectiveDisplayName;

                content = content.Replace("TEMPLATE_BUNDLE_DISPLAY_NAME", bundleDisplayName);
                content = content.Replace("TEMPLATE_BUNDLE_ID", cmakeBundleId);
                content = content.Replace("TEMPLATE_PROJECT_NAME", sanitizedProjectName);
                content = content.Replace("TEMPLATE_BUNDLE_PREFIX", iOSBundlePrefix);
                content = content.Replace("TEMPLATE_APP_VERSION", appVersion);
                content = content.Replace("TEMPLATE_IOS_MIN_VERSION", iOSMinVersion);

                // Set orientation: Directory.Build.props takes precedence when it has a specific
                // (non-"both") value; command-line --orientation is a fallback used only when
                // the props file leaves orientation at the default ("both").
                string orientation;
                if (!string.IsNullOrEmpty(iOSScreenOrientation) && iOSScreenOrientation != "both")
                {
                    // Props file explicitly specifies a concrete orientation — always respect it.
                    orientation = iOSScreenOrientation;
                }
                else if (!string.IsNullOrEmpty(opts.Orientation) && opts.Orientation != "both")
                {
                    // Props says "both" (or unset); honour explicit command-line override.
                    orientation = opts.ValidatedOrientation;
                }
                else
                {
                    orientation = iOSScreenOrientation; // "both" / default
                }
                
                string iosOrientations, ipadOrientations;
                string iosOrientationsPlist, ipadOrientationsPlist;
                switch (orientation)
                {
                    case "portrait":
                        iosOrientations = "UIInterfaceOrientationPortrait";
                        ipadOrientations = "UIInterfaceOrientationPortrait";
                        iosOrientationsPlist = "\n        <string>UIInterfaceOrientationPortrait</string>";
                        ipadOrientationsPlist = "\n        <string>UIInterfaceOrientationPortrait</string>";
                        break;
                    case "portrait_upside_down":
                        iosOrientations = "UIInterfaceOrientationPortraitUpsideDown";
                        ipadOrientations = "UIInterfaceOrientationPortraitUpsideDown";
                        iosOrientationsPlist = "\n        <string>UIInterfaceOrientationPortraitUpsideDown</string>";
                        ipadOrientationsPlist = "\n        <string>UIInterfaceOrientationPortraitUpsideDown</string>";
                        break;
                    case "landscape_left":
                        iosOrientations = "UIInterfaceOrientationLandscapeLeft";
                        ipadOrientations = "UIInterfaceOrientationLandscapeLeft";
                        iosOrientationsPlist = "\n        <string>UIInterfaceOrientationLandscapeLeft</string>";
                        ipadOrientationsPlist = "\n        <string>UIInterfaceOrientationLandscapeLeft</string>";
                        break;
                    case "landscape_right":
                        iosOrientations = "UIInterfaceOrientationLandscapeRight";
                        ipadOrientations = "UIInterfaceOrientationLandscapeRight";
                        iosOrientationsPlist = "\n        <string>UIInterfaceOrientationLandscapeRight</string>";
                        ipadOrientationsPlist = "\n        <string>UIInterfaceOrientationLandscapeRight</string>";
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
                
                // Configure native libraries in CMakeLists.txt
                ConfigureIOSNativeLibrariesInCMake(ref content, projectName);
                
                File.WriteAllText(cmakeDest, content);

                // Copy and process Info.plist.in
                string plistSource = Path.Combine(templatesDir, "Info.plist.in");
                string plistDest = Path.Combine(iosDir, "Info.plist.in");
                if (File.Exists(plistSource))
                {
                    string plistContent = File.ReadAllText(plistSource);
                    plistContent = plistContent.Replace("@TEMPLATE_IOS_BUNDLE_ID@", plistBundleId);
                    // CFBundleDisplayName is what iOS actually shows under the icon; it only appears
                    // when <IOSDisplayName> is set, so an unset project keeps the exact plist it had.
                    plistContent = plistContent.Replace("@TEMPLATE_IOS_DISPLAY_NAME@",
                        string.IsNullOrEmpty(EffectiveDisplayName)
                            ? ""
                            : $"\n    <key>CFBundleDisplayName</key>\n    <string>{System.Security.SecurityElement.Escape(EffectiveDisplayName)}</string>");
                    plistContent = plistContent.Replace("TEMPLATE_PROJECT_NAME", sanitizedProjectName);
                    plistContent = plistContent.Replace("TEMPLATE_IOS_MIN_VERSION", iOSMinVersion);
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

                    // Inject arbitrary Info.plist key/value pairs from IOSInfoPlistKey_* properties.
                    // Each entry becomes:  <key>KeyName</key>\n    <string>value</string>
                    var extraPlistSb = new System.Text.StringBuilder();
                    foreach (var kv in iOSInfoPlistKeys)
                    {
                        extraPlistSb.Append($"\n    <key>{System.Security.SecurityElement.Escape(kv.Key)}</key>");
                        extraPlistSb.Append($"\n    <string>{System.Security.SecurityElement.Escape(kv.Value)}</string>");
                    }
                    // Raw fragments (IOSInfoPlistRawFragment_*) go in unescaped — the author owns
                    // their XML validity (needed for array/dict values like SKAdNetworkItems).
                    foreach (var kv in iOSInfoPlistRawFragments)
                    {
                        extraPlistSb.Append("\n    " + kv.Value.Trim());
                    }
                    plistContent = plistContent.Replace("@TEMPLATE_IOS_PLIST_EXTRA_KEYS@", extraPlistSb.ToString());

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

                string sanitizedProjectName = projectName.Replace("_", "-");
                string buildDir = Path.Combine(iosDir, "build-xcode-ios-app");
                
                // Determine configuration based on build type
                string configuration = string.IsNullOrEmpty(opts.Type) || opts.Type.Equals("release", StringComparison.OrdinalIgnoreCase) 
                    ? "Release" 
                    : "Debug";

                var result = Cli.Wrap("xcodebuild")
                    .WithArguments($"-target {sanitizedProjectName}-ios-app -configuration {configuration} -sdk iphoneos -arch arm64")
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

                string appBundlePath = Path.Combine(buildDir, $"{configuration}-iphoneos", $"{sanitizedProjectName}-ios-app.app");
                
                // Check if the app bundle exists at the expected location, otherwise look in bin/{configuration}
                if (!Directory.Exists(appBundlePath))
                {
                    string altPath = Path.Combine(buildDir, "bin", configuration, $"{sanitizedProjectName}-ios-app.app");
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

                string sanitizedProjectName = projectName.Replace("_", "-");
                string buildDir = Path.Combine(iosDir, "build-xcode-ios-app");
                
                // Determine configuration based on build type
                string configuration = string.IsNullOrEmpty(opts.Type) || opts.Type.Equals("release", StringComparison.OrdinalIgnoreCase) 
                    ? "Release" 
                    : "Debug";
                
                string appBundlePath = Path.Combine(buildDir, $"{configuration}-iphoneos", $"{sanitizedProjectName}-ios-app.app");

                // Check multiple possible locations for the app bundle
                string[] possiblePaths = new[]
                {
                    appBundlePath,
                    Path.Combine(buildDir, $"{configuration}-iphoneos", $"{sanitizedProjectName}-ios-app", $"{sanitizedProjectName}-ios-app.app"),
                    Path.Combine(buildDir, configuration, $"{sanitizedProjectName}-ios-app.app"),
                    Path.Combine(buildDir, $"{sanitizedProjectName}-ios-app.app"),
                    Path.Combine(buildDir, "bin", configuration, $"{sanitizedProjectName}-ios-app.app")
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

                // Determine the target device(s). With --ios-device, use it directly (supports WiFi
                // devices that won't appear in the USB scan). Otherwise scan USB via ios-deploy -c and,
                // when several devices are connected, prompt to choose one (or all) — matching the
                // Android flow. (A prior change auto-picked usbDevices[0], so a multi-device deploy
                // silently installed on only the first device.)
                var selectedDevices = new List<(string Id, string Name, string Via)>();

                if (!string.IsNullOrEmpty(opts.IOSDeviceId))
                {
                    selectedDevices.Add((opts.IOSDeviceId, opts.IOSDeviceId, "specified"));
                    Log.LogMessage(MessageImportance.High, $"Using specified iOS device: {opts.IOSDeviceId}");
                }
                else
                {
                    // Discover every reachable device across BOTH transports and let the user pick. USB via
                    // ios-deploy -c (unchanged); WiFi via devicectl. A device on the cable ALWAYS deploys over USB:
                    // USB entries come first, and a device reachable both ways is kept only as its USB entry (deduped
                    // by UDID) — so cabled deployment is never regressed, WiFi only ADDS network-only devices.
                    var usbDevices = new List<(string Id, string Name, string Via)>();
                    var checkResult = Cli.Wrap("which")
                        .WithArguments("ios-deploy")
                        .WithValidation(CommandResultValidation.None)
                        .ExecuteBufferedAsync().GetAwaiter().GetResult();

                    if (checkResult.ExitCode == 0)
                    {
                        var deviceResult = Cli.Wrap("ios-deploy")
                            .WithArguments("-c --timeout 2")
                            .WithValidation(CommandResultValidation.None)
                            .ExecuteBufferedAsync().GetAwaiter().GetResult();

                        foreach (var rawLine in deviceResult.StandardOutput.Split('\n'))
                        {
                            if (!rawLine.Contains("Found")) continue;
                            var afterFound = rawLine.Substring(rawLine.IndexOf("Found") + 6);
                            var idEnd      = afterFound.IndexOf(" ");
                            if (idEnd <= 0) continue;
                            var id   = afterFound.Substring(0, idEnd).Trim();
                            var name = "Unknown";
                            var aka  = afterFound.IndexOf("a.k.a.");
                            if (aka >= 0)
                            {
                                var rest = afterFound.Substring(aka + 6);
                                int q1 = rest.IndexOf('\''), q2 = rest.LastIndexOf('\'');
                                name = q2 > q1 && q1 >= 0 ? rest.Substring(q1 + 1, q2 - q1 - 1) : rest.Trim();
                            }
                            usbDevices.Add((id, name, "USB"));
                        }
                    }
                    else
                    {
                        // ios-deploy is only needed for the USB scan; without it we can still deploy over WiFi.
                        Log.LogMessage(MessageImportance.High, "ios-deploy not found — skipping the USB scan and looking for network (WiFi) devices instead. For USB installs: brew install ios-deploy");
                    }

                    // WiFi devices on the local network, excluding any already seen on USB (USB preferred).
                    var usbIds = new HashSet<string>(usbDevices.Select(d => d.Id), StringComparer.OrdinalIgnoreCase);
                    var wifiDevices = DiscoverWifiDevices(usbIds);

                    var allDevices = new List<(string Id, string Name, string Via)>();
                    allDevices.AddRange(usbDevices);    // USB first → the non-interactive default stays a cabled device
                    allDevices.AddRange(wifiDevices);

                    if (allDevices.Count == 0)
                    {
                        Log.LogError("No iOS devices found via USB or WiFi. Connect a cable, enable 'Connect via network' for the device (Xcode → Window → Devices and Simulators), or pass --ios-device <UDID>.");
                        return false;
                    }
                    selectedDevices = ChooseDevices(allDevices);
                }

                // Install (and optionally launch) on each selected device.
                bool allOk = true;
                foreach (var device in selectedDevices)
                {
                    if (selectedDevices.Count > 1)
                        Log.LogMessage(MessageImportance.High, $"\n📱 Installing on device: {device.Name} [{device.Via}] ({device.Id})");
                    if (!InstallAndLaunchOnDevice(device.Id, device.Name, appBundlePath, runAfterInstall, device.Via))
                        allOk = false;
                }
                return allOk;
            }
            catch (Exception ex)
            {
                Log.LogError($"Failed to install on device: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Pick which discovered device(s) to install on across BOTH transports: one is auto-selected, otherwise
        /// the user chooses (interactive prompt) or the first is used with a warning (non-interactive) — matching
        /// the Android flow. Each device carries its transport (USB / WiFi), shown in the list. USB entries come
        /// first, so the non-interactive default is a cabled device when one is connected. Returns ≥ 1 device.
        /// </summary>
        private List<(string Id, string Name, string Via)> ChooseDevices(List<(string Id, string Name, string Via)> devices)
        {
            var selected = new List<(string Id, string Name, string Via)>();
            if (devices.Count == 1)
            {
                selected.Add(devices[0]);
                Log.LogMessage(MessageImportance.High, $"✅ Using {devices[0].Via} device: {devices[0].Name} ({devices[0].Id})");
                return selected;
            }

            Log.LogMessage(MessageImportance.High, $"📱 Multiple iOS devices detected ({devices.Count} devices):");
            Log.LogMessage(MessageImportance.High, "======================================================");
            for (int i = 0; i < devices.Count; i++)
                Log.LogMessage(MessageImportance.High, $"{i + 1}) {devices[i].Name}  [{devices[i].Via}]  ({devices[i].Id})");
            Log.LogMessage(MessageImportance.High, $"{devices.Count + 1}) All devices");

            if (opts.Interactive)
            {
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
                            selected = new List<(string Id, string Name, string Via)>(devices);
                            Log.LogMessage(MessageImportance.High, $"✅ Selected all devices ({devices.Count} devices)");
                        }
                        else
                        {
                            selected.Add(devices[selection - 1]);
                            Log.LogMessage(MessageImportance.High, $"✅ Selected device: {devices[selection - 1].Name} [{devices[selection - 1].Via}] ({devices[selection - 1].Id})");
                        }
                        break;
                    }

                    Console.WriteLine($"❌ Invalid selection. Please enter a number between 1 and {devices.Count + 1}.");
                    selection = -1;
                }
            }
            else
            {
                selected.Add(devices[0]);
                Log.LogMessage(MessageImportance.High, $"⚠️  Using first device: {devices[0].Name} [{devices[0].Via}] ({devices[0].Id})");
                Log.LogWarning("Multiple devices found. Using the first one. Pass --ios-device <UDID> to choose, or --interactive to be prompted.");
            }
            return selected;
        }

        /// <summary>
        /// Discover iOS devices reachable over the local network (WiFi) via <c>xcrun devicectl list devices</c>.
        /// Returns (UDID, Name, "WiFi") for each PAIRED device currently reachable on the network (its
        /// connectionProperties.transportType is "localNetwork"; a cabled device reports "wired" and an unreachable
        /// one has no transportType), skipping any UDID in <paramref name="excludeUdids"/> (devices already seen on
        /// USB, which are preferred). Best-effort: returns empty if devicectl is missing/fails, so it can never break
        /// USB deploy. The device must have "Connect via network" enabled (Xcode → Window → Devices and Simulators).
        /// Static so the list-devices task can reuse it.
        /// </summary>
        public static List<(string Id, string Name, string Via)> DiscoverWifiDevices(HashSet<string>? excludeUdids = null)
        {
            var devices = new List<(string Id, string Name, string Via)>();
            string tmp = Path.Combine(Path.GetTempPath(), $"sokol-devicectl-{Guid.NewGuid():N}.json");
            try
            {
                var result = Cli.Wrap("xcrun")
                    .WithArguments($"devicectl list devices --timeout {DevicectlLaunchTimeoutSeconds} --json-output \"{tmp}\"")
                    .WithValidation(CommandResultValidation.None)
                    .ExecuteBufferedAsync().GetAwaiter().GetResult();

                if (result.ExitCode != 0 || !File.Exists(tmp))
                    return devices;

                using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(tmp));
                if (!doc.RootElement.TryGetProperty("result", out var res) ||
                    !res.TryGetProperty("devices", out var arr) || arr.ValueKind != System.Text.Json.JsonValueKind.Array)
                    return devices;

                foreach (var dev in arr.EnumerateArray())
                {
                    if (!dev.TryGetProperty("connectionProperties", out var cp)) continue;
                    string transport = cp.TryGetProperty("transportType", out var tt) ? (tt.GetString() ?? "") : "";
                    string pairing   = cp.TryGetProperty("pairingState",  out var ps) ? (ps.GetString() ?? "") : "";
                    // network-reachable + paired only (a "wired" device is on USB; an unreachable one has no transport)
                    if (!transport.Equals("localNetwork", StringComparison.OrdinalIgnoreCase)) continue;
                    if (!pairing.Equals("paired", StringComparison.OrdinalIgnoreCase)) continue;

                    string udid = dev.TryGetProperty("hardwareProperties", out var hw) && hw.TryGetProperty("udid", out var u)
                        ? (u.GetString() ?? "") : "";
                    if (string.IsNullOrEmpty(udid)) continue;
                    if (excludeUdids != null && excludeUdids.Contains(udid)) continue;

                    string name = dev.TryGetProperty("deviceProperties", out var dp) && dp.TryGetProperty("name", out var n)
                        ? (n.GetString() ?? "iOS device") : "iOS device";
                    devices.Add((udid, name, "WiFi"));
                }
            }
            catch { /* best-effort: WiFi discovery is optional and must never break USB deploy */ }
            finally
            {
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { /* best effort cleanup */ }
            }
            return devices;
        }

        /// <summary>
        /// Launch fallback for a CABLED device that CoreDevice cannot reach at all — an iPhone on iOS 15
        /// or older shows as "unavailable" in `devicectl list devices` and `devicectl device process launch`
        /// fails with CoreDeviceError 1000 (device not found), while the install (ios-deploy) succeeded.
        /// `idevicedebug --detach run <bundle-id>` (libimobiledevice) launches it through debugserver and
        /// detaches, leaving it running. ⛔ NOT `ios-deploy -m -L -b <app>`: that route reported "success"
        /// and the app died at dyld start with SIGTRAP inside lldb_image_notifier — the debugger's detach
        /// left a trap behind — on an iPhone 7 / iOS 15.8.8 (3 crash reports, 2026-09-06); idevicedebug
        /// launched the same build cleanly on the same device.
        /// </summary>
        private bool TryLaunchViaIdevicedebug(string targetDeviceId, string targetDeviceName, string bundleId)
        {
            try
            {
                var r = Cli.Wrap("idevicedebug")
                    .WithArguments($"-u {targetDeviceId} --detach run {bundleId}")
                    .WithValidation(CommandResultValidation.None)
                    .ExecuteBufferedAsync().GetAwaiter().GetResult();
                if (r.ExitCode == 0) return true;
                Log.LogMessage(MessageImportance.Normal, $"idevicedebug launch on {targetDeviceName} failed (exit {r.ExitCode}): {r.StandardError.Trim()}");
            }
            catch (Exception ex)
            {
                Log.LogMessage(MessageImportance.Normal, $"idevicedebug launch on {targetDeviceName} unavailable (brew install libimobiledevice): {ex.Message}");
            }
            return false;
        }

        /// <summary>
        /// Install the app bundle on ONE device (xcrun devicectl first, ios-deploy fallback) and,
        /// when requested, launch it. Returns false only if every install method failed.
        /// <paramref name="via"/> is the transport the device was discovered on ("USB" / "WiFi").
        /// </summary>
        private bool InstallAndLaunchOnDevice(string targetDeviceId, string targetDeviceName, string appBundlePath,
                                              bool runAfterInstall, string via = "USB")
        {
            bool overWifi = string.Equals(via, "WiFi", StringComparison.OrdinalIgnoreCase);

            // ── Install ───────────────────────────────────────────────────────
            // Pick the tool by TRANSPORT — they are not interchangeable.
            //
            // On the CABLE, ios-deploy first. It talks to the device directly and installs this app in
            // ~10s. devicectl goes through CoreDevice, which routinely parks a cabled phone in the
            // "connecting" state (see `xcrun devicectl list devices`); in that state
            // `devicectl device install` blocks for its entire timeout and then fails, so leading with it
            // cost a minute or more per deploy and looked like a hang. It stays as the fallback for a
            // machine without ios-deploy.
            //
            // Over WiFi it is the reverse: ios-deploy is cable-only — it blocks on "Waiting for iOS device
            // to be connected" until something appears on the cable — so devicectl is the only option. Its
            // control channel drops transiently when the device locks or the tunnel re-establishes
            // ("Connection reset by peer" / CoreDeviceError 4000), hence the retries.
            bool installed;
            if (overWifi)
            {
                installed = TryInstallViaDevicectl(targetDeviceId, targetDeviceName, appBundlePath);
                for (int attempt = 2; !installed && attempt <= DevicectlWifiAttempts; attempt++)
                {
                    Log.LogMessage(MessageImportance.High,
                        $"[Install] WiFi control channel dropped — retry {attempt}/{DevicectlWifiAttempts} in 3s (keep the device UNLOCKED)...");
                    System.Threading.Thread.Sleep(3000);
                    installed = TryInstallViaDevicectl(targetDeviceId, targetDeviceName, appBundlePath);
                }
            }
            else
            {
                installed = TryInstallViaIosDeploy(targetDeviceId, targetDeviceName, appBundlePath);
                if (!installed)
                    installed = TryInstallViaDevicectl(targetDeviceId, targetDeviceName, appBundlePath);
            }

            if (!installed)
            {
                if (overWifi)
                    Log.LogError($"Could not install on {targetDeviceName} ({targetDeviceId}) over WiFi after {DevicectlWifiAttempts} attempts. " +
                                 "Unlock the device and keep it awake, confirm it is on the SAME network as this Mac " +
                                 "(Xcode → Window → Devices and Simulators → 'Connect via network'), then retry — or plug in a cable, " +
                                 "which deploys over USB.");
                else
                    Log.LogError($"All installation methods failed for device {targetDeviceName} ({targetDeviceId}).");
                return false;
            }

            Log.LogMessage(MessageImportance.High, $"App installed successfully on device: {targetDeviceName}!");

            // ── Launch ────────────────────────────────────────────────────────
            if (runAfterInstall)
            {
                Log.LogMessage(MessageImportance.High, $"Launching app on device: {targetDeviceName} ({targetDeviceId})");

                string infoPlistPath = Path.Combine(appBundlePath, "Info.plist");
                string bundleId      = "";

                try
                {
                    var plistResult = Cli.Wrap("plutil")
                        .WithArguments($"-extract CFBundleIdentifier raw \"{infoPlistPath}\"")
                        .WithValidation(CommandResultValidation.None)
                        .ExecuteBufferedAsync().GetAwaiter().GetResult();

                    if (plistResult.ExitCode == 0)
                        bundleId = plistResult.StandardOutput.Trim();
                    else
                        Log.LogWarning($"Could not extract bundle ID from Info.plist: {plistResult.StandardError}");
                }
                catch (Exception ex)
                {
                    Log.LogWarning($"plutil failed: {ex.Message}");
                }

                // Launching stays on devicectl: ios-deploy needs a DeviceSupport bundle for the device's iOS
                // version and simply refuses ("Unable to locate DeviceSupport directory") on a current
                // release, so it is not an alternative here — only for the install above, which needs none.
                if (!string.IsNullOrEmpty(bundleId))
                {
                    var launchResult = Cli.Wrap("xcrun")
                        .WithArguments($"devicectl device process launch --timeout {DevicectlLaunchTimeoutSeconds} --device {targetDeviceId} {bundleId}")
                        .WithStandardOutputPipe(PipeTarget.ToDelegate(s => Log.LogMessage(MessageImportance.Normal, s)))
                        .WithStandardErrorPipe(PipeTarget.ToDelegate(s => Log.LogMessage(MessageImportance.Normal, s)))
                        .WithValidation(CommandResultValidation.None)
                        .ExecuteBufferedAsync().GetAwaiter().GetResult();

                    if (launchResult.ExitCode != 0 && !overWifi && TryLaunchViaIdevicedebug(targetDeviceId, targetDeviceName, bundleId))
                        Log.LogMessage(MessageImportance.High, $"App launched successfully on device: {targetDeviceName} (via idevicedebug)!");
                    else if (launchResult.ExitCode != 0)
                        Log.LogWarning($"App launch failed (exit {launchResult.ExitCode}) — the app IS installed, so just tap it on " +
                                       $"the device. devicectl cannot reach {targetDeviceName} (check `xcrun devicectl list devices`: " +
                                       $"it should read 'available (paired)', not 'connecting'). {launchResult.StandardError}");
                    else
                        Log.LogMessage(MessageImportance.High, $"App launched successfully on device: {targetDeviceName}!");
                }
            }

            return true;
        }

        /// <summary>How many times to try devicectl on a WiFi device before giving up (the control channel
        /// drops transiently when the device locks or the tunnel re-establishes).</summary>
        private const int DevicectlWifiAttempts = 3;

        /// <summary>Seconds ios-deploy may wait for a device on the CABLE before failing. Without a bound it
        /// waits forever and the build appears stuck.</summary>
        private const int IosDeployWaitSeconds = 30;

        /// <summary>Overall bound for a devicectl call. UNBOUNDED IS NOT SAFE: when CoreDevice reports the
        /// device as "connecting" rather than "available (paired)" — a locked device, a half-established
        /// tunnel, a pairing that needs re-trusting — <c>devicectl device install</c> never returns, so the
        /// build hangs with no error and the ios-deploy USB fallback below is never reached. Generous
        /// enough for a first install of a large NativeAOT bundle over WiFi.</summary>
        private const int DevicectlTimeoutSeconds = 180;

        /// <summary>Bound for the launch call, which only has to start an already-installed app.</summary>
        private const int DevicectlLaunchTimeoutSeconds = 60;

        /// <summary>
        /// Installs the app bundle using <c>xcrun devicectl</c> (Xcode 15+, iOS 17+).
        /// Works over WiFi and USB; no extra tools required beyond a standard Xcode install.
        /// The device must have "Connect via Network" enabled (Xcode → Window → Devices → checkbox).
        /// </summary>
        private bool TryInstallViaDevicectl(string deviceId, string deviceName, string appBundlePath)
        {
            try
            {
                Log.LogMessage(MessageImportance.High, $"[Install] Trying xcrun devicectl for {deviceName}...");

                var result = Cli.Wrap("xcrun")
                    .WithArguments($"devicectl device install app --timeout {DevicectlTimeoutSeconds} --device {deviceId} \"{appBundlePath}\"")
                    .WithStandardOutputPipe(PipeTarget.ToDelegate(s => Log.LogMessage(MessageImportance.Normal, s)))
                    .WithStandardErrorPipe(PipeTarget.ToDelegate(s => Log.LogMessage(MessageImportance.Normal, s)))
                    .WithValidation(CommandResultValidation.None)
                    .ExecuteBufferedAsync().GetAwaiter().GetResult();

                if (result.ExitCode == 0)
                {
                    Log.LogMessage(MessageImportance.High, $"[Install] devicectl succeeded.");
                    return true;
                }

                Log.LogMessage(MessageImportance.Normal, $"[Install] devicectl failed (exit {result.ExitCode}): {result.StandardError}");
                return false;
            }
            catch (Exception ex)
            {
                Log.LogMessage(MessageImportance.Normal, $"[Install] devicectl not available: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Installs the app bundle using <c>ios-deploy</c> (brew install ios-deploy).
        /// Works over WiFi when the device is network-paired; also works over USB.
        /// Does NOT pass --no-wifi so both transports are allowed.
        /// </summary>
        private bool TryInstallViaIosDeploy(string deviceId, string deviceName, string appBundlePath)
        {
            try
            {
                string iosDeploy = FindTool("ios-deploy");
                if (string.IsNullOrEmpty(iosDeploy))
                {
                    Log.LogMessage(MessageImportance.Normal, "[Install] ios-deploy not found, skipping.");
                    return false;
                }

                Log.LogMessage(MessageImportance.High, $"[Install] Trying ios-deploy for {deviceName}...");

                // --timeout is REQUIRED: without it ios-deploy waits forever on "Waiting for iOS device to
                // be connected" when the device isn't on the cable, and the build just hangs.
                var result = Cli.Wrap(iosDeploy)
                    .WithArguments($"--id {deviceId} --timeout {IosDeployWaitSeconds} --bundle \"{appBundlePath}\"")
                    .WithStandardOutputPipe(PipeTarget.ToDelegate(s => Log.LogMessage(MessageImportance.Normal, s)))
                    .WithStandardErrorPipe(PipeTarget.ToDelegate(s => Log.LogMessage(MessageImportance.Normal, s)))
                    .WithValidation(CommandResultValidation.None)
                    .ExecuteBufferedAsync().GetAwaiter().GetResult();

                if (result.ExitCode == 0)
                {
                    Log.LogMessage(MessageImportance.High, $"[Install] ios-deploy succeeded.");
                    return true;
                }

                Log.LogMessage(MessageImportance.Normal, $"[Install] ios-deploy failed (exit {result.ExitCode}): {result.StandardError}");
                return false;
            }
            catch (Exception ex)
            {
                Log.LogMessage(MessageImportance.Normal, $"[Install] ios-deploy error: {ex.Message}");
                return false;
            }
        }

        private static string FindTool(string name)
        {
            var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
            char sep    = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ';' : ':';
            var dirs    = new List<string>(pathEnv.Split(sep, StringSplitOptions.RemoveEmptyEntries));
            foreach (var extra in new[] { "/usr/bin", "/usr/local/bin", "/opt/homebrew/bin", "/opt/local/bin" })
                if (!dirs.Contains(extra)) dirs.Add(extra);
            foreach (var dir in dirs)
            {
                var candidate = Path.Combine(dir, name);
                if (File.Exists(candidate)) return candidate;
            }
            return string.Empty;
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
                    // iOS Bundle Prefix
                    var bundlePrefixElement = propertyGroup.Element("IOSBundlePrefix");
                    if (bundlePrefixElement != null && !string.IsNullOrEmpty(bundlePrefixElement.Value))
                    {
                        iOSBundlePrefix = bundlePrefixElement.Value;
                        propertyCount++;
                    }

                    // iOS Bundle Name (optional override of the bundle id's name part)
                    var bundleNameElement = propertyGroup.Element("IOSBundleName");
                    if (bundleNameElement != null && !string.IsNullOrEmpty(bundleNameElement.Value))
                    {
                        iOSBundleName = bundleNameElement.Value;
                        propertyCount++;
                    }

                    // iOS Display Name (optional Home-screen label)
                    var displayNameElement = propertyGroup.Element("IOSDisplayName");
                    if (displayNameElement != null && !string.IsNullOrEmpty(displayNameElement.Value))
                    {
                        iOSDisplayName = displayNameElement.Value;
                        propertyCount++;
                    }

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

                    // App Version (common across all platforms)
                    var versionElement = propertyGroup.Element("AppVersion");
                    if (versionElement != null && !string.IsNullOrEmpty(versionElement.Value))
                    {
                        appVersion = versionElement.Value;
                        propertyCount++;
                    }

                    // Detect iOS native libraries (IOSNativeLibrary_*Path properties)
                    // and arbitrary Info.plist key/value pairs (IOSInfoPlistKey_* properties)
                    foreach (var element in propertyGroup.Elements())
                    {
                        string elementName = element.Name.LocalName;
                        if (elementName.StartsWith("IOSNativeLibrary_") && elementName.EndsWith("Path"))
                        {
                            // Extract library name from IOSNativeLibrary_[LibraryName]Path
                            string libraryName = elementName.Substring("IOSNativeLibrary_".Length);
                            libraryName = libraryName.Substring(0, libraryName.Length - "Path".Length);
                            
                            if (!string.IsNullOrEmpty(element.Value))
                            {
                                string libraryBasePath = element.Value;

                                // Expand MSBuild variables that the XML reader leaves unexpanded
                                libraryBasePath = libraryBasePath.Replace("$(SokolNetHome)", Utils.GetSokolNetHome(), StringComparison.OrdinalIgnoreCase);
                                libraryBasePath = libraryBasePath.Replace("$(HomeDir)", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), StringComparison.OrdinalIgnoreCase);

                                string absolutePath = Path.IsPathRooted(libraryBasePath)
                                    ? libraryBasePath
                                    : Path.Combine(projectPath, libraryBasePath);
                                    
                                iOSNativeLibraries[libraryName] = absolutePath;
                                propertyCount++;
                            }
                        }
                        else if (elementName.StartsWith("IOSStaticFrameworks_") && elementName.EndsWith("Path"))
                        {
                            // IOSStaticFrameworks_[Name]Path → a directory of .xcframework bundles whose
                            // DEVICE slice is linked INTO THE APP EXECUTABLE (static vendor SDKs). A
                            // static SWIFT framework (e.g. GoogleMobileAds 13+) absorbed into a plugin
                            // dylib aborts at launch in the Swift runtime — it must live in the main
                            // image. These are linked, never embedded (static archives have no runtime
                            // presence of their own).
                            string groupName = elementName.Substring("IOSStaticFrameworks_".Length);
                            groupName = groupName.Substring(0, groupName.Length - "Path".Length);

                            if (!string.IsNullOrEmpty(element.Value))
                            {
                                string basePath = element.Value;
                                basePath = basePath.Replace("$(SokolNetHome)", Utils.GetSokolNetHome(), StringComparison.OrdinalIgnoreCase);
                                basePath = basePath.Replace("$(HomeDir)", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), StringComparison.OrdinalIgnoreCase);

                                string absoluteFwPath = Path.IsPathRooted(basePath)
                                    ? basePath
                                    : Path.Combine(projectPath, basePath);

                                iOSStaticFrameworks[groupName] = absoluteFwPath;
                                propertyCount++;
                            }
                        }
                        else if (elementName.StartsWith("IOSInfoPlistKey_"))
                        {
                            // Extract the plist key name from IOSInfoPlistKey_[PlistKeyName]
                            string plistKey = elementName.Substring("IOSInfoPlistKey_".Length);
                            if (!string.IsNullOrEmpty(plistKey) && !string.IsNullOrEmpty(element.Value))
                            {
                                iOSInfoPlistKeys[plistKey] = element.Value;
                                propertyCount++;
                            }
                        }
                        else if (elementName.StartsWith("IOSInfoPlistRawFragment_"))
                        {
                            // Raw plist XML fragment (arrays/dicts) — inserted unescaped.
                            string fragName = elementName.Substring("IOSInfoPlistRawFragment_".Length);
                            if (!string.IsNullOrEmpty(fragName) && !string.IsNullOrEmpty(element.Value))
                            {
                                iOSInfoPlistRawFragments[fragName] = element.Value;
                                propertyCount++;
                            }
                        }
                    }
                }

                if (propertyCount > 0)
                {
                    Log.LogMessage(MessageImportance.High, $"📋 Read {propertyCount} iOS properties from Directory.Build.props");
                    if (!string.IsNullOrEmpty(appVersion))
                        Log.LogMessage(MessageImportance.High, $"   - AppVersion: {appVersion}");
                    if (!string.IsNullOrEmpty(iOSBundlePrefix))
                        Log.LogMessage(MessageImportance.High, $"   - IOSBundlePrefix: {iOSBundlePrefix}");
                        if (!string.IsNullOrEmpty(iOSBundleName))
                            Log.LogMessage(MessageImportance.High, $"   - IOSBundleName: {iOSBundleName} (bundle id: {iOSBundlePrefix}.{iOSBundleName})");
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
                    
                    // Log iOS native libraries
                    foreach (var library in iOSNativeLibraries)
                    {
                        Log.LogMessage(MessageImportance.High, $"   - IOSNativeLibrary_{library.Key}Path: {library.Value}");
                    }
                    // Log extra plist keys
                    foreach (var kv in iOSInfoPlistKeys)
                    {
                        Log.LogMessage(MessageImportance.High, $"   - IOSInfoPlistKey_{kv.Key}: {kv.Value}");
                    }
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
            string cacheDir = Path.Combine(homeDir, ".Sokol.NET-cache");
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

        private void CopyIOSNativeLibraries(string frameworksDir)
        {
            if (iOSNativeLibraries.Count == 0)
            {
                return;
            }

            Log.LogMessage(MessageImportance.High, "📦 Copying iOS native libraries to frameworks directory...");

            foreach (var library in iOSNativeLibraries)
            {
                string libraryName = library.Key;
                string libraryPath = library.Value;
                
                Log.LogMessage(MessageImportance.High, $"   Processing {libraryName} from {libraryPath}");

                if (!Directory.Exists(libraryPath))
                {
                    Log.LogWarning($"iOS native library directory not found: {libraryPath}");
                    continue;
                }

                // Look for .framework directories in the library path
                string[] frameworks = Directory.GetDirectories(libraryPath, "*.framework", SearchOption.TopDirectoryOnly);
                
                if (frameworks.Length == 0)
                {
                    Log.LogWarning($"No .framework directories found in {libraryPath}");
                    continue;
                }

                foreach (string frameworkDir in frameworks)
                {
                    string frameworkName = Path.GetFileName(frameworkDir);
                    string destFramework = Path.Combine(frameworksDir, frameworkName);

                    if (Directory.Exists(destFramework))
                    {
                        Log.LogMessage(MessageImportance.Normal, $"   Removing existing framework: {frameworkName}");
                        Directory.Delete(destFramework, true);
                    }

                    Log.LogMessage(MessageImportance.High, $"   ✅ Copying {frameworkName} framework");
                    CopyDirectory(frameworkDir, destFramework);
                }
            }

            Log.LogMessage(MessageImportance.High, "📦 iOS native libraries copied successfully");
        }

        private void ConfigureIOSNativeLibrariesInCMake(ref string content, string projectName)
        {
            // Build the framework lists for iOS
            var frameworkList = new List<string>();
            var frameworkLinks = new List<string>();

            string sanitizedProjectName = projectName.Replace("_", "-");

            // Always include the required frameworks
            frameworkList.Add("${FRAMEWORK_DIR}/sokol.framework");
            frameworkList.Add($"${{FRAMEWORK_DIR}}/{sanitizedProjectName}.framework");
            
            frameworkLinks.Add("\"-framework sokol\"");
            frameworkLinks.Add($"\"-framework {sanitizedProjectName}\"");

            // Add detected native libraries
            foreach (var library in iOSNativeLibraries)
            {
                string libraryName = library.Key;
                string libraryPath = library.Value;

                if (!Directory.Exists(libraryPath))
                {
                    continue;
                }

                // Look for .framework directories in the library path
                string[] frameworks = Directory.GetDirectories(libraryPath, "*.framework", SearchOption.TopDirectoryOnly);
                
                foreach (string frameworkDir in frameworks)
                {
                    string frameworkName = Path.GetFileNameWithoutExtension(frameworkDir); // Remove .framework extension
                    frameworkList.Add($"${{FRAMEWORK_DIR}}/{frameworkName}.framework");
                    frameworkLinks.Add($"\"-framework {frameworkName}\"");
                }
            }

            // Static vendor SDKs linked into the APP EXECUTABLE (IOSStaticFrameworks_*Path).
            // A static SWIFT framework (e.g. GoogleMobileAds 13+) must live in the main image —
            // absorbed into a clang-built plugin dylib the Swift runtime aborts at launch
            // resolving symbolic type references. -ObjC keeps the SDKs' ObjC classes alive
            // (nothing in the exe references them; plugin dylibs reach them via the ObjC
            // runtime), and the Swift compatibility shims + JavaScriptCore + libc++ cover the
            // SDKs' link-time needs (flag set proven in plugins/Ads/native/CMakeLists.txt).
            bool anyStaticFramework = false;
            foreach (var group in iOSStaticFrameworks)
            {
                if (!Directory.Exists(group.Value))
                {
                    continue;
                }

                foreach (string xcframework in Directory.GetDirectories(group.Value, "*.xcframework", SearchOption.TopDirectoryOnly))
                {
                    string sliceDir = Path.Combine(xcframework, "ios-arm64");
                    if (!Directory.Exists(sliceDir))
                    {
                        sliceDir = Directory.GetDirectories(xcframework, "ios-*", SearchOption.TopDirectoryOnly)
                            .FirstOrDefault(d => !Path.GetFileName(d).Contains("simulator"));
                    }
                    if (string.IsNullOrEmpty(sliceDir) || !Directory.Exists(sliceDir))
                    {
                        Log.LogWarning($"No device slice (ios-arm64) found in {xcframework} — skipping");
                        continue;
                    }

                    string staticFwName = Path.GetFileNameWithoutExtension(xcframework);
                    frameworkLinks.Add($"\"-F{sliceDir}\"");
                    frameworkLinks.Add($"\"-framework {staticFwName}\"");
                    anyStaticFramework = true;
                    Log.LogMessage(MessageImportance.High, $"   🔗 Linking static framework into the app executable: {staticFwName}");
                }
            }
            if (anyStaticFramework)
            {
                frameworkLinks.Add("\"-ObjC\"");
                frameworkLinks.Add("\"-framework JavaScriptCore\"");
                frameworkLinks.Add("\"-lc++\"");
                frameworkLinks.Add("\"-lswiftCompatibility51\"");
                frameworkLinks.Add("\"-lswiftCompatibility56\"");
                frameworkLinks.Add("\"-lswiftCompatibilityConcurrency\"");
                frameworkLinks.Add("\"-lswiftCompatibilityPacks\"");
                // What swiftc adds for deployment < iOS 15: the OS concurrency runtime, weak.
                // Without it the Swift runtime aborts resolving concurrency type metadata
                // ("missing weak symbol", mangling 'ScP') the moment the SDK touches it.
                frameworkLinks.Add("\"-Wl,-weak-lswift_Concurrency\"");
            }

            // Replace placeholders in CMakeLists.txt
            string embedFrameworksList = string.Join(";", frameworkList);
            string frameworkLinksList = string.Join("\n    ", frameworkLinks);

            content = content.Replace("TEMPLATE_EMBED_FRAMEWORKS_LIST", embedFrameworksList);
            content = content.Replace("TEMPLATE_FRAMEWORK_LINKS", frameworkLinksList);
            // The Swift compatibility shims resolve from the toolchain swift lib dir. Resolved
            // to an ABSOLUTE path here — $(TOOLCHAIN_DIR) is unreliable in the app target's link
            // (Xcode 26 resolved it to the Metal toolchain cryptex → 'swiftCompatibility51 not
            // found'); xcode-select gives the real default toolchain.
            string swiftLibSearchPaths = "$(inherited)";
            if (anyStaticFramework)
            {
                string xcodeDev = Cli.Wrap("xcode-select").WithArguments("-p")
                    .ExecuteBufferedAsync().GetAwaiter().GetResult().StandardOutput.Trim();
                swiftLibSearchPaths =
                    $"$(inherited) {xcodeDev}/Toolchains/XcodeDefault.xctoolchain/usr/lib/swift/iphoneos $(SDKROOT)/usr/lib/swift";
            }
            content = content.Replace("TEMPLATE_LIBRARY_SEARCH_PATHS", swiftLibSearchPaths);

            if (iOSNativeLibraries.Count > 0)
            {
                Log.LogMessage(MessageImportance.High, $"📋 Configured {iOSNativeLibraries.Count} iOS native libraries in CMakeLists.txt");
                foreach (var library in iOSNativeLibraries)
                {
                    Log.LogMessage(MessageImportance.High, $"   - {library.Key}");
                }
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

        private void ProcessIOSIcon(string iosDir, string projectDir)
        {
            if (string.IsNullOrWhiteSpace(iOSIcon))
            {
                Log.LogMessage(MessageImportance.Normal, "ℹ️  No IOSIcon specified in Directory.Build.props, using default icon");
                return;
            }

            // IOSIcon may point at a pre-made .appiconset DIRECTORY (or a folder containing one) instead of a
            // single PNG. It is copied VERBATIM, Contents.json included: a generated set already has the exact
            // idiom/scale/size metadata Apple expects, and the marketing 1024 is flattened opaque (no alpha),
            // which App Store validation requires and a naive resize of an alpha PNG would violate.
            // ⛔ Backwards compatibility: a FILE keeps the original resize+generate-Contents.json path below.
            string? sourceIconSet = FindAppIconSetDirectory(iOSIcon, projectDir);
            if (sourceIconSet != null)
            {
                CopyIOSAppIconSet(sourceIconSet, iosDir);
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

        /// <summary>Resolve <paramref name="iconPath"/> to a pre-made <c>*.appiconset</c> directory, using the
        /// same search order as <see cref="FindIconFile"/> (absolute → Assets/ → project-relative). Accepts
        /// either the .appiconset itself or a parent folder containing exactly one, so both
        /// <c>docs/branding/icons/ios</c> and <c>docs/branding/icons/ios/AppIcon.appiconset</c> work.
        /// Returns null when it is not a directory — which is what keeps the single-PNG path the default.</summary>
        private string? FindAppIconSetDirectory(string iconPath, string projectDir)
        {
            string? dir = null;
            if (Path.IsPathRooted(iconPath) && Directory.Exists(iconPath)) dir = iconPath;
            else if (Directory.Exists(Path.Combine(projectDir, "Assets", iconPath))) dir = Path.Combine(projectDir, "Assets", iconPath);
            else if (Directory.Exists(Path.Combine(projectDir, iconPath))) dir = Path.Combine(projectDir, iconPath);
            if (dir == null) return null;

            if (dir.EndsWith(".appiconset", StringComparison.OrdinalIgnoreCase)) return dir;

            var nested = Directory.GetDirectories(dir, "*.appiconset");
            if (nested.Length == 1) return nested[0];
            if (nested.Length > 1)
            {
                Log.LogWarning($"⚠️  '{dir}' contains {nested.Length} .appiconset folders — point IOSIcon at the one you want");
                return null;
            }
            Log.LogWarning($"⚠️  iOS icon directory '{dir}' contains no .appiconset — ignoring");
            return null;
        }

        /// <summary>Copy a pre-made asset-catalog icon set into <c>Assets.xcassets/AppIcon.appiconset</c>,
        /// Contents.json and all. The destination is cleared first so a stale icon from a previous build (or
        /// from the single-PNG path, whose filenames differ) can never survive and get picked up by Xcode.</summary>
        private void CopyIOSAppIconSet(string setDir, string iosDir)
        {
            Log.LogMessage(MessageImportance.High, $"📱 Processing iOS icon SET: {setDir}");
            try
            {
                string appIconDir = Path.Combine(iosDir, "Assets.xcassets", "AppIcon.appiconset");
                if (Directory.Exists(appIconDir)) Directory.Delete(appIconDir, recursive: true);
                Directory.CreateDirectory(appIconDir);

                int files = 0;
                foreach (string src in Directory.GetFiles(setDir))
                {
                    File.Copy(src, Path.Combine(appIconDir, Path.GetFileName(src)), overwrite: true);
                    files++;
                }

                if (!File.Exists(Path.Combine(appIconDir, "Contents.json")))
                    Log.LogWarning("⚠️  The copied icon set has no Contents.json — Xcode will not build an AppIcon from it");

                Log.LogMessage(MessageImportance.High, $"✅ iOS icon set copied verbatim ({files} file(s), Contents.json included)");
            }
            catch (Exception ex)
            {
                Log.LogWarning($"⚠️  Failed to copy iOS icon set: {ex.Message}");
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

        private bool ResizeImageWithSkiaSharp(string sourcePath, string destPath, int width, int height)
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

        private void CopyToOutputPath(string iosDir, string projectName, string buildType)
        {
            try
            {
                string sanitizedProjectName = projectName.Replace("_", "-");
                string buildDir = Path.Combine(iosDir, "build-xcode-ios-app");
                
                // Determine configuration based on build type
                string configuration = string.IsNullOrEmpty(buildType) || buildType.Equals("release", StringComparison.OrdinalIgnoreCase) 
                    ? "Release" 
                    : "Debug";
                
                string xcodeBuildConfig = $"{configuration}-iphoneos";
                string appBundlePath = Path.Combine(buildDir, xcodeBuildConfig, $"{sanitizedProjectName}-ios-app.app");

                // Check alternate locations
                if (!Directory.Exists(appBundlePath))
                {
                    string altPath = Path.Combine(buildDir, "bin", configuration, $"{sanitizedProjectName}-ios-app.app");
                    if (Directory.Exists(altPath))
                    {
                        appBundlePath = altPath;
                    }
                }

                // Also check the other configuration as fallback
                if (!Directory.Exists(appBundlePath))
                {
                    string fallbackConfig = configuration == "Release" ? "Debug" : "Release";
                    string fallbackPath = Path.Combine(buildDir, $"{fallbackConfig}-iphoneos", $"{sanitizedProjectName}-ios-app.app");
                    if (Directory.Exists(fallbackPath))
                    {
                        appBundlePath = fallbackPath;
                    }
                }

                if (!Directory.Exists(appBundlePath))
                {
                    Log.LogWarning($"App bundle not found, skipping output copy.");
                    return;
                }

                // Determine output base path: use custom path if specified, otherwise use project's output folder
                string outputBasePath = string.IsNullOrEmpty(opts.OutputPath) 
                    ? Path.Combine(opts.ProjectPath, "output") 
                    : opts.OutputPath;

                // Create output directory: {basePath}/iOS/{buildType}/
                string outputDir = Path.Combine(outputBasePath, "iOS", buildType);
                Directory.CreateDirectory(outputDir);

                // Copy .app bundle with descriptive name
                string outputAppBundle = Path.Combine(outputDir, $"{projectName}-{buildType}.app");
                
                // Remove existing bundle if present
                if (Directory.Exists(outputAppBundle))
                {
                    Directory.Delete(outputAppBundle, true);
                }

                // Copy the entire .app bundle directory
                CopyDirectory(appBundlePath, outputAppBundle);

                Log.LogMessage(MessageImportance.High, $"✅ iOS app bundle copied to: {outputAppBundle}");
            }
            catch (Exception ex)
            {
                Log.LogWarning($"Failed to copy iOS app bundle to output path: {ex.Message}");
            }
        }
    }
}