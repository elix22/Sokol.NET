using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using SkiaSharp;
using Task = Microsoft.Build.Utilities.Task;

namespace SokolApplicationBuilder
{
    public class DesktopBuildTask : Task
    {
        private readonly Options opts;

        public DesktopBuildTask(Options opts)
        {
            this.opts = opts;
            Utils.opts = opts;
        }

        public override bool Execute()
        {
            try
            {
                Log.LogMessage(MessageImportance.High, "🖥️  Building Desktop Application...");

                // Determine project name
                string projectName = GetProjectName(opts.ProjectPath);
                if (string.IsNullOrEmpty(projectName))
                {
                    Log.LogError("Could not determine project name");
                    return false;
                }

                Log.LogMessage(MessageImportance.Normal, $"Project name: {projectName}");

                // Find project file
                string projectFile = Path.Combine(opts.ProjectPath, $"{projectName}.csproj");
                if (!File.Exists(projectFile))
                {
                    Log.LogError($"Project file not found: {projectFile}");
                    if (!Utils.FindProjectInPath(opts.ProjectPath, ref projectFile))
                    {
                        Log.LogError("No .csproj file found in project directory");
                        return false;
                    }
                    Log.LogMessage(MessageImportance.Normal, $"Using project file: {projectFile}");
                }

                // A game made with the engine (its build files import the engine's Game.targets): the app is compiled
                // ahead of time, so its scripts are registered - and their fields set - by generated code, as on the
                // phones (RegisteredScripts.g.cs, compiled in under _DESKTOP_PUBLISHED_BINARY_). Any other project is
                // left alone.
                string engineTargets = Path.Combine(opts.ProjectPath, "Directory.Build.targets");
                if (File.Exists(engineTargets) && File.ReadAllText(engineTargets).Contains("Game.targets"))
                {
                    Log.LogMessage(MessageImportance.High, "📝 Generating RegisteredScripts.g.cs for the desktop app...");
                    Utils.GenerateRegisteredScripts(Log, opts.ProjectPath, Path.GetFileNameWithoutExtension(projectFile));
                }

                // Default the runtime identifier to the host OS/arch when --rid is omitted. Without this the
                // publish command emits an empty "-r  -c Release", where -r swallows -c as its value and the
                // dangling "Release" becomes a second project → MSB1008 / "Switch: Release". Android/iOS builders
                // derive their own RID from the architecture, so only the desktop path needs this fallback.
                if (string.IsNullOrWhiteSpace(opts.RID))
                {
                    opts.RID = GetDefaultDesktopRid();
                    Log.LogMessage(MessageImportance.Normal, $"No --rid specified; defaulting to host RID: {opts.RID}");
                }

                // Determine build configuration. --type release-harness = Release-OPTIMIZED codegen WITH the
                // in-app harness (the HARNESS define is added below); plain release = optimized, no harness.
                string buildType = (opts.Type == "release" || opts.Type == "release-harness") ? "Release" : "Debug";
                string targetFramework = string.IsNullOrEmpty(opts.Framework) ? "net10.0" : opts.Framework;

                // Determine build output path (always build to project bin folder)
                string buildOutputPath = Path.Combine(opts.ProjectPath, "bin", buildType, opts.RID);
                string outputPath = buildOutputPath;

                Log.LogMessage(MessageImportance.Normal, $"Build type: {buildType}");
                Log.LogMessage(MessageImportance.Normal, $"Target framework: {targetFramework}");
                Log.LogMessage(MessageImportance.Normal, $"Runtime ID: {opts.RID}");
                Log.LogMessage(MessageImportance.Normal, $"Output path: {outputPath}");

                // Ensure paths are absolute
                string absoluteProjectFile = Path.GetFullPath(projectFile);
                string absoluteOutputPath = Path.GetFullPath(outputPath);

                // ⛔ -p:DefineConstants is a GLOBAL property, so it REPLACES whatever the project computed
                // rather than adding to it — every <DefineConstants>$(DefineConstants);X</DefineConstants>
                // line in the .csproj is silently discarded for this publish. That means the platform
                // symbol the project would have defined (__MACOS__/__WINDOWS__/__LINUX__, see the app
                // templates) has to be re-supplied HERE, or a published desktop binary loses every
                // platform-gated P/Invoke — which is not a build error, just a method that compiles to
                // nothing. It cost us the whole macOS share sheet: the app ran, captured its screenshot
                // and called into a SharePlugin whose macOS branch had been compiled away.
                string desktopConstants = "_DESKTOP_PUBLISHED_BINARY_";
                string platformDefine = DesktopPlatformDefine(opts.RID);
                if (platformDefine.Length > 0) desktopConstants += "%3B" + platformDefine;   // never an empty entry
                if (opts.Type == "release-harness") desktopConstants += "%3BHARNESS";

                // Build project with dotnet publish
                string dotnetCommand = $"dotnet publish -f {targetFramework} \"{absoluteProjectFile}\" -r {opts.RID} -c {buildType} " +
                                     $"-p:PublishAot=true -p:TrimmerRemoveSymbols=false -p:TrimMode=partial " +
                                     $"-p:DisableUnsupportedError=true -p:PublishAotUsingRuntimePack=true " +
                                     $"-p:StripSymbols=true -p:DefineConstants=\"{desktopConstants}\" " +
                                     $"-o \"{absoluteOutputPath}\"" +
                                     ObfuscationInjection.BuildArgs(opts, opts.ProjectPath, buildType, Log);

                Log.LogMessage(MessageImportance.High, "📦 Publishing .NET project...");
                (int exitCode, string output) = Utils.RunShellCommand(
                    Log,
                    dotnetCommand,
                    new Dictionary<string, string>(),
                    workingDir: opts.ProjectPath,
                    logStdErrAsMessage: true,
                    debugMessageImportance: MessageImportance.High,
                    label: "dotnet-publish");

                if (exitCode != 0)
                {
                    Log.LogError("dotnet publish failed");
                    return false;
                }

                Log.LogMessage(MessageImportance.High, "✅ .NET project published successfully");

                // Copy Assets folder if it exists
                string assetsPath = Path.Combine(opts.ProjectPath, "Assets");
                if (Directory.Exists(assetsPath))
                {
                    Log.LogMessage(MessageImportance.Normal, "📁 Copying Assets folder...");
                    assetsPath.CopyDirectoryIfDifferent(outputPath, true);
                }
                // What a model is made from is not packed (the project's own copy step put it here too).
                int sourceOnly = Utils.RemoveSourceOnlyAssets(outputPath);
                if (sourceOnly > 0) Log.LogMessage(MessageImportance.High, $"📁 Left out {sourceOnly} source file(s) (.blend): the app reads the models exported beside them");

                // Process desktop icon if specified
                string? iconPath = ProcessDesktopIcon(opts.ProjectPath, outputPath);

                // Create macOS app bundle if on macOS
                bool isMacOS = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) && opts.RID.Contains("osx");
                if (isMacOS)
                {
                    CreateMacOSAppBundle(projectName, outputPath, iconPath);
                }

                // Always copy to output folder (project's output folder or custom path)
                CopyToOutputPath(projectName, outputPath, buildType, isMacOS);

                Log.LogMessage(MessageImportance.High, "✅ Desktop build completed successfully");
                Log.LogMessage(MessageImportance.High, $"📂 Output: {outputPath}");

                return true;
            }
            catch (Exception ex)
            {
                Log.LogError($"Desktop build failed: {ex.Message}");
                return false;
            }
        }

