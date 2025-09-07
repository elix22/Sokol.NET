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

                Log.LogMessage(MessageImportance.High, $"Build type: {buildType}");
                if (installApp)
                    Log.LogMessage(MessageImportance.High, "Will install APK/AAB on device after build");

                // Get app name
                string appName = GetAppName();
                Log.LogMessage(MessageImportance.High, $"Configuring Android app for: {appName}");

                // Copy Android template
                CopyAndroidTemplate();

                // Configure Android app
                ConfigureAndroidApp(appName);

                // Compile shaders
                CompileShaders();

                // Publish .NET assemblies for different architectures
                PublishAssemblies();

                // Build Android app
                BuildAndroidApp(appName, buildType);

                // Sign if release
                if (buildType == "release")
                    SignReleaseApp();

                // Install if requested
                if (installApp)
                    InstallOnDevice(appName, buildType);

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
            // Get app name from csproj or directory
            string csprojFile = Directory.GetFiles(opts.ProjectPath, "*.csproj").FirstOrDefault();
            if (!string.IsNullOrEmpty(csprojFile))
            {
                return Path.GetFileNameWithoutExtension(csprojFile);
            }
            return Path.GetFileName(opts.ProjectPath);
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
                content = content.Replace("android:label=\"NativeActivity\"", $"android:label=\"{appName}\"");
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

            var result = Cli.Wrap("dotnet")
                .WithArguments("msbuild -t:CompileShaders -p:DefineConstants=\"__ANDROID__\"")
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
                    var result = Cli.Wrap("dotnet")
                        .WithArguments($"publish -r {arch} -p:BuildAsLibrary=true -p:DisableUnsupportedError=true -p:PublishAotUsingRuntimePack=true -p:RemoveSections=true -p:DefineConstants=\"__ANDROID__\" --verbosity quiet")
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
                    .WithArguments($"assembleRelease -PcmakeArgs=\"-DAPP_NAME={appName}\"")
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
                    .WithArguments($"assembleDebug -PcmakeArgs=\"-DAPP_NAME={appName}\"")
                    .WithWorkingDirectory(androidPath)
                    .WithStandardOutputPipe(PipeTarget.ToDelegate(s => Log.LogMessage(MessageImportance.Normal, s)))
                    .WithStandardErrorPipe(PipeTarget.ToDelegate(s => Log.LogError(s)))
                    .ExecuteAsync()
                    .GetAwaiter()
                    .GetResult();

                Log.LogMessage(MessageImportance.High, $"Debug APK build completed with exit code: {result.ExitCode}");
            }
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

        void InstallOnDevice(string appName, string buildType)
        {
            Log.LogMessage(MessageImportance.High, "Installing on Android device...");

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
                    return;
                }
            }
            catch (Exception ex)
            {
                Log.LogError($"ADB not found or failed: {ex.Message}");
                return;
            }

            // Parse device list
            var devices = new List<string>();
            var lines = deviceListOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                if (!line.Contains("List of devices") && !string.IsNullOrWhiteSpace(line))
                {
                    var parts = line.Split('\t', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2 && parts[1] == "device")
                    {
                        devices.Add(parts[0]);
                    }
                }
            }

            if (devices.Count == 0)
            {
                Log.LogError("No Android devices found. Please connect a device and enable USB debugging.");
                return;
            }

            string selectedDeviceId = "";
            if (!string.IsNullOrEmpty(opts.DeviceId))
            {
                // User specified a device ID
                if (devices.Contains(opts.DeviceId))
                {
                    selectedDeviceId = opts.DeviceId;
                    Log.LogMessage(MessageImportance.High, $"Using specified device: {selectedDeviceId}");
                }
                else
                {
                    Log.LogError($"Specified device '{opts.DeviceId}' not found. Available devices:");
                    for (int i = 0; i < devices.Count; i++)
                    {
                        Log.LogError($"  {devices[i]}");
                    }
                    return;
                }
            }
            else if (devices.Count == 1)
            {
                selectedDeviceId = devices[0];
                Log.LogMessage(MessageImportance.High, $"Found device: {selectedDeviceId}");
            }
            else
            {
                Log.LogMessage(MessageImportance.High, $"Found {devices.Count} connected devices:");
                for (int i = 0; i < devices.Count; i++)
                {
                    Log.LogMessage(MessageImportance.High, $"{i + 1}. {devices[i]}");
                }

                // Use the first device as fallback
                selectedDeviceId = devices[0];
                Log.LogMessage(MessageImportance.High, $"Using first device: {selectedDeviceId}");
                Log.LogWarning("Multiple devices found. Using the first one. Use --device <device_id> to specify which device to use.");
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

            // Install APK on selected device
            var installResult = Cli.Wrap("adb")
                .WithArguments($"-s {selectedDeviceId} install -r \"{apkPath}\"")
                .WithStandardOutputPipe(PipeTarget.ToDelegate(s => Log.LogMessage(MessageImportance.Normal, s)))
                .WithStandardErrorPipe(PipeTarget.ToDelegate(s => Log.LogError(s)))
                .ExecuteAsync()
                .GetAwaiter()
                .GetResult();

            if (installResult.ExitCode == 0)
            {
                Log.LogMessage(MessageImportance.High, "APK installed successfully!");

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
                    Log.LogMessage(MessageImportance.High, "App launched successfully!");
                }
                catch
                {
                    Log.LogWarning($"Could not launch app automatically. Package: {packageName}");
                }
            }
            else
            {
                Log.LogError("Failed to install APK on device!");
            }
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

            string SCREEN_ORIENTATION = GetEnvValue("SCREEN_ORIENTATION");
            if (SCREEN_ORIENTATION == string.Empty)
            {
                SCREEN_ORIENTATION = "landscape";
            }
            if (SCREEN_ORIENTATION != "landscape" && SCREEN_ORIENTATION != "portrait")
            {
                SCREEN_ORIENTATION = "landscape";
            }

            AndroidManifest.AppendTextLine($"      <activity android:name=\".UrhoMainActivity\" android:exported=\"true\" android:configChanges=\"keyboardHidden|orientation|screenSize\" android:screenOrientation=\"{SCREEN_ORIENTATION}\" android:theme=\"@android:style/Theme.NoTitleBar.Fullscreen\"/>");

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

