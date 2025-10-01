using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Security;
using CommandLine;
// dotnet run --task AndroidBuild --path /Users/elialoni/Development/SliderZ --output /Users/elialoni/Development/SliderZ/output
namespace SokolApplicationBuilder
{
    public class Options
    {

        [Option("task", Required = true, HelpText = "Set Task")]
        public string Task { get; set; } = "";

        [Option("architecture", Required = false, HelpText = "Set Architecture , android/ios/desktop/web")]
        public string Arch { get; set; } = "";

        [Option("subtask", Required = false, HelpText = "Set Sub Task")]
        public string SubTask { get; set; } = "";

        [Option("type", Required = false, HelpText = "Set type release/debug")]
        public string Type { get; set; } = "";

        [Option("path", Default = "", Required = false, HelpText = "Set project path")]
        public string ProjectPath { get; set; } = "";

        [Option("output", Required = false, HelpText = "Set output path for build binaries")]
        public string OutputPath { get; set; } = "";

        [Option("verbose", Required = false, HelpText = "Set verbose messages")]
        public bool Verbose { get; set; } = false;

        [Option( "install", Required = false, HelpText = "Install binary")]
        public bool Install { get; set; } = false;

        [Option( "encrypt", Required = false, HelpText = "Encrypt Game.dll")]
        public bool Encrypt { get; set; } = false;

        [Option( "encrypt-key", Required = false, HelpText = "Encryption key path relative to project path")]
        public string EncryptKeyPath { get; set; } = "";

        [Option("developer", Required = false, HelpText = "iOS developer ID")]
        public string DeveloperID { get; set; } = "";

        [Option("device", Required = false, HelpText = "Android device ID to install to (use 'adb devices' to list available devices)")]
        public string DeviceId { get; set; } = "";

        [Option("ios-device", Required = false, HelpText = "iOS device ID to install to (use 'ios-deploy -c' to list available devices). If not specified, installs on all connected devices.")]
        public string IOSDeviceId { get; set; } = "";

        [Option("graphics", Required = false, HelpText = "Graphics backend , default is OpenGL/OpenGL ES")]
        public string GraphicsBackend { get; set; } = "";

        [Option( "debug", Required = false, HelpText = "install and debug binary")]
        public bool Debug { get; set; } = false;

        [Option( "obfuscate", Required = false, HelpText = "obfuscate the source code")]
        public bool Obfuscate { get; set; } = false;

        [Option("keystore", Required = false, HelpText = "Android key store path")]
        public string KeyStorePath { get; set; } = "";
        
        [Option( "aot", Required = false, HelpText = "AOT mode")]
        public bool Aot { get; set; } = false;
        
        [Option("framework", Required = false, HelpText = "Set .NET target framework")]
        public string Framework { get; set; } = "";
        
        [Option("properties", Required = false, HelpText = "extra  properties  -p:key=value -p:key=value  ")]
        public string Properties { get; set; } = "";

        [Option( "rid", Required = false, HelpText = "runtime identifier")]
        public string  RID { get; set; } = "";

        // iOS specific options
        [Option("development-team", Required = false, HelpText = "iOS development team ID")]
        public string DevelopmentTeam { get; set; } = "";

        [Option("compile", Required = false, HelpText = "Compile the Xcode project after generation")]
        public bool Compile { get; set; } = false;

        [Option("run", Required = false, HelpText = "Launch the app after installation")]
        public bool Run { get; set; } = false;

        [Option("orientation", Required = false, HelpText = "iOS app orientation: portrait, landscape, or both (default: both)")]
        public string Orientation { get; set; } = "both";

        [Option("project", Required = false, HelpText = "Project name to build (if not specified, auto-detect based on folder contents)")]
        public string ProjectName { get; set; } = "";

        [Option("linker-flags", Required = false, HelpText = "Additional linker flags to pass to the compiler")]
        public string LinkerFlags { get; set; } = "";

        [Option("interactive", Required = false, HelpText = "Enable interactive device selection when multiple devices are connected")]
        public bool Interactive { get; set; } = false;

        // Computed properties
        public string Path => ProjectPath;
        public string TemplatesPath => System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "", "templates");

        public string ValidatedOrientation
        {
            get
            {
                string orientation = Orientation?.ToLower() ?? "both";
                return orientation switch
                {
                    "portrait" => "portrait",
                    "landscape" => "landscape",
                    "both" => "both",
                    _ => "both" // default fallback
                };
            }
        }

    }

}
