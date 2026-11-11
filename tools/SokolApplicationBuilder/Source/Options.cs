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

        [Option( "obfuscate", Required = false, HelpText = "Obfuscate the app's managed code before AOT (requires obfuscate.xml in the project folder; release builds only). See docs/OBFUSCATION_TOOL_DESIGN.md")]
        public bool Obfuscate { get; set; } = false;

        [Option( "no-obfuscate", Required = false, HelpText = "Force obfuscation OFF for this build, overriding <Obfuscation>true</Obfuscation> in the project's Directory.Build.props. Use when you need readable stack traces or un-perturbed performance numbers.")]
        public bool NoObfuscate { get; set; } = false;

        [Option( "obfuscation-config", Required = false, HelpText = "Override path to the obfuscation XML config (default: <project>/obfuscate.xml).")]
        public string ObfuscationConfig { get; set; } = "";

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

        [Option("export-method", Required = false, HelpText = "iOS, with --subtask ipa: how the .ipa is signed - app-store-connect (default: for the App Store and TestFlight), release-testing (ad hoc), debugging (development) or enterprise")]
        public string ExportMethod { get; set; } = "";

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

        [Option("device-archs", Required = false, HelpText = "Build only for architectures matching currently connected Android devices (faster local iteration). Default: build all architectures.")]
        public bool DeviceArchs { get; set; } = false;

        [Option("destination", Required = false, HelpText = "Destination path for create project task")]
        public string Destination { get; set; } = "";

        [Option("template", Required = false, HelpText = "Override template path for create project task")]
        public string TemplatePath { get; set; } = "";

        // Image processing options
        [Option("source", Required = false, HelpText = "Source image path for image processing task")]
        public string SourceImage { get; set; } = "";

        [Option("dest", Required = false, HelpText = "Destination image path for image processing task")]
        public string DestImage { get; set; } = "";

        [Option("width", Required = false, HelpText = "Target width for image processing (default: 800)")]
        public int Width { get; set; } = 0;

        [Option("height", Required = false, HelpText = "Target height for image processing (default: 600)")]
        public int Height { get; set; } = 0;

        [Option("mode", Required = false, HelpText = "Image processing mode: 'crop' (fill, may crop), 'fit' (contain entire image, may add padding), or 'cut' (extract region without resize). Default: 'crop'")]
        public string ImageMode { get; set; } = "crop";

        [Option("cutx", Required = false, HelpText = "X coordinate (starting point) for cut/crop/fit modes. Default: 0")]
        public int CutX { get; set; } = 0;

        [Option("cuty", Required = false, HelpText = "Y coordinate (starting point) for cut/crop/fit modes. Default: 0")]
        public int CutY { get; set; } = 0;

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
                    "portrait"              => "portrait",
                    "portrait_upside_down"  => "portrait_upside_down",
                    "landscape"             => "landscape",
                    "landscape_left"        => "landscape_left",
                    "landscape_right"       => "landscape_right",
                    "both"                  => "both",
                    _ => "both" // default fallback
                };
            }
        }

    }

}
