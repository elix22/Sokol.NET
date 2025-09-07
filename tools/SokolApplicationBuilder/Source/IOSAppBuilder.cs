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


namespace SokolApplicationBuilder
{
    enum ProcessortArchitecture
    {
        Intel=1,
        AppleSilicon
    }

    public class IOSBuildTask : Task
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

        string CLANG_CMD = string.Empty;
        string AR_CMD = string.Empty;
        string LIPO_CMD = string.Empty;
        string IOS_SDK_PATH = string.Empty;

        ProcessortArchitecture processortArchitecture = ProcessortArchitecture.Intel;

        public IOSBuildTask(Options opts)
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
            return BuildIOSApp();
        }

        private bool PublishAOT()
        {
            string buildType = "Debug";

            if (opts.Type == "release")
            {
                buildType = "Release";
            }
            string targetFramework = (opts.Framework != "")? opts.Framework : "net9.0";
            /*
             * dotnet publish -f net9.0 -r  ios-arm64 -c Release -p:OutputType=Library  -p:PublishAot=true -p:TrimmerRemoveSymbols=false -p:TrimMode=partial -p:DisableUnsupportedError=true -p:PublishAotUsingRuntimePack=true -p:RemoveSections=true -p:StripSymbols=true -p:BuildAsLibrary=true  -p:DefineConstants="IOS"
               mkdir Game.framework
               install_name_tool -rpath @executable_path @executable_path/Frameworks bin/Release/net9.0/ios-arm64/publish/libGame.dylib 
               install_name_tool -id @rpath/Game.framework/Game bin/Release/net9.0/ios-arm64/publish/libGame.dylib 
               lipo -create  bin/Release/net9.0/ios-arm64/publish/libGame.dylib -output Game.framework/Game
               cp  Info.plist Game.framework/Info.plist
               mkdir -p IOS/Frameworks
               rm -rf IOS/Frameworks/Game.framework
               mv -f Game.framework IOS/Frameworks
             */
            
            string dotnet_build_command = $"dotnet publish -f {targetFramework} -r  ios-arm64 -c {buildType} -p:OutputType=Library  -p:PublishAot=true -p:TrimmerRemoveSymbols=false -p:TrimMode=partial -p:DisableUnsupportedError=true -p:PublishAotUsingRuntimePack=true -p:RemoveSections=true -p:StripSymbols=true -p:BuildAsLibrary=true  -p:DefineConstants=\"IOS\"";

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
            
            Utils.DeleteDirectory(Path.Combine(opts.OutputPath, "IOS/Frameworks","Game.framework"));
            Directory.CreateDirectory(Path.Combine(opts.OutputPath, "IOS/Frameworks","Game.framework"));
            
            ( exitCode,  output) = Utils.RunShellCommand(Log,
                $"install_name_tool -rpath @executable_path @executable_path/Frameworks bin/Release/{targetFramework}/ios-arm64/publish/libGame.dylib",
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
            
            ( exitCode,  output) = Utils.RunShellCommand(Log,
                $"install_name_tool -id @rpath/Game.framework/Game bin/Release/{targetFramework}/ios-arm64/publish/libGame.dylib",
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
            
            ( exitCode,  output) = Utils.RunShellCommand(Log,
                $"lipo -create  bin/Release/{targetFramework}/ios-arm64/publish/libGame.dylib -output {Path.Combine(opts.OutputPath, "IOS/Frameworks","Game.framework","Game")}",
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
            
            File.Copy(Path.Combine(URHONET_HOME_PATH, "template/IOS" , "Info.plist"), Path.Combine(opts.OutputPath, "IOS/Frameworks","Game.framework","Info.plist"), true);
            
            return true;
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

            if (opts.DeveloperID == null)
            {
                Log.LogError("Developer ID can't be empty");
                return false;
            }

            opts.ProjectPath = opts.ProjectPath.Trim();
            opts.DeveloperID = opts.DeveloperID.Trim();
            opts.Type =    opts.Type.Trim();

            FileInfo fi = new FileInfo(opts.ProjectPath);
            opts.ProjectPath = fi.FullName;
            Console.WriteLine($"Project path:{opts.ProjectPath}");


            

            (int exitCode, string output) = Utils.RunShellCommand(Log,
                                             "xcode-select --print-path",
                                             envVars,
                                             workingDir: opts.ProjectPath,
                                             logStdErrAsMessage: true,
                                             debugMessageImportance: MessageImportance.High,
                                             label: "find-xcode");

            if (exitCode != 0 || output == string.Empty)
            {
                Log.LogError("xcode not found");
                return false;
            }

            CLANG_CMD = Path.Combine(output, "Toolchains/XcodeDefault.xctoolchain/usr/bin/clang");
            if (!File.Exists(CLANG_CMD))
            {
                Log.LogError($"clang not found , searched in {CLANG_CMD}");
                return false;
            }
            AR_CMD = Path.Combine(output, "Toolchains/XcodeDefault.xctoolchain/usr/bin/ar");
            if (!File.Exists(AR_CMD))
            {
                Log.LogError($"ar not found , searched in {AR_CMD}");
                return false;
            }
            LIPO_CMD = Path.Combine(output, "Toolchains/XcodeDefault.xctoolchain/usr/bin/lipo");
            if (!File.Exists(LIPO_CMD))
            {
                Log.LogError($"lipo not found , searched in {LIPO_CMD}");
                return false;
            }
            IOS_SDK_PATH = Path.Combine(output, "Platforms/iPhoneOS.platform/Developer/SDKs/iPhoneOS.sdk");
            if (!Directory.Exists(IOS_SDK_PATH))
            {
                Log.LogError($"iOS sdk not found , searched in {IOS_SDK_PATH}");
                return false;
            }

            if (opts.Type != "debug" && opts.Type != "release")
            {
                Log.LogError($"You must provide build type release/debug");
                return false;
            }


            SetOutputPath();

            if (opts.DeveloperID == string.Empty)
            {
                if (!File.Exists(Path.Combine(opts.OutputPath, "IOS/build/ios_env_vars.sh")))
                {
                    Log.LogError("Apple Developer ID not provided");
                    return false;
                }
                else
                {
                    string project_vars_path = Path.Combine(opts.OutputPath, "IOS/build/ios_env_vars.sh");
                    ParseEnvironmentVars(project_vars_path);
                }
            }
            else
            {
                envVars["DEVELOPMENT_TEAM"] = opts.DeveloperID;
                envVars["CODE_SIGN_IDENTITY"] = string.Empty;
                envVars["PROVISIONING_PROFILE_SPECIFIER"] = string.Empty;
            }

            envVars["MONO_PATH"] = Path.Combine(opts.OutputPath, "IOS/bin/Data/DotNet/ios");
            envVars["URHO3D_HOME"] = Path.Combine(opts.OutputPath, "IOS");


            DEVELOPMENT_TEAM = envVars["DEVELOPMENT_TEAM"];

            if (DEVELOPMENT_TEAM == string.Empty || DEVELOPMENT_TEAM == null)
            {
                Log.LogError("DEVELOPMENT_TEAM ID is empty");
                return false;
            }

            Console.WriteLine($"DEVELOPMENT_TEAM = {DEVELOPMENT_TEAM}");

            bool result = CheckDependencies(new List<string> { "cmake", "xcodebuild", "ios-deploy", "codesign", "dotnet", "plutil" });
            if (!result)
            {
                Log.LogError("Not all dependencies found");
                return false;
            }



            GetUrhoNetHomePath();
            if (URHONET_HOME_PATH == string.Empty) return false;

            ParseEnvironmentVariables();

            if (!Directory.Exists(Path.Combine(opts.OutputPath, "IOS")))
            {
                if (!Directory.Exists(Path.Combine(URHONET_HOME_PATH, "template/IOS")))
                {
                    Log.LogError(Path.Combine(URHONET_HOME_PATH, "template/IOS") + "Doesn't exist");
                    return false;
                }

                Path.Combine(URHONET_HOME_PATH, "template/IOS").CopyDirectory(Path.Combine(opts.OutputPath, "IOS"), true);
            }

            Path.Combine(opts.OutputPath, $"IOS/CMakeLists.txt").ReplaceInfile("TEMPLATE_PROJECT_NAME", PROJECT_NAME);
  
          
            (exitCode,  output) = Utils.RunShellCommand(Log,
                              "chmod +x *.sh",
                              envVars,
                              workingDir: Path.Combine(opts.OutputPath, "IOS/script"),
                              logStdErrAsMessage: true,
                              debugMessageImportance: MessageImportance.High,
                              label: "chmod-sh");

            if (exitCode != 0)
            {
                Log.LogError("chmod +x *.sh failed");
                return false;
            }


            (exitCode, output) = Utils.RunShellCommand(Log,
                              "chmod +x .bash_helpers.sh",
                              envVars,
                              workingDir: Path.Combine(opts.OutputPath, "IOS/script"),
                              logStdErrAsMessage: true,
                              debugMessageImportance: MessageImportance.High,
                              label: "chmod-sh");

            if (exitCode != 0)
            {
                Log.LogError("chmod +x .bash_helpers.sh");
                return false;
            }



            Directory.CreateDirectory(Path.Combine(opts.OutputPath, "IOS/bin"));
            Utils.RunShellCommand(Log,
                                      "ln -sf  ../../Assets/* .",
                                      envVars,
                                      workingDir: Path.Combine(opts.OutputPath, "IOS/bin"),
                                      logStdErrAsMessage: true,
                                      debugMessageImportance: MessageImportance.High,
                                      label: "link-assets");

            // TBD ELI
            // if (!HandlePlugins())
            // {
            //     return false;
            // }

            Utils.RunShellCommand(Log,
                           "plutil -remove NSUserTrackingUsageDescription  CMake/Modules/iOSBundleInfo.plist.template",
                           envVars,
                           workingDir: Path.Combine(opts.OutputPath, "IOS"),
                           logStdErrAsMessage: true,
                           debugMessageImportance: MessageImportance.High,
                           label: "plist-update");

            Utils.RunShellCommand(Log,
            "plutil -replace NSUserTrackingUsageDescription  -string \"This identifier will be used to deliver personalized ads to you.\" CMake/Modules/iOSBundleInfo.plist.template",
            envVars,
            workingDir: Path.Combine(opts.OutputPath, "IOS"),
            logStdErrAsMessage: true,
            debugMessageImportance: MessageImportance.High,
            label: "plist-update");

            string SCREEN_ORIENTATION = GetEnvValue("SCREEN_ORIENTATION");
            if (SCREEN_ORIENTATION == string.Empty)
            {
                SCREEN_ORIENTATION = "landscape";
            }
            if (SCREEN_ORIENTATION != "landscape" && SCREEN_ORIENTATION != "portrait")
            {
                SCREEN_ORIENTATION = "landscape";
            }

            Utils.RunShellCommand(Log,
                $"plutil -remove UISupportedInterfaceOrientations  {envVars["URHO3D_HOME"]}/CMake/Modules/iOSBundleInfo.plist.template",
                envVars,
                workingDir: Path.Combine(opts.OutputPath, "IOS"),
                logStdErrAsMessage: true,
                debugMessageImportance: MessageImportance.High,
                label: "plist-update");

            Utils.RunShellCommand(Log,
                $"plutil -insert UISupportedInterfaceOrientations -array  {envVars["URHO3D_HOME"]}/CMake/Modules/iOSBundleInfo.plist.template",
                envVars,
                workingDir: Path.Combine(opts.OutputPath, "IOS"),
                logStdErrAsMessage: true,
                debugMessageImportance: MessageImportance.High,
                label: "plist-update");


            if (SCREEN_ORIENTATION == "landscape")
            {
                Utils.RunShellCommand(Log,
                     $"plutil -insert UISupportedInterfaceOrientations -string UIInterfaceOrientationLandscapeLeft -append {envVars["URHO3D_HOME"]}/CMake/Modules/iOSBundleInfo.plist.template",
                     envVars,
                     workingDir: Path.Combine(opts.OutputPath, "IOS"),
                     logStdErrAsMessage: true,
                     debugMessageImportance: MessageImportance.High,
                     label: "plist-update");

                Utils.RunShellCommand(Log,
                     $"plutil -insert UISupportedInterfaceOrientations -string UIInterfaceOrientationLandscapeRight -append {envVars["URHO3D_HOME"]}/CMake/Modules/iOSBundleInfo.plist.template",
                     envVars,
                     workingDir: Path.Combine(opts.OutputPath, "IOS"),
                     logStdErrAsMessage: true,
                     debugMessageImportance: MessageImportance.High,
                     label: "plist-update");
            }
            else
            {
                Utils.RunShellCommand(Log,
                     $"plutil -insert UISupportedInterfaceOrientations -string UIInterfaceOrientationPortrait -append {envVars["URHO3D_HOME"]}/CMake/Modules/iOSBundleInfo.plist.template",
                     envVars,
                     workingDir: Path.Combine(opts.OutputPath, "IOS"),
                     logStdErrAsMessage: true,
                     debugMessageImportance: MessageImportance.High,
                     label: "plist-update");

                Utils.RunShellCommand(Log,
                     $"plutil -insert UISupportedInterfaceOrientations -string UIInterfaceOrientationPortraitUpsideDown -append {envVars["URHO3D_HOME"]}/CMake/Modules/iOSBundleInfo.plist.template",
                     envVars,
                     workingDir: Path.Combine(opts.OutputPath, "IOS"),
                     logStdErrAsMessage: true,
                     debugMessageImportance: MessageImportance.High,
                     label: "plist-update");
            }


            Utils.RunShellCommand(Log,
                           $"plutil -remove GADIsAdManagerApp  {envVars["URHO3D_HOME"]}/CMake/Modules/iOSBundleInfo.plist.template",
                           envVars,
                           workingDir: Path.Combine(opts.OutputPath, "IOS"),
                           logStdErrAsMessage: true,
                           debugMessageImportance: MessageImportance.High,
                           label: "plist-update");

            Utils.RunShellCommand(Log,
                  $"plutil -remove GADApplicationIdentifier  {envVars["URHO3D_HOME"]}/CMake/Modules/iOSBundleInfo.plist.template",
                  envVars,
                  workingDir: Path.Combine(opts.OutputPath, "IOS"),
                  logStdErrAsMessage: true,
                  debugMessageImportance: MessageImportance.High,
                  label: "plist-update");

            string GAD_APPLICATION_ID = GetEnvValue("GAD_APPLICATION_ID");
            if (GAD_APPLICATION_ID != string.Empty)
            {

                Utils.RunShellCommand(Log,
                $"plutil -replace GADIsAdManagerApp  -bool 'true' {envVars["URHO3D_HOME"]}/CMake/Modules/iOSBundleInfo.plist.template",
                envVars,
                workingDir: Path.Combine(opts.OutputPath, "IOS"),
                logStdErrAsMessage: true,
                debugMessageImportance: MessageImportance.High,
                label: "plist-update");

                Utils.RunShellCommand(Log,
                $"plutil -replace GADApplicationIdentifier  -string {GAD_APPLICATION_ID} {envVars["URHO3D_HOME"]}/CMake/Modules/iOSBundleInfo.plist.template",
                envVars,
                workingDir: Path.Combine(opts.OutputPath, "IOS"),
                logStdErrAsMessage: true,
                debugMessageImportance: MessageImportance.High,
                label: "plist-update");
            }
          
  

            Directory.CreateDirectory(Path.Combine(opts.OutputPath, "IOS/Frameworks"));

            if (opts.Type == "debug")
            {
                
                // if (File.Exists(Path.Combine(opts.OutputPath, "IOS/lib", "libUrho3D.a")))
                //     File.Delete(Path.Combine(opts.OutputPath, "IOS/lib", "libUrho3D.a"));
                // (exitCode, output) = Utils.RunShellCommand(Log,
                //               $"cat libUrho3D.split.??  > {Path.Combine(opts.OutputPath, "IOS/lib", "libUrho3D.a")}",
                //               envVars,
                //               workingDir: Path.Combine(URHONET_HOME_PATH, "template/libs/ios/urho3d", renderingBackend, "debug"),
                //               logStdErrAsMessage: true,
                //               debugMessageImportance: MessageImportance.High,
                //               label: "copy cat libUrho3D.a");
                // TBD ELI
                Path.Combine(URHONET_HOME_PATH, "template/libs/ios/urho3d","release").CopyDirectory(Path.Combine(opts.OutputPath, "IOS/Frameworks"), true);
            }
            else
            {
                Path.Combine(URHONET_HOME_PATH, "template/libs/ios/urho3d","release").CopyDirectory(Path.Combine(opts.OutputPath, "IOS/Frameworks"), true);
            }


            Directory.CreateDirectory(Path.Combine(opts.OutputPath, "IOS/build"));

            if (opts.DeveloperID != string.Empty && opts.DeveloperID != null)
            {
                File.Copy(Path.Combine(opts.OutputPath, "IOS/script/ios_env_vars.sh"), Path.Combine(opts.OutputPath, "IOS/build/ios_env_vars.sh"), true);
                Path.Combine(opts.OutputPath, "IOS/build/ios_env_vars.sh").ReplaceInfile("T_DEVELOPMENT_TEAM", opts.DeveloperID);
                Path.Combine(opts.OutputPath, "IOS/build/ios_env_vars.sh").ReplaceInfile("T_CODE_SIGN_IDENTITY", string.Empty);
                Path.Combine(opts.OutputPath, "IOS/build/ios_env_vars.sh").ReplaceInfile("T_PROVISIONING_PROFILE_SPECIFIER", string.Empty);
            }
            else
            {
                if (!File.Exists(Path.Combine(opts.OutputPath, "IOS/build/ios_env_vars.sh")))
                {
                    Log.LogError("Developer ID not provided");
                    return false;
                }
            }

            
            File.Copy(Path.Combine(opts.OutputPath, "IOS/script/ios.entitlements"), Path.Combine(opts.OutputPath, "IOS/build/ios.entitlements"), true);
            Path.Combine(opts.OutputPath, "IOS/build/ios.entitlements").ReplaceInfile("T_DEVELOPER_ID", (opts.DeveloperID == null)?"":opts.DeveloperID);
            Path.Combine(opts.OutputPath, "IOS/build/ios.entitlements").ReplaceInfile("T_UUID", PROJECT_UUID);


            string buildType = "Debug";

            if (opts.Type == "release")
            {
                buildType = "Release";
            }


            if (!PublishAOT())
            {
                Log.LogError("dotnet publish error");
                return false;
            }
            
            (exitCode, output) = Utils.RunShellCommand(Log,
                       $"{Path.Combine(opts.OutputPath, "IOS/script/cmake_ios_dotnet.sh")} {Path.Combine(opts.OutputPath, "IOS/build")} -DDEVELOPMENT_TEAM={GetEnvValue("DEVELOPMENT_TEAM")} -DCODE_SIGN_IDENTITY={GetEnvValue("CODE_SIGN_IDENTITY")} -DPROVISIONING_PROFILE_SPECIFIER={GetEnvValue("PROVISIONING_PROFILE_SPECIFIER")}",
                       envVars,
                       workingDir: opts.OutputPath,
                       logStdErrAsMessage: true,
                       debugMessageImportance: MessageImportance.High,
                       label: "cmake-ios");


            if (exitCode != 0)
            {
                Log.LogError($"cmake failed ");
                return false;
            }

            string buildConfiguration = "Debug";
            if(opts.Type == "release")
            {
                buildConfiguration = "Release";
            }

            (exitCode, output) = Utils.RunShellCommand(Log,
                              $"xcodebuild -project {Path.Combine(opts.OutputPath, "IOS/build")}/{PROJECT_NAME}.xcodeproj -configuration {buildConfiguration}  ",
                              envVars,
                              workingDir: opts.OutputPath,
                              logStdErrAsMessage: true,
                              debugMessageImportance: MessageImportance.High,
                              label: "xcode-build");


            if (exitCode != 0)
            {
                Log.LogError($"xcode build failed ");
                return false;
            }

            if (opts.Install)
            {
                (exitCode, output) = Utils.RunShellCommand(Log,
                  $"ios-deploy --justlaunch --debug --bundle  {Path.Combine(opts.OutputPath, "IOS/build/bin")}/{PROJECT_NAME}.app  ",
                  envVars,
                  workingDir: opts.OutputPath,
                  logStdErrAsMessage: true,
                  debugMessageImportance: MessageImportance.High,
                  label: "ios-deploy");
                if (exitCode != 0)
                {
                    Log.LogError($"ios-deploy failed ");
                    return false;
                }
            }
            else if (opts.Debug)
            {
                (exitCode, output) = Utils.RunShellCommand(Log,
                  $"ios-deploy  --debug --bundle  {Path.Combine(opts.OutputPath, "IOS/build/bin")}/{PROJECT_NAME}.app  ",
                  envVars,
                  workingDir: opts.OutputPath,
                  logStdErrAsMessage: true,
                  debugMessageImportance: MessageImportance.High,
                  label: "ios-deploy");
                if (exitCode != 0)
                {
                    Log.LogError($"ios-deploy failed ");
                    return false;
                }
            }

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

        private void SetOutputPath()
        {
            if (opts.OutputPath == string.Empty)
            {
                opts.OutputPath = opts.ProjectPath;
            }

            if (!Directory.Exists(opts.OutputPath))
                Directory.CreateDirectory(opts.OutputPath);
        }
        string GetUrhoNetHomePath()
        {

            if (URHONET_HOME_PATH != string.Empty) return URHONET_HOME_PATH;

            string homeFolder = Utils.GetHomeFolder();
            string urhoNetConfigFolderPath = Path.Combine(homeFolder, ".urhonet_config");

            if (!Directory.Exists(urhoNetConfigFolderPath))
            {
                Log.LogError($".urhonet_config folder not found");
                return string.Empty;
            }

            string urhoNetHomeFilePath = Path.Combine(urhoNetConfigFolderPath, "urhonethome");
            if (!File.Exists(urhoNetHomeFilePath))
            {
                Log.LogError($"urhonethome not found");
                return string.Empty;
            }


            string[] lines = urhoNetHomeFilePath.FileReadAllLines();
            foreach (var line in lines)
            {
                if (line == string.Empty) continue;

                if (Directory.Exists(line))
                {
                    URHONET_HOME_PATH = line;
                    break;
                }
            }

            return URHONET_HOME_PATH;
        }

        private bool ParseEnvironmentVariables()
        {

            string project_vars_path = Path.Combine(opts.ProjectPath, "script", "project_vars.sh");

            if (!File.Exists(project_vars_path))
            {
                Log.LogError($"project_vars.sh not found");
                return false;
            }
            ParseEnvironmentVars(project_vars_path);

            PROJECT_UUID = GetEnvValue("PROJECT_UUID");
            PROJECT_NAME = GetEnvValue("PROJECT_NAME");
            JAVA_PACKAGE_PATH = GetEnvValue("JAVA_PACKAGE_PATH");
            VERSION_CODE = GetEnvValue("VERSION_CODE");
            VERSION_NAME = GetEnvValue("VERSION_NAME");

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
                    envVars[vars[0].Trim()] = vars[1].Trim();
                }
            }
        }

        private bool HandlePlugins()
        {
            List<string> PLUGINS = GetPlugins();
            if (PLUGINS.Count > 0)
            {
                Directory.CreateDirectory(Path.Combine(opts.OutputPath, "IOS/Plugins"));
                foreach (string plugin in PLUGINS)
                {
                    if (!Directory.Exists(Path.Combine(URHONET_HOME_PATH, $"template/Plugins/{plugin}")))
                    {
                        Log.LogError(Path.Combine(URHONET_HOME_PATH, $"template/Plugins/{plugin}") + " not found");
                        return false;
                    }

                    if (!Directory.Exists(Path.Combine(opts.OutputPath, $"IOS/Plugins/{plugin}")))
                        Path.Combine(URHONET_HOME_PATH, $"template/Plugins/{plugin}").CopyDirectory(Path.Combine(opts.OutputPath, $"IOS/Plugins/{plugin}"));
                }

            }
            return true;
        }

        List<string> GetPlugins()
        {
            List<string> result = new List<string>();

            string PLUGINS = GetEnvValue("PLUGINS");
            if (PLUGINS != string.Empty)
            {
                result = SplitToList(PLUGINS);
            }

            return result;
        }

        List<string> GetDotNetReferenceAssemblies()
        {
            List<string> result = new List<string>();

            string DOTNET_REFERENCE_DLL = GetEnvValue("DOTNET_REFERENCE_DLL");
            if (DOTNET_REFERENCE_DLL != string.Empty)
            {
                result = SplitToList(DOTNET_REFERENCE_DLL);
            }

            return result;
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

        string GetEnvValue(string key)
        {
            string value = string.Empty;
            if (envVars.TryGetValue(key, out var val))
            {
                value = val;
                value = value.Replace("\'", "");
            }
            return value.Trim();
        }

        bool CheckDependencies(List<string> deps)
        {
            bool result = true;

            foreach (var dep in deps)
            {
                if (!iscmd(dep))
                {
                    result = false;
                    Log.LogError(dep + " not found");
                    break;
                }
            }

            return result;
        }

        bool iscmd(string cmd)
        {
            bool result = false;
            string command = " command -v " + cmd;
            (int exitCode, string output) = Utils.RunShellCommand(Log,
                                 command,
                                 null,
                                 workingDir: opts.ProjectPath,
                                 logStdErrAsMessage: true,
                                 debugMessageImportance: MessageImportance.High,
                                 label: "iscmd");

            if (output != string.Empty && exitCode == 0) result = true;

            return result;
        }


    }



}