        // Portable RID for the current host (e.g. osx-arm64, win-x64, linux-x64). NativeAOT publish needs a
        // portable RID, so build it from OS + process architecture rather than RuntimeInformation.RuntimeIdentifier
        // (which can return version-specific RIDs like "osx.15-arm64" that the runtime pack won't resolve).
        private static string GetDefaultDesktopRid()
        {
            string os = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win"
                      : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "osx"
                      : "linux";

            string arch = RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.Arm64 => "arm64",
                Architecture.X64 => "x64",
                Architecture.X86 => "x86",
                Architecture.Arm => "arm",
                _ => "x64"
            };

            return $"{os}-{arch}";
        }

        /// <summary>The platform symbol a desktop project would have defined for itself, keyed off the
        /// publish RID. It has to be restated here because -p:DefineConstants REPLACES the project's own
        /// value (see the publish command above), and a lost platform symbol fails SILENTLY: the
        /// platform-gated code compiles to an empty method instead of erroring.</summary>
        private static string DesktopPlatformDefine(string rid)
            => rid.StartsWith("osx", StringComparison.OrdinalIgnoreCase)   ? "__MACOS__"
             : rid.StartsWith("win", StringComparison.OrdinalIgnoreCase)   ? "__WINDOWS__"
             : rid.StartsWith("linux", StringComparison.OrdinalIgnoreCase) ? "__LINUX__"
             : "";

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
                return string.Empty;
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
                    Log.LogMessage(MessageImportance.Normal, $"Using project matching folder name: {projectName}");
                    return projectName;
                }
            }

            // No match found, list available projects and use the first one as fallback
            Log.LogMessage(MessageImportance.Normal, $"No project matched parent folder name '{parentFolderName}'. Available projects:");
            foreach (string csprojFile in csprojFiles)
            {
                Log.LogMessage(MessageImportance.Normal, $"  - {Path.GetFileNameWithoutExtension(csprojFile)}");
            }

            string fallbackProject = Path.GetFileNameWithoutExtension(csprojFiles[0]);
            Log.LogMessage(MessageImportance.Normal, $"Using first project as fallback: {fallbackProject}");
            return fallbackProject;
        }

        private string? ProcessDesktopIcon(string projectPath, string outputPath)
        {
            try
            {
                string? desktopIcon = ReadDesktopIconFromDirectoryBuildProps(projectPath);
                
                if (string.IsNullOrEmpty(desktopIcon))
                {
                    Log.LogMessage(MessageImportance.Normal, "ℹ️  No DesktopIcon specified in Directory.Build.props");
                    return null;
                }

                string? sourceIconPath = FindIconFile(projectPath, desktopIcon);
                
                if (sourceIconPath == null)
                {
                    Log.LogWarning($"⚠️  Desktop icon not found: {desktopIcon}");
                    return null;
                }

                Log.LogMessage(MessageImportance.High, $"🖥️  Processing Desktop icon: {Path.GetFileName(sourceIconPath)}");

                // Determine platform from RID
                string platform = "unknown";
                if (opts.RID.Contains("win"))
                    platform = "windows";
                else if (opts.RID.Contains("osx") || opts.RID.Contains("mac"))
                    platform = "macos";
                else if (opts.RID.Contains("linux"))
                    platform = "linux";

                string? generatedIconPath = null;
                switch (platform)
                {
                    case "windows":
                        generatedIconPath = GenerateWindowsIcon(sourceIconPath, outputPath);
                        break;
                    case "macos":
                        generatedIconPath = GenerateMacOSIcon(sourceIconPath, outputPath);
                        break;
                    case "linux":
                        GenerateLinuxIcon(sourceIconPath, outputPath);
                        break;
                    default:
                        Log.LogMessage(MessageImportance.Normal, $"ℹ️  Unknown platform for RID: {opts.RID}, skipping icon generation");
                        break;
                }

                Log.LogMessage(MessageImportance.High, "✅ Desktop icon processed successfully");
                return generatedIconPath;
            }
            catch (Exception ex)
            {
                Log.LogWarning($"⚠️  Failed to process desktop icon: {ex.Message}");
                return null;
            }
        }

        private string? GenerateWindowsIcon(string sourceIconPath, string outputPath)
        {
            string icoPath = Path.Combine(outputPath, "app.ico");
            
            // Try to generate multi-size ICO using ImageMagick
            var sizes = new[] { 256, 128, 64, 48, 32, 16 };
            bool created = false;

            // Try ImageMagick 7+ with 'magick' command
            try
            {
                var sizeArgs = string.Join(" ", sizes.Select(s => $"-define icon:auto-resize={s}"));
                var magickResult = CliWrap.Cli.Wrap("magick")
                    .WithArguments($"convert \"{sourceIconPath}\" {sizeArgs} \"{icoPath}\"")
                    .WithValidation(CliWrap.CommandResultValidation.None)
                    .ExecuteAsync()
                    .GetAwaiter()
                    .GetResult();

                if (magickResult.ExitCode == 0)
                {
                    created = true;
                    Log.LogMessage(MessageImportance.Normal, $"   ✅ Created app.ico");
                }
            }
            catch { }

            if (!created)
            {
                // Fallback: Try 'convert' command
                try
                {
                    var convertResult = CliWrap.Cli.Wrap("convert")
                        .WithArguments($"\"{sourceIconPath}\" -define icon:auto-resize=256,128,64,48,32,16 \"{icoPath}\"")
                        .WithValidation(CliWrap.CommandResultValidation.None)
                        .ExecuteAsync()
                        .GetAwaiter()
                        .GetResult();

                    if (convertResult.ExitCode == 0)
                    {
                        created = true;
                        Log.LogMessage(MessageImportance.Normal, $"   ✅ Created app.ico");
                    }
                }
                catch { }
            }

            if (!created)
            {
                // Final fallback: Copy source file as .ico (won't be proper multi-size)
                File.Copy(sourceIconPath, icoPath, true);
                Log.LogWarning($"   ⚠️  ImageMagick not found. Copied source as app.ico (not multi-size)");
            }
            
            return File.Exists(icoPath) ? icoPath : null;
        }

        private string? GenerateMacOSIcon(string sourceIconPath, string outputPath)
        {
            string icnsPath = Path.Combine(outputPath, "app.icns");
            string iconsetPath = Path.Combine(outputPath, "app.iconset");
            
            try
            {
                Directory.CreateDirectory(iconsetPath);

                // Generate all required icon sizes for macOS
                var sizes = new[] 
                {
                    (16, "icon_16x16.png"),
                    (32, "icon_16x16@2x.png"),
                    (32, "icon_32x32.png"),
                    (64, "icon_32x32@2x.png"),
                    (128, "icon_128x128.png"),
                    (256, "icon_128x128@2x.png"),
                    (256, "icon_256x256.png"),
                    (512, "icon_256x256@2x.png"),
                    (512, "icon_512x512.png"),
                    (1024, "icon_512x512@2x.png")
                };

                foreach (var (size, filename) in sizes)
                {
                    string iconPath = Path.Combine(iconsetPath, filename);
                    if (ResizeImageForDesktop(sourceIconPath, iconPath, size))
                    {
                        Log.LogMessage(MessageImportance.Normal, $"   ✅ Created {filename} ({size}x{size})");
                    }
                }

                // Convert iconset to icns using iconutil (macOS only)
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    try
                    {
                        var iconutilResult = CliWrap.Cli.Wrap("iconutil")
                            .WithArguments($"-c icns \"{iconsetPath}\" -o \"{icnsPath}\"")
                            .WithValidation(CliWrap.CommandResultValidation.None)
                            .ExecuteAsync()
                            .GetAwaiter()
                            .GetResult();

                        if (iconutilResult.ExitCode == 0)
                        {
                            Log.LogMessage(MessageImportance.Normal, $"   ✅ Created app.icns");
                            // Clean up iconset directory
                            Directory.Delete(iconsetPath, true);
                        }
                        else
                        {
                            Log.LogWarning($"   ⚠️  Failed to create .icns file. Individual icon files available in {iconsetPath}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.LogWarning($"   ⚠️  iconutil failed: {ex.Message}. Individual icon files available in {iconsetPath}");
                    }
                }
                else
                {
                    Log.LogMessage(MessageImportance.Normal, $"   ℹ️  iconutil not available (macOS only). Individual icon files created in {iconsetPath}");
                }
                
                return File.Exists(icnsPath) ? icnsPath : null;
            }
            catch (Exception ex)
            {
                Log.LogWarning($"   ⚠️  Failed to generate macOS icon: {ex.Message}");
                return null;
            }
        }

        private void GenerateLinuxIcon(string sourceIconPath, string outputPath)
        {
            // Generate standard Linux icon sizes
            var sizes = new[] { 16, 22, 24, 32, 48, 64, 128, 256, 512 };

            foreach (int size in sizes)
            {
                string iconPath = Path.Combine(outputPath, $"icon_{size}.png");
                if (ResizeImageForDesktop(sourceIconPath, iconPath, size))
                {
                    Log.LogMessage(MessageImportance.Normal, $"   ✅ Created icon_{size}.png ({size}x{size})");
                }
            }
        }

        private bool ResizeImageForDesktop(string sourceIcon, string outputPath, int size)
        {
            // First choice: Use SkiaSharp (pure C# - always available, cross-platform, high quality)
            try
            {
                if (ResizeImageWithSkiaSharp(sourceIcon, outputPath, size))
                    return true;
            }
            catch (Exception ex)
            {
                Log.LogWarning($"   ⚠️  SkiaSharp resizing failed: {ex.Message}");
            }

            // Fallback: Try ImageMagick 7+ with 'magick' command
            try
            {
                // Use -resize with ^ to fill, then -gravity center -extent to crop to exact size
                var magickResult = CliWrap.Cli.Wrap("magick")
                    .WithArguments($"convert \"{sourceIcon}\" -resize {size}x{size}^ -gravity center -extent {size}x{size} \"{outputPath}\"")
                    .WithValidation(CliWrap.CommandResultValidation.None)
                    .ExecuteAsync()
                    .GetAwaiter()
                    .GetResult();

                if (magickResult.ExitCode == 0)
                    return true;
            }
            catch { }

            // Fallback: Try ImageMagick 6 with 'convert' command
            try
            {
                var convertResult = CliWrap.Cli.Wrap("convert")
                    .WithArguments($"\"{sourceIcon}\" -resize {size}x{size}^ -gravity center -extent {size}x{size} \"{outputPath}\"")
                    .WithValidation(CliWrap.CommandResultValidation.None)
                    .ExecuteAsync()
                    .GetAwaiter()
                    .GetResult();

                if (convertResult.ExitCode == 0)
                    return true;
            }
            catch { }

            // Fallback: Try sips (macOS only)
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                try
                {
                    var sipsResult = CliWrap.Cli.Wrap("sips")
                        .WithArguments($"-z {size} {size} \"{sourceIcon}\" --out \"{outputPath}\"")
                        .WithValidation(CliWrap.CommandResultValidation.None)
                        .ExecuteAsync()
                        .GetAwaiter()
                        .GetResult();

                    if (sipsResult.ExitCode == 0)
                        return true;
                }
                catch { }
            }

            // Final fallback: Copy original
            File.Copy(sourceIcon, outputPath, true);
            Log.LogWarning($"   ⚠️  All image resizing methods failed. Copied original for {Path.GetFileName(outputPath)}");
            return true;
        }

        private bool ResizeImageWithSkiaSharp(string sourceIcon, string outputPath, int size)
        {
            // Load the source image
            using var inputStream = File.OpenRead(sourceIcon);
            using var original = SKBitmap.Decode(inputStream);
            
            if (original == null)
            {
                Log.LogWarning($"   ⚠️  Failed to decode image: {sourceIcon}");
                return false;
            }

            // Calculate dimensions to maintain aspect ratio and fill the target size
            int srcWidth = original.Width;
            int srcHeight = original.Height;
            float srcAspect = (float)srcWidth / srcHeight;
            float targetAspect = 1.0f; // Square target

            int cropWidth, cropHeight, cropX, cropY;
            
            if (srcAspect > targetAspect)
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
            using var cropped = new SKBitmap(cropWidth, cropHeight);
            using var canvas = new SKCanvas(cropped);
            
            var srcRect = new SKRect(cropX, cropY, cropX + cropWidth, cropY + cropHeight);
            var destRect = new SKRect(0, 0, cropWidth, cropHeight);
            
            canvas.DrawBitmap(original, srcRect, destRect, new SKPaint
            {
                IsAntialias = true
            });

            // Resize to target size with high-quality sampling
            var imageInfo = new SKImageInfo(size, size, SKColorType.Rgba8888, SKAlphaType.Premul);
            var samplingOptions = new SKSamplingOptions(SKCubicResampler.CatmullRom);
            using var resized = cropped.Resize(imageInfo, samplingOptions);
            
            if (resized == null)
            {
                Log.LogWarning($"   ⚠️  Failed to resize image to {size}x{size}");
                return false;
            }

            // Save as PNG
            using var image = SKImage.FromBitmap(resized);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var outputStream = File.OpenWrite(outputPath);
            data.SaveTo(outputStream);

            return true;
        }

        private string? ReadDesktopIconFromDirectoryBuildProps(string projectPath)
        {
            string directoryBuildPropsPath = Path.Combine(projectPath, "Directory.Build.props");
            if (!File.Exists(directoryBuildPropsPath))
                return null;

            try
            {
                var doc = System.Xml.Linq.XDocument.Load(directoryBuildPropsPath);
                var desktopIcon = doc.Descendants("DesktopIcon").FirstOrDefault()?.Value;
                return desktopIcon;
            }
            catch
            {
                return null;
            }
        }

        private string ReadAppVersionFromDirectoryBuildProps(string projectPath)
        {
            string directoryBuildPropsPath = Path.Combine(projectPath, "Directory.Build.props");
            if (!File.Exists(directoryBuildPropsPath))
                return "1.0";

            try
            {
                var doc = System.Xml.Linq.XDocument.Load(directoryBuildPropsPath);
                var appVersion = doc.Descendants("AppVersion").FirstOrDefault()?.Value;
                return !string.IsNullOrEmpty(appVersion) ? appVersion : "1.0";
            }
            catch
            {
                return "1.0";
            }
        }

        private string? FindIconFile(string projectPath, string iconPath)
        {
            // Try absolute path
            if (Path.IsPathRooted(iconPath) && File.Exists(iconPath))
                return iconPath;

            // Try Assets folder
            string assetsPath = Path.Combine(projectPath, "Assets", iconPath);
            if (File.Exists(assetsPath))
                return assetsPath;

            // Try relative to project
            string relativePath = Path.Combine(projectPath, iconPath);
            if (File.Exists(relativePath))
                return relativePath;

            return null;
        }

        private void CreateMacOSAppBundle(string projectName, string outputPath, string? iconPath)
        {
            try
            {
                Log.LogMessage(MessageImportance.High, "📦 Creating macOS .app bundle...");

                // Create .app bundle structure
                string appBundlePath = Path.Combine(outputPath, $"{projectName}.app");
                string contentsPath = Path.Combine(appBundlePath, "Contents");
                string macOSPath = Path.Combine(contentsPath, "MacOS");
                string resourcesPath = Path.Combine(contentsPath, "Resources");

                Directory.CreateDirectory(macOSPath);
                Directory.CreateDirectory(resourcesPath);

                // Move executable to MacOS folder
                string executablePath = Path.Combine(outputPath, projectName);
                string newExecutablePath = Path.Combine(macOSPath, projectName);
                
                if (File.Exists(executablePath))
                {
                    File.Move(executablePath, newExecutablePath, true);
                    Log.LogMessage(MessageImportance.Normal, $"   ✅ Moved executable to bundle");
                }

                // Move dylib files to MacOS folder
                foreach (string dylibFile in Directory.GetFiles(outputPath, "*.dylib"))
                {
                    string fileName = Path.GetFileName(dylibFile);
                    string destPath = Path.Combine(macOSPath, fileName);
                    File.Move(dylibFile, destPath, true);
                    Log.LogMessage(MessageImportance.Normal, $"   ✅ Moved {fileName} to bundle");
                }

                // Copy icon to Resources folder if available
                string? bundleIconFileName = null;
                if (!string.IsNullOrEmpty(iconPath) && File.Exists(iconPath))
                {
                    bundleIconFileName = Path.GetFileName(iconPath);
                    string resourceIconPath = Path.Combine(resourcesPath, bundleIconFileName);
                    File.Copy(iconPath, resourceIconPath, true);
                    
                    // Clean up original icon file
                    File.Delete(iconPath);
                    Log.LogMessage(MessageImportance.Normal, $"   ✅ Added icon to Resources");
                }

                // Move all files from outputPath to Resources (preserving directory structure)
                // This ensures all assets are available in the app bundle
                foreach (string file in Directory.GetFiles(outputPath, "*", SearchOption.AllDirectories))
                {
                    string relativePath = Path.GetRelativePath(outputPath, file);
                    
                    // Skip files that are already inside .app or .dSYM bundles
                    if (relativePath.Contains(".app") || relativePath.Contains(".dSYM"))
                    {
                        continue;
                    }
                    
                    string destPath = Path.Combine(resourcesPath, relativePath);
                    
                    // Create directory if needed
                    string? destDir = Path.GetDirectoryName(destPath);
                    if (!string.IsNullOrEmpty(destDir))
                    {
                        Directory.CreateDirectory(destDir);
                    }
                    
                    File.Move(file, destPath, true);
                }
                
                // Clean up any remaining subdirectories in outputPath (except the .app bundle)
                foreach (string dir in Directory.GetDirectories(outputPath))
                {
                    string dirName = Path.GetFileName(dir);
                    // Skip .app bundle and .dSYM directories
                    if (!dirName.EndsWith(".app") && !dirName.EndsWith(".dSYM"))
                    {
                        try
                        {
                            Directory.Delete(dir, true);
                        }
                        catch
                        {
                            // Ignore cleanup errors
                        }
                    }
                }
                
                // Clean up any nested .app bundles inside the Resources folder
                // (sometimes created by dotnet publish)
                foreach (string dir in Directory.GetDirectories(resourcesPath, "*.app", SearchOption.AllDirectories))
                {
                    try
                    {
                        Directory.Delete(dir, true);
                        Log.LogMessage(MessageImportance.Normal, $"   ✅ Removed nested .app bundle from Resources");
                    }
                    catch
                    {
                        // Ignore cleanup errors
                    }
                }

                // Create Info.plist
                string infoPlistPath = Path.Combine(contentsPath, "Info.plist");
                
                // Read app version from Directory.Build.props
                string appVersion = ReadAppVersionFromDirectoryBuildProps(opts.ProjectPath);
                string versionCode = appVersion.Split('.')[0];
                
                string infoPlist = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<!DOCTYPE plist PUBLIC ""-//Apple//DTD PLIST 1.0//EN"" ""http://www.apple.com/DTDs/PropertyList-1.0.dtd"">
<plist version=""1.0"">
<dict>
    <key>CFBundleExecutable</key>
    <string>{projectName}</string>
    <key>CFBundleIdentifier</key>
    <string>com.sokol.{projectName.ToLower()}</string>
    <key>CFBundleName</key>
    <string>{projectName}</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleShortVersionString</key>
    <string>{appVersion}</string>
    <key>CFBundleVersion</key>
    <string>{versionCode}</string>";

                if (!string.IsNullOrEmpty(bundleIconFileName))
                {
                    infoPlist += $@"
    <key>CFBundleIconFile</key>
    <string>{bundleIconFileName}</string>";
                }

                infoPlist += @"
    <key>LSMinimumSystemVersion</key>
    <string>10.13</string>
    <key>NSHighResolutionCapable</key>
    <true/>
</dict>
</plist>";

                File.WriteAllText(infoPlistPath, infoPlist);
                Log.LogMessage(MessageImportance.Normal, $"   ✅ Created Info.plist");

                Log.LogMessage(MessageImportance.High, $"✅ macOS app bundle created: {projectName}.app");
            }
            catch (Exception ex)
            {
                Log.LogWarning($"⚠️  Failed to create macOS app bundle: {ex.Message}");
            }
        }

        private void CopyToOutputPath(string projectName, string buildOutputPath, string buildType, bool isMacOS)
        {
            try
            {
                // Determine platform name from RID
                string platformName = "Desktop";
                if (opts.RID.Contains("osx"))
                    platformName = "macOS";
                else if (opts.RID.Contains("win"))
                    platformName = "Windows";
                else if (opts.RID.Contains("linux"))
                    platformName = "Linux";

                // Determine output base path: use custom path if specified, otherwise use project's output folder
                string outputBasePath = string.IsNullOrEmpty(opts.OutputPath) 
                    ? Path.Combine(opts.ProjectPath, "output") 
                    : opts.OutputPath;

                // Create output directory: {basePath}/Desktop/{Platform}/{buildType}/
                string outputDir = Path.Combine(outputBasePath, "Desktop", platformName, buildType.ToLower());
                Directory.CreateDirectory(outputDir);

                if (isMacOS)
                {
                    // Copy .app bundle for macOS
                    string appBundlePath = Path.Combine(buildOutputPath, $"{projectName}.app");
                    if (Directory.Exists(appBundlePath))
                    {
                        string outputAppBundle = Path.Combine(outputDir, $"{projectName}.app");
                        
                        // Remove existing bundle if present
                        if (Directory.Exists(outputAppBundle))
                        {
                            Directory.Delete(outputAppBundle, true);
                        }

                        // Copy the entire .app bundle directory
                        CopyDirectory(appBundlePath, outputAppBundle);
                        Log.LogMessage(MessageImportance.High, $"✅ Desktop app bundle copied to: {outputAppBundle}");
                    }
                    else
                    {
                        Log.LogWarning($"App bundle not found at {appBundlePath}, skipping output copy.");
                    }
                }
                else
                {
                    // Copy executable and dependencies for Windows/Linux
                    string executableName = projectName;
                    if (opts.RID.Contains("win"))
                        executableName += ".exe";

                    string executablePath = Path.Combine(buildOutputPath, executableName);
                    if (File.Exists(executablePath))
                    {
                        // Copy executable
                        string outputExecutable = Path.Combine(outputDir, executableName);
                        File.Copy(executablePath, outputExecutable, true);

                        // Copy all .dll files
                        foreach (string dllFile in Directory.GetFiles(buildOutputPath, "*.dll"))
                        {
                            string fileName = Path.GetFileName(dllFile);
                            File.Copy(dllFile, Path.Combine(outputDir, fileName), true);
                        }

                        // Copy all .so files (Linux)
                        foreach (string soFile in Directory.GetFiles(buildOutputPath, "*.so"))
                        {
                            string fileName = Path.GetFileName(soFile);
                            File.Copy(soFile, Path.Combine(outputDir, fileName), true);
                        }

                        // Copy Assets folder if it exists
                        string assetsPath = Path.Combine(buildOutputPath, "Assets");
                        if (Directory.Exists(assetsPath))
                        {
                            string outputAssetsPath = Path.Combine(outputDir, "Assets");
                            CopyDirectory(assetsPath, outputAssetsPath);
                        }

                        Log.LogMessage(MessageImportance.High, $"✅ Desktop executable copied to: {outputExecutable}");
                    }
                    else
                    {
                        Log.LogWarning($"Executable not found at {executablePath}, skipping output copy.");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.LogWarning($"Failed to copy desktop build to output path: {ex.Message}");
            }
        }

        private void CopyDirectory(string sourceDir, string destDir)
        {
            // Create destination directory
            Directory.CreateDirectory(destDir);

            // Copy all files
            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string destFile = Path.Combine(destDir, Path.GetFileName(file));
                File.Copy(file, destFile, true);
            }

            // Copy all subdirectories
            foreach (string subdir in Directory.GetDirectories(sourceDir))
            {
                string destSubDir = Path.Combine(destDir, Path.GetFileName(subdir));
                CopyDirectory(subdir, destSubDir);
            }
        }
    }

}