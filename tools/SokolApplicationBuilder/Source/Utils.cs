
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// Modified by Eli Aloni (A.K.A. elix22)

using System;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Locator;  // Add this
namespace SokolApplicationBuilder
{
    public class ReferenceInfo
    {
        public string Name { get; set; }
        public string HintPath { get; set; }
    };

    public class PackageReferenceInfo
    {
        public string Name { get; set; }
        public string Version { get; set; }
    };

    public static class Utils
    {
        private static readonly object s_SyncObj = new object();

        const string RedColorText = "\u001b[31m";
        const string GreenColorText = "\u001b[32m";
        const string YellowColorText = "\u001b[33m";
        const string BlueColorText = "\u001b[34m";
        const string PurpleColorText = "\u001b[35m";
        const string CyanColorText = "\u001b[36m";

        static string URHONET_HOME_PATH = string.Empty;

        public static Options? opts;

        /// <summary>
        /// Delete the .NET SDK resolver backup temp files a wasm/browser build leaves next to the csproj
        /// (<c>&lt;proj&gt;.SdkResolver.&lt;hash&gt;.proj.Backup.tmp</c>). The SDK writes one per build/restore
        /// and never cleans them up, so they pile up on disk. Cross-platform (managed File I/O, no shell —
        /// a shell <c>rm</c> would not run on Windows). Best-effort; never throws.
        /// </summary>
        public static void CleanupSdkResolverTempFiles(string dir)
        {
            try
            {
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;
                foreach (string f in Directory.EnumerateFiles(dir))
                {
                    string name = Path.GetFileName(f);
                    if (name.Contains(".SdkResolver.") && name.EndsWith(".proj.Backup.tmp", StringComparison.OrdinalIgnoreCase))
                        try { File.Delete(f); } catch { }
                }
            }
            catch { }
        }

        public static string GetUrhoNetHomePath()
        {

            if (URHONET_HOME_PATH != string.Empty) return URHONET_HOME_PATH;

            string homeFolder = Utils.GetHomeFolder();
            string urhoNetConfigFolderPath = Path.Combine(homeFolder, ".urhonet_config");

            if (!Directory.Exists(urhoNetConfigFolderPath))
            {
                Console.WriteLine($".urhonet_config folder not found");
                return string.Empty;
            }

            string urhoNetHomeFilePath = Path.Combine(urhoNetConfigFolderPath, "urhonethome");
            if (!File.Exists(urhoNetHomeFilePath))
            {
                Console.WriteLine($"urhonethome not found");
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

        public static string GetHomeFolder()
        {
            return System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
        }

        public static string GetSokolNetHome()
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
            return Path.GetFullPath(Path.Combine(opts?.ProjectPath ?? "", "..", "..", ".."));
        }
        public static string FixPathString(this string path)
        {
            path = path.Replace("\\", "/");
            path = path.Replace("//", "/");
            return path;
        }

        public static string[] FileReadAllLines(this string path)
        {
            List<string> lines = new List<string>();
            try
            {
                using (FileStream fs = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    using var sr = new StreamReader(fs, Encoding.UTF8);
                    string? line = String.Empty;

                    while ((line = sr.ReadLine()) != null)
                    {
                        lines.Add(line);
                    }
                }
            }
            catch (Exception ex)
            {
                // Console.WriteLine(ex.ToString());
                Console.WriteLine(ex.ToString());
            }

            return lines.ToArray();
        }

        public static (int exitCode, string output) RunShellCommand(
                                                TaskLoggingHelper? logger,
                                                string command,
                                                IDictionary<string, string>? envVars,
                                                string workingDir,
                                                bool silent = false,
                                                bool logStdErrAsMessage = false,
                                                MessageImportance debugMessageImportance = MessageImportance.Low,
                                                string? label = null)
        {
            string scriptFileName = CreateTemporaryBatchFile(command);
            (string shell, string args) = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                                                        ? ("cmd", $"/c \"{scriptFileName}\"")
                                                        : ("/bin/sh", $"\"{scriptFileName}\"");

            string msgPrefix = label == null ? string.Empty : $"[{label}] ";
            logger?.LogMessage(debugMessageImportance, $"{msgPrefix}Running {command} via script {scriptFileName}:", msgPrefix);
            logger?.LogMessage(debugMessageImportance, File.ReadAllText(scriptFileName), msgPrefix);

            return TryRunProcess(logger,
                                 shell,
                                 args,
                                 envVars,
                                 workingDir,
                                 silent: silent,
                                 logStdErrAsMessage: logStdErrAsMessage,
                                 label: label,
                                 debugMessageImportance: debugMessageImportance);

            static string CreateTemporaryBatchFile(string command)
            {
                string extn = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".cmd" : ".sh";
                string file = Path.Combine(Path.GetTempPath(), $"tmp{Guid.NewGuid():N}{extn}");

                using StreamWriter sw = new(file);
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    sw.WriteLine("setlocal");
                    sw.WriteLine("set errorlevel=dummy");
                    sw.WriteLine("set errorlevel=");
                }
                else
                {
                    // Use sh rather than bash, as not all 'nix systems necessarily have Bash installed
                    sw.WriteLine("#!/bin/sh");
                }

                sw.WriteLine(command);
                return file;
            }
        }


        public static string RunProcess(
            TaskLoggingHelper logger,
            string path,
            string args = "",
            IDictionary<string, string>? envVars = null,
            string? workingDir = null,
            bool ignoreErrors = false,
            bool silent = true,
            MessageImportance debugMessageImportance = MessageImportance.High)
        {
            (int exitCode, string output) = TryRunProcess(
                                                logger,
                                                path,
                                                args,
                                                envVars,
                                                workingDir,
                                                silent: silent,
                                                debugMessageImportance: debugMessageImportance);

            if (exitCode != 0 && !ignoreErrors)
                throw new Exception("Error: Process returned non-zero exit code: " + output);

            return output;
        }

        public static (int, string) TryRunProcess(
               TaskLoggingHelper? logger,
               string path,
               string args = "",
               IDictionary<string, string>? envVars = null,
               string? workingDir = null,
               bool silent = true,
               bool logStdErrAsMessage = false,
               MessageImportance debugMessageImportance = MessageImportance.High,
               string? label = null)
        {
            string msgPrefix = label == null ? string.Empty : $"[{label}] ";
            logger?.LogMessage(debugMessageImportance, $"{msgPrefix}Running: {path} {args}");
            var outputBuilder = new StringBuilder();
            var processStartInfo = new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                Arguments = args,
            };

            if (workingDir != null)
                processStartInfo.WorkingDirectory = workingDir;

            logger?.LogMessage(debugMessageImportance, $"{msgPrefix}Using working directory: {workingDir ?? Environment.CurrentDirectory}", msgPrefix);

            if (envVars != null)
            {
                //TBD ELI 
                // if (envVars.Count > 0)
                //     logger?.LogMessage(MessageImportance.Low, $"{msgPrefix}Setting environment variables for execution:", msgPrefix);

                foreach (KeyValuePair<string, string> envVar in envVars)
                {
                    processStartInfo.EnvironmentVariables[envVar.Key] = envVar.Value;
                    //TBD ELI logger?.LogMessage(MessageImportance.Low, $"{msgPrefix}\t{envVar.Key} = {envVar.Value}");
                }
            }

            Process? process = Process.Start(processStartInfo);
            if (process == null)
                throw new ArgumentException($"{msgPrefix}Process.Start({path} {args}) returned null process");

            process.ErrorDataReceived += (sender, e) =>
            {
                lock (s_SyncObj)
                {
                    if (string.IsNullOrEmpty(e.Data))
                        return;

                    string msg = $"{msgPrefix}{e.Data}";
                    if (!silent)
                    {
                        if (logStdErrAsMessage)
                            logger?.LogMessage(debugMessageImportance, e.Data, msgPrefix);
                        else
                            logger?.LogWarning(msg);
                    }
                    outputBuilder.AppendLine(e.Data);
                }
            };
            process.OutputDataReceived += (sender, e) =>
            {
                lock (s_SyncObj)
                {
                    if (string.IsNullOrEmpty(e.Data))
                        return;

                    if (!silent)
                        logger?.LogMessage(debugMessageImportance, e.Data, msgPrefix);
                    outputBuilder.AppendLine(e.Data);
                }
            };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();

            logger?.LogMessage(debugMessageImportance, $"{msgPrefix}Exit code: {process.ExitCode}");
            return (process.ExitCode, outputBuilder.ToString().Trim('\r', '\n'));
        }

        internal static string CreateTemporaryBatchFile(string command)
        {
            string extn = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".cmd" : ".sh";
            string file = Path.Combine(Path.GetTempPath(), $"tmp{Guid.NewGuid():N}{extn}");

            using StreamWriter sw = new(file);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                sw.WriteLine("setlocal");
                sw.WriteLine("set errorlevel=dummy");
                sw.WriteLine("set errorlevel=");
            }
            else
            {
                // Use sh rather than bash, as not all 'nix systems necessarily have Bash installed
                sw.WriteLine("#!/bin/sh");
            }

            sw.WriteLine(command);

            return file;
        }

        public static bool CopyIfDifferent(string src, string dst, bool useHash)
        {
            if (!File.Exists(src))
                throw new ArgumentException($"Cannot find {src} file to copy", nameof(src));

            bool areDifferent = !File.Exists(dst) ||
                                    (useHash && ComputeHash(src) != ComputeHash(dst)) ||
                                    (File.ReadAllText(src) != File.ReadAllText(dst));

            if (areDifferent)
                File.Copy(src, dst, true);

            return areDifferent;
        }

        public static string ComputeHash(string filepath)
        {
            using var stream = File.OpenRead(filepath);
            using HashAlgorithm hashAlgorithm = SHA512.Create();

            byte[] hash = hashAlgorithm.ComputeHash(stream);
            return Convert.ToBase64String(hash);
        }

        public static void DirectoryCopy(string sourceDir, string destDir, Func<string, bool>? predicate = null)
        {
            string[] files = Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories);
            foreach (string file in files)
            {
                if (predicate != null && !predicate(file))
                    continue;

                string relativePath = Path.GetRelativePath(sourceDir, file);
                string? relativeDir = Path.GetDirectoryName(relativePath);
                if (!string.IsNullOrEmpty(relativeDir))
                    Directory.CreateDirectory(Path.Combine(destDir, relativeDir));

                File.Copy(file, Path.Combine(destDir, relativePath), true);
            }
        }

        public static void CopyDirectory(this string sourceDir, string destinationDir, bool recursive = true)
        {
            // Get information about the source directory
            var dir = new DirectoryInfo(sourceDir);

            // Check if the source directory exists
            if (!dir.Exists)
                throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");

            // Cache directories before we start copying
            DirectoryInfo[] dirs = dir.GetDirectories();

            // Create the destination directory
            Directory.CreateDirectory(destinationDir);

            // Get the files in the source directory and copy to the destination directory
            foreach (FileInfo file in dir.GetFiles())
            {
                string targetFilePath = Path.Combine(destinationDir, file.Name);
                File.Delete(targetFilePath);
                file.CopyTo(targetFilePath, true);
            }

            // If recursive and copying subdirectories, recursively call this method
            if (recursive)
            {
                foreach (DirectoryInfo subDir in dirs)
                {
                    string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                    CopyDirectory(subDir.FullName, newDestinationDir, true);
                }
            }
        }


        /// <summary>
        /// A file an app does not read and a package does not carry: what a model is made from - a Blender file (.blend)
        /// and the backups Blender leaves beside it (.blend1, .blend2 ...). The app reads the model exported beside it
        /// (ship.blend.glb), which is packed.
        /// </summary>
        public static bool IsSourceOnlyAsset(string path)
        {
            string name = Path.GetFileName(path);
            int at = name.LastIndexOf(".blend", StringComparison.OrdinalIgnoreCase);
            if (at < 0) return false;
            for (int i = at + 6; i < name.Length; i++)
                if (name[i] < '0' || name[i] > '9') return false;
            return true;
        }

        /// <summary>
        /// Removes the source-only files (<see cref="IsSourceOnlyAsset"/>) from a folder that was packed for an app -
        /// never from a project's own Assets. Returns how many were removed.
        /// </summary>
        public static int RemoveSourceOnlyAssets(string packedDir)
        {
            if (!Directory.Exists(packedDir)) return 0;
            int removed = 0;
            foreach (string file in Directory.GetFiles(packedDir, "*.blend*", SearchOption.AllDirectories))
            {
                if (!IsSourceOnlyAsset(file)) continue;
                File.Delete(file);
                removed++;
            }
            return removed;
        }

        public static void CopyDirectoryIfDifferent(this string sourceDir, string destinationDir, bool recursive = true)
        {
            // Get information about the source directory
            var dir = new DirectoryInfo(sourceDir);

            // Check if the source directory exists
            if (!dir.Exists)
                throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");

            // Cache directories before we start copying
            DirectoryInfo[] dirs = dir.GetDirectories();

            // Create the destination directory
            Directory.CreateDirectory(destinationDir);

            // Get the files in the source directory and copy to the destination directory
            foreach (FileInfo file in dir.GetFiles())
            {
                string targetFilePath = Path.Combine(destinationDir, file.Name);
                CopyIfDifferent(file.FullName, targetFilePath, true);
            }

            // If recursive and copying subdirectories, recursively call this method
            if (recursive)
            {
                foreach (DirectoryInfo subDir in dirs)
                {
                    string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                    CopyDirectoryIfDifferent(subDir.FullName, newDestinationDir, true);
                }
            }
        }

        public static void DeleteDirectory(this string path)
        {
            if (!Directory.Exists(path)) return;

            foreach (string directory in Directory.GetDirectories(path))
            {
                DeleteDirectory(directory);
            }

            try
            {
                Directory.Delete(path, true);
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine(RedColorText + ex.ToString());
            }
            catch (PathTooLongException ex)
            {
                Console.WriteLine(RedColorText + ex.ToString());
            }
            catch (DirectoryNotFoundException ex)
            {
                Console.WriteLine(RedColorText + ex.ToString());
            }
            catch (IOException ex)
            {
                Directory.Delete(path, true);
                Console.WriteLine(RedColorText + ex.ToString());
            }
            catch (UnauthorizedAccessException ex)
            {
                Directory.Delete(path, true);
                Console.WriteLine(RedColorText + ex.ToString());
            }

        }


        public static void ReplaceInfile(this string inFileName, string oldstr, string newStr)
        {
            if (File.Exists(inFileName) == false) return;

            try
            {
                string allText = File.ReadAllText(inFileName);
                string replacedText = allText.Replace(oldstr, newStr);

                File.Delete(inFileName);
                File.WriteAllText(inFileName, replacedText);
            }
            catch (Exception e)
            {
                Console.WriteLine(RedColorText + e.ToString());
            }
        }

        public static void DeleteFile(this string path)
        {
            if (File.Exists(path) == false) return;

            try
            {
                File.Delete(path);
            }
            catch (Exception e)
            {
                Console.WriteLine(RedColorText + e.ToString());
            }
        }

        public static void AppendText(this string path, string content)
        {
            var writer = File.AppendText(path);
            writer.Write(content);
            writer.Dispose();
        }

        public static void AppendTextLine(this string path, string content)
        {
            var writer = File.AppendText(path);
            writer.WriteLine(content);
            writer.Dispose();
        }

        public static string GetEmbeddedResource(string file)
        {
            using Stream stream = typeof(Utils).Assembly
                .GetManifestResourceStream($"{typeof(Utils).Assembly.GetName().Name}.Templates.{file}")!;
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        public static void ResolveReferenceAssemblies(string assemblyFullPath, string[] assemblySearchPaths, out List<string> assembliesList, out List<string> assembliesFullPathList)
        {
            assembliesList = new List<string>();
            assembliesFullPathList = new List<string>();

            try
            {

                string[] assemblies = assemblyFullPath.GetReferencedAssemblies();
                if (assemblies.Length > 0)
                {
                    foreach (var assemblyName in assemblies)
                    {
                        if (assemblyName != string.Empty)
                        {
                            string refAssemblyFullPath = getAssemblyFullPath(assemblyName, assemblySearchPaths);
                            if (refAssemblyFullPath != string.Empty)
                            {
                                String? name = assembliesList.FindLast(item => item.Equals(assemblyName));
                                if (name == null)
                                {
                                    assembliesList.Add(assemblyName);
                                    assembliesFullPathList.Add(refAssemblyFullPath);
                                    resolveReferenceAssemblies(assemblyName, assemblySearchPaths, ref assembliesList, ref assembliesFullPathList);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(RedColorText + e.ToString());
            }
        }

        static void resolveReferenceAssemblies(String assemblyName, string[] assemblySearchPaths, ref List<string> assembliesList, ref List<string> assembliesFullPathList)
        {

            if (assemblyName == "Game") return;

            string assemblyFullPath = getAssemblyFullPath(assemblyName, assemblySearchPaths);

            if (assemblyFullPath != string.Empty && File.Exists(assemblyFullPath))
            {
                if (assembliesFullPathList.FindLast(item => item.Equals(assemblyFullPath)) == null)
                    assembliesFullPathList.Add(assemblyFullPath);

                try
                {

                    var assemblies = assemblyFullPath.GetReferencedAssemblies();
                    if (assemblies.Length > 0)
                    {
                        foreach (var refAssembly in assemblies)
                        {
                            if (refAssembly != string.Empty)
                            {
                                string refAssemblyFullPath = getAssemblyFullPath(refAssembly, assemblySearchPaths);

                                if (refAssemblyFullPath != string.Empty)
                                {
                                    string? name = assembliesList.FindLast(item => item.Equals(refAssembly));
                                    if (name == null)
                                    {
                                        assembliesList.Add(refAssembly);
                                        resolveReferenceAssemblies(refAssembly, assemblySearchPaths, ref assembliesList, ref assembliesFullPathList);
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
        }


        static bool referenceExist(AssemblyName reference, string[] assemblySearchPaths)
        {
            bool res = false;

            foreach (var path in assemblySearchPaths)
            {
                string assemblyFullPath = Path.Combine(path, (reference.Name == null) ? "" : reference.Name) + ".dll";
                if (File.Exists(assemblyFullPath))
                {
                    res = true;
                    break;
                }
            }

            return res;
        }

        static string getAssemblyFullPath(AssemblyName reference, string[] assemblySearchPaths)
        {
            if (reference.Name == null) return string.Empty;

            string res = string.Empty;
            String assemblyName = reference.Name;
            bool hasExtention = reference.Name.EndsWith(".dll") || reference.Name.EndsWith(".exe");

            if (hasExtention == false) assemblyName += ".dll";

            foreach (var path in assemblySearchPaths)
            {
                string assemblyFullPath = Path.Combine(path, assemblyName);
                if (File.Exists(assemblyFullPath))
                {
                    res = assemblyFullPath;
                    break;
                }
            }

            return res;
        }

        static string getAssemblyFullPath(string reference, string[] assemblySearchPaths)
        {
            string res = string.Empty;
            String assemblyName = reference;
            bool hasExtention = reference.EndsWith(".dll") || reference.EndsWith(".exe");

            if (hasExtention == false) assemblyName += ".dll";

            foreach (var path in assemblySearchPaths)
            {
                string assemblyFullPath = Path.Combine(path, assemblyName);
                if (File.Exists(assemblyFullPath))
                {
                    res = assemblyFullPath;
                    break;
                }
            }

            return res;
        }

        public static string[] GetReferencedAssemblies(this string assembly)
        {

            bool isWindows = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            bool isOSX = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
            bool isLinux = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
            string os_folder = "";
            if (isWindows)
            {
                os_folder = "windows";
            }
            else if (isOSX)
            {
                string architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString();
                if (architecture == "Arm64")
                {
                    os_folder = "macos/arm64";

                }
                else if (architecture == "X64")
                {
                    os_folder = "macos/x86_64";
                }

            }
            else
            {
                os_folder = "linux";
            }



            string monodis_exe = Path.Combine(GetUrhoNetHomePath(), "tools", "monodis", os_folder, "monodis");


            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                monodis_exe += ".exe";
            }

            monodis_exe = monodis_exe.Replace("\\", "/");

            // Console.WriteLine($"monodis_exe {monodis_exe}");

            var escapedArgs = assembly.Replace("\"", "\\\"");

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = monodis_exe,
                Arguments = "--assemblyref " + assembly,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            //TBD ELI, Set temporary MONO_PATH environment variable specifically for startInfo to find mscorlib.dll , 
            //it's needed in case the Mono sdk is not installed.
            // it's a temporary hack untill the new build process implementation will be ready.
            string mscorlib_path = Path.Combine(GetUrhoNetHomePath(), "tools", "monodis", "lib", "mono", "4.5");
            startInfo.Environment["MONO_PATH"] = mscorlib_path;

            var process = Process.Start(startInfo);

            List<string> reference_assemblies = new List<string>();

            if (process == null) return reference_assemblies.ToArray();

            process.Start();
            string result = process.StandardOutput.ReadToEnd();
            process.WaitForExit();


            string[] entries = result.Split("\n");
            foreach (string entry in entries)
            {
                if (entry.Contains("Name="))
                {
                    String name = entry.Replace("Name=", "").Replace("\r", "").Replace("\t", "");
                    name = name.Trim();
                    reference_assemblies.Add(name);
                }
            }



            return reference_assemblies.ToArray();
        }


        public static void RegisterMSBuild()
        {
            // Only register if not already registered
            if (!MSBuildLocator.IsRegistered)
            {
                var instances = MSBuildLocator.QueryVisualStudioInstances().ToArray();
                if (instances.Length > 0)
                {
                    MSBuildLocator.RegisterInstance(instances.OrderByDescending(x => x.Version).First());
                }
                else
                {
                    Console.WriteLine("No MSBuild instances found !!");
                }
            }
        }

        public static void GetProjectReferences(string csprojPath, ref List<ReferenceInfo> referencesList)
        {
            // Create a project collection
            using (var pc = new ProjectCollection())
            {
                // Load the project
                var project = pc.LoadProject(csprojPath);

                // Get all Reference items
                var references = project.GetItems("Reference");

                // Output the results
                foreach (var reference in references)
                {
                    string name = reference.EvaluatedInclude;
                    if (name == "UrhoDotNet" || name.Contains("UrhoDotNet")) continue;
                    string hintPath = reference.GetMetadataValue("HintPath");
                    referencesList.Add(new ReferenceInfo { Name = name, HintPath = hintPath });
                    Console.WriteLine($"Reference: {name}, HintPath: {hintPath}");
                }
            }
        }

        public static void GetProjectPackageReferences(string csprojPath, ref List<PackageReferenceInfo> packageReferenceInfos)
        {
            using (var pc = new ProjectCollection())
            {
                var project = pc.LoadProject(csprojPath);

                var packageReferences = project.GetItems("PackageReference");

                foreach (var package in packageReferences)
                {
                    string name = package.EvaluatedInclude;
                    string version = package.GetMetadataValue("Version");
                    packageReferenceInfos.Add(new PackageReferenceInfo { Name = name, Version = version });
                    Console.WriteLine($"Package: {name}, Version: {version}");
                }
            }
        }


        public static void AppendReferencesAndPackageReferencesToProject(string csprojPath, List<ReferenceInfo> referencesList, List<PackageReferenceInfo> packageReferenceInfos)
        {
            using (var pc = new ProjectCollection())
            {
                var project = pc.LoadProject(csprojPath);
                foreach (var reference in referencesList)
                {
                    var item = project.AddItem("Reference", reference.Name);
                    item.First().SetMetadataValue("HintPath", reference.HintPath);
                }
                foreach (var packageReference in packageReferenceInfos)
                {
                    var item = project.AddItem("PackageReference", packageReference.Name);
                    item.First().SetMetadataValue("Version", packageReference.Version);
                }
                project.Save();
            }
        }


        public static void AppendReferencesToProject(string csprojPath, List<ReferenceInfo> referencesList)
        {
            using (var pc = new ProjectCollection())
            {
                var project = pc.LoadProject(csprojPath);
                foreach (var reference in referencesList)
                {
                    var item = project.AddItem("Reference", reference.Name);
                    item.First().SetMetadataValue("HintPath", reference.HintPath);
                }
                project.Save();
            }
        }

        public static void AppendPackageReferencesToProject(string csprojPath, ref List<PackageReferenceInfo> packageReferencesList)
        {
            using (var pc = new ProjectCollection())
            {
                var project = pc.LoadProject(csprojPath);
                foreach (var packageReference in packageReferencesList)
                {
                    var item = project.AddItem("PackageReference", packageReference.Name);
                    item.First().SetMetadataValue("Version", packageReference.Version);
                }
                project.Save();
            }
        }

        public static bool FindProjectInPath(string path, ref string projectPath)
        {
            string[] csprojFiles = Directory.GetFiles(path, "*.csproj");

            if (csprojFiles.Length == 0)
            {
                Console.WriteLine($"No .csproj files found in {path}");
                return false;
            }

            if (csprojFiles.Length == 1)
            {
                // Only one project found, use it
                projectPath = csprojFiles[0];
                Console.WriteLine($"Found single project: {Path.GetFileNameWithoutExtension(projectPath)}");
                return true;
            }

            // Multiple projects found, try to match with parent folder name
            string parentFolderName = Path.GetFileName(path);
            Console.WriteLine($"Found {csprojFiles.Length} projects, looking for match with parent folder: {parentFolderName}");

            foreach (string csprojFile in csprojFiles)
            {
                string projectName = Path.GetFileNameWithoutExtension(csprojFile);
                if (string.Equals(projectName, parentFolderName, StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"Matched project with parent folder name: {projectName}");
                    projectPath = csprojFile;
                    return true;
                }
            }

            // No match found, use the first one as fallback
            string fallbackProject = Path.GetFileNameWithoutExtension(csprojFiles[0]);
            Console.WriteLine($"No project matched parent folder name '{parentFolderName}'. Using first project as fallback: {fallbackProject}");
            projectPath = csprojFiles[0];
            return true;
        }

        public static void ParseEnvironmentVariables(this string project_vars_path , out Dictionary<string, string> envVarsDict)
        {
            string[] project_vars = project_vars_path.FileReadAllLines();
            envVarsDict = new Dictionary<string, string>();
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


        public static string GetEnvValue( this Dictionary<string, string> envVarsDict, string key)
        {
            string value = string.Empty;
            if (envVarsDict.TryGetValue(key, out var val))
            {
                value = val;
                value = value.Replace("\'", "");
            }
            return value.Trim();
        }

        /// <summary>
        /// Scans the project's JIT-built DLL for concrete GameBehaviour subclasses using
        /// PE metadata (no CLR loading). Emits Source/Generated/RegisteredScripts.g.cs
        /// with static RegisterType&lt;T&gt;() calls safe for NativeAOT trimming — each with a setter of the
        /// script's public fields in plain code (a switch on the field's name), so that a value of the scene
        /// file reaches its field in a game compiled ahead of time, where a field cannot be found by its name.
        /// A script may sit in a namespace or inside another class, and may derive from another script.
        /// Runs a `dotnet build` first if no DLL exists yet, or if the DLL has scripts' base assembly
        /// (GameEditor.Framework) among its references and a source file is newer than it: a script added
        /// since the last desktop build would otherwise not be registered.
        /// Returns true if the file was created or changed (caller should rebuild).
        /// </summary>
        public static bool GenerateRegisteredScripts(TaskLoggingHelper log, string projectPath, string projectName)
        {
            // Find the built DLL — search bin/ for the most recently written match.
            string binDir = Path.Combine(projectPath, "bin");
            string? dllPath = null;
            if (Directory.Exists(binDir))
            {
                dllPath = Directory.EnumerateFiles(binDir, $"{projectName}.dll", SearchOption.AllDirectories)
                    .Where(p => !p.Contains(Path.DirectorySeparatorChar + "publish" + Path.DirectorySeparatorChar))
                    .OrderByDescending(File.GetLastWriteTimeUtc)
                    .FirstOrDefault();
            }

            if (dllPath != null && ReferencesScriptFramework(dllPath) && NewestSourceWriteTimeUtc(projectPath) > File.GetLastWriteTimeUtc(dllPath))
            {
                log.LogMessage(MessageImportance.High, "Sources are newer than the built DLL — rebuilding it to scan for scripts...");
                dllPath = null;
            }

            if (dllPath == null)
            {
                // No current DLL — do a quick dotnet build to produce it.
                log.LogMessage(MessageImportance.High, "No current DLL found — running dotnet build to scan for scripts...");
                string projectFile = Path.GetFullPath(Path.Combine(projectPath, $"{projectName}.csproj"));
                (int exitCode, _) = RunShellCommand(
                    log,
                    $"dotnet build \"{projectFile}\"",
                    new Dictionary<string, string>(),
                    workingDir: projectPath,
                    logStdErrAsMessage: true,
                    debugMessageImportance: MessageImportance.Normal,
                    label: "build-for-scan");

                if (exitCode != 0)
                {
                    log.LogMessage(MessageImportance.Normal, "dotnet build failed — skipping script registration generation.");
                    return false;
                }

                dllPath = Directory.EnumerateFiles(binDir, $"{projectName}.dll", SearchOption.AllDirectories)
                    .Where(p => !p.Contains(Path.DirectorySeparatorChar + "publish" + Path.DirectorySeparatorChar))
                    .OrderByDescending(File.GetLastWriteTimeUtc)
                    .FirstOrDefault();

                if (dllPath == null)
                {
                    log.LogMessage(MessageImportance.Normal, $"Could not find {projectName}.dll after build — skipping.");
                    return false;
                }
            }

            log.LogMessage(MessageImportance.Normal, $"Scanning: {dllPath}");

            var scriptTypes = new List<string>();
            var scriptFields = new Dictionary<string, List<(string Name, string Call)>>();
            try
            {
                using var fs = new FileStream(dllPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var peReader = new PEReader(fs);

                if (!peReader.HasMetadata)
                {
                    log.LogMessage(MessageImportance.Normal, "DLL has no metadata — skipping.");
                    return false;
                }

                var mr = peReader.GetMetadataReader();

                foreach (var typeDefHandle in mr.TypeDefinitions)
                {
                    var typeDef = mr.GetTypeDefinition(typeDefHandle);

                    if ((typeDef.Attributes & System.Reflection.TypeAttributes.Abstract) != 0) continue;
                    if (!DerivesFromGameBehaviour(mr, typeDef)) continue;

                    string? typeName = GetCSharpTypeName(mr, typeDef);
                    if (typeName == null)
                    {
                        log.LogWarning($"Script '{mr.GetString(typeDef.Name)}' is not registered: a script class must not be generic, and a class it is nested in must be public or internal.");
                        continue;
                    }
                    if (!HasPublicParameterlessConstructor(mr, typeDef))
                    {
                        log.LogWarning($"Script '{typeName}' is not registered: it has no public constructor without parameters.");
                        continue;
                    }
                    scriptTypes.Add(typeName);
                    scriptFields[typeName] = SettableFields(mr, typeDefHandle, Path.GetDirectoryName(dllPath)!);
                    log.LogMessage(MessageImportance.Normal, $"  Found script: {typeName} ({scriptFields[typeName].Count} field(s))");
                }
            }
            catch (Exception ex)
            {
                log.LogMessage(MessageImportance.Normal, $"Failed to read DLL metadata: {ex.Message} — skipping.");
                return false;
            }

            string sourceDir = Path.Combine(projectPath, "Source");
            Directory.CreateDirectory(Path.Combine(sourceDir, "Generated"));
            string outputPath = Path.Combine(sourceDir, "Generated", "RegisteredScripts.g.cs");

            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated by SokolApplicationBuilder — do not edit>");
            sb.AppendLine("#nullable disable");
            sb.AppendLine("#if __ANDROID__ || __IOS__ || _DESKTOP_PUBLISHED_BINARY_");
            sb.AppendLine("using System.Runtime.CompilerServices;");
            sb.AppendLine();
            sb.AppendLine("internal static class RegisteredScripts");
            sb.AppendLine("{");
            sb.AppendLine("    [ModuleInitializer]");
            sb.AppendLine("    public static void Register()");
            sb.AppendLine("    {");
            if (scriptTypes.Count == 0)
            {
                sb.AppendLine("        // No GameBehaviour subclasses found.");
            }
            else
            {
                const string scripting = "global::GameEditor.Framework.Scripting";
                foreach (var t in scriptTypes)
                {
                    var fields = scriptFields[t];
                    if (fields.Count == 0)
                    {
                        sb.AppendLine($"        {scripting}.ScriptSystem.RegisterType<global::{t}>(static ({scripting}.GameBehaviour script, string field, in {scripting}.ScriptFieldValue value) => false);");
                        continue;
                    }
                    sb.AppendLine($"        {scripting}.ScriptSystem.RegisterType<global::{t}>(static ({scripting}.GameBehaviour script, string field, in {scripting}.ScriptFieldValue value) =>");
                    sb.AppendLine("        {");
                    sb.AppendLine($"            var s = (global::{t})script;");
                    sb.AppendLine("            switch (field)");
                    sb.AppendLine("            {");
                    foreach (var (name, call) in fields)
                        sb.AppendLine($"                case \"{name}\": return {scripting}.ScriptValue.{call}(ref s.@{name}, value);");
                    sb.AppendLine("                default: return false;");
                    sb.AppendLine("            }");
                    sb.AppendLine("        });");
                }
            }
            sb.AppendLine("    }");
            sb.AppendLine("}");
            sb.AppendLine("#endif");

            string newContent = sb.ToString();
            string? existing = File.Exists(outputPath) ? File.ReadAllText(outputPath) : null;
            if (existing == newContent)
            {
                log.LogMessage(MessageImportance.High, $"✅ RegisteredScripts.g.cs up to date ({scriptTypes.Count} type(s)).");
                return false;
            }

            File.WriteAllText(outputPath, newContent);
            log.LogMessage(MessageImportance.High, $"✅ RegisteredScripts.g.cs updated ({scriptTypes.Count} type(s): {string.Join(", ", scriptTypes)}).");
            return true;
        }

        /// <summary>True when the DLL references GameEditor.Framework, the assembly GameBehaviour is in.</summary>
        private static bool ReferencesScriptFramework(string dllPath)
        {
            try
            {
                using var fs = new FileStream(dllPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var peReader = new PEReader(fs);
                if (!peReader.HasMetadata) return false;
                var mr = peReader.GetMetadataReader();
                foreach (var handle in mr.AssemblyReferences)
                    if (mr.GetString(mr.GetAssemblyReference(handle).Name) == "GameEditor.Framework") return true;
            }
            catch { }
            return false;
        }

        /// <summary>The newest write time of the project's own C# sources (not build output, platform folders or generated files).</summary>
        private static DateTime NewestSourceWriteTimeUtc(string projectPath)
        {
            var newest = DateTime.MinValue;
            var skip = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "bin", "obj", "Android", "ios", "output", "shaders", "Generated", "node_modules" };
            var pending = new Stack<string>();
            pending.Push(projectPath);
            while (pending.Count > 0)
            {
                string dir = pending.Pop();
                try
                {
                    foreach (string sub in Directory.GetDirectories(dir))
                    {
                        string name = Path.GetFileName(sub);
                        if (!skip.Contains(name) && !name.StartsWith('.')) pending.Push(sub);
                    }
                    foreach (string file in Directory.GetFiles(dir, "*.cs"))
                    {
                        var t = File.GetLastWriteTimeUtc(file);
                        if (t > newest) newest = t;
                    }
                }
                catch { }
            }
            return newest;
        }

        /// <summary>Follows the base types inside the assembly up to a class named GameBehaviour that another assembly defines.</summary>
        private static bool DerivesFromGameBehaviour(MetadataReader mr, TypeDefinition typeDef)
        {
            for (int depth = 0; depth < 64; depth++)
            {
                var baseHandle = typeDef.BaseType;
                if (baseHandle.IsNil) return false;
                if (baseHandle.Kind == HandleKind.TypeReference)
                    return mr.GetString(mr.GetTypeReference((TypeReferenceHandle)baseHandle).Name) == "GameBehaviour";
                if (baseHandle.Kind != HandleKind.TypeDefinition) return false;   // a generic base: not a script
                typeDef = mr.GetTypeDefinition((TypeDefinitionHandle)baseHandle);
            }
            return false;
        }

        /// <summary>
        /// The public fields of a script (its own and those of the scripts it derives from) that a scene file's text
        /// can set, each with the ScriptValue method that sets it: Set for a number, a bool, a text, a vector, a
        /// rotation, a matrix, a prefab and another entity, SetEnum for an enum. A field of any other type has no line: it is set in code.
        /// </summary>
        private static List<(string Name, string Call)> SettableFields(MetadataReader mr, TypeDefinitionHandle script, string dllFolder)
        {
            // The class and its base scripts, base first; a field of the same name in a derived script hides the base's.
            var chain = new List<TypeDefinition>();
            var typeDef = mr.GetTypeDefinition(script);
            for (int depth = 0; depth < 64; depth++)
            {
                chain.Insert(0, typeDef);
                if (typeDef.BaseType.Kind != HandleKind.TypeDefinition) break;
                typeDef = mr.GetTypeDefinition((TypeDefinitionHandle)typeDef.BaseType);
            }

            var fields = new List<(string Name, string Call)>();
            foreach (var part in chain)
                foreach (var handle in part.GetFields())
                {
                    var field = mr.GetFieldDefinition(handle);
                    if ((field.Attributes & System.Reflection.FieldAttributes.FieldAccessMask) != System.Reflection.FieldAttributes.Public) continue;
                    if ((field.Attributes & (System.Reflection.FieldAttributes.Static | System.Reflection.FieldAttributes.InitOnly | System.Reflection.FieldAttributes.Literal)) != 0) continue;
                    string? call = null;
                    try { call = FieldSetterCall(mr, mr.GetBlobReader(field.Signature), dllFolder); }
                    catch (BadImageFormatException) { }
                    string name = mr.GetString(field.Name);
                    fields.RemoveAll(f => f.Name == name);
                    if (call != null) fields.Add((name, call));
                }
            return fields;
        }

        /// <summary>The ScriptValue method for a field of this signature, or null when a text cannot set it.</summary>
        private static string? FieldSetterCall(MetadataReader mr, BlobReader signature, string dllFolder)
        {
            if (signature.ReadSignatureHeader().Kind != SignatureKind.Field) return null;
            switch (signature.ReadSignatureTypeCode())
            {
                case SignatureTypeCode.Boolean: case SignatureTypeCode.Char: case SignatureTypeCode.Byte: case SignatureTypeCode.Int16:
                case SignatureTypeCode.Int32: case SignatureTypeCode.UInt32: case SignatureTypeCode.Int64: case SignatureTypeCode.UInt64:
                case SignatureTypeCode.Single: case SignatureTypeCode.Double: case SignatureTypeCode.String:
                    return "Set";
                case SignatureTypeCode.TypeHandle:
                    break;
                default:
                    return null;      // an array, a generic type, a pointer, a type with a modifier
            }
            var type = signature.ReadTypeHandle();
            if (type.Kind == HandleKind.TypeDefinition)
                return IsEnum(mr, mr.GetTypeDefinition((TypeDefinitionHandle)type)) ? "SetEnum" : null;
            if (type.Kind != HandleKind.TypeReference) return null;

            var reference = mr.GetTypeReference((TypeReferenceHandle)type);
            string ns = mr.GetString(reference.Namespace), name = mr.GetString(reference.Name);
            switch (ns + "." + name)
            {
                case "System.Numerics.Vector2": case "System.Numerics.Vector3": case "System.Numerics.Vector4": case "System.Numerics.Quaternion":
                case "System.Numerics.Matrix3x2": case "System.Numerics.Matrix4x4": case "GameEditor.Framework.Scene.Prefab":
                case "Frent.Entity":
                    return "Set";
            }
            // An enum of another assembly (the engine's): that assembly lies beside the game's.
            if (reference.ResolutionScope.Kind != HandleKind.AssemblyReference) return null;
            string other = Path.Combine(dllFolder, mr.GetString(mr.GetAssemblyReference((AssemblyReferenceHandle)reference.ResolutionScope).Name) + ".dll");
            if (!File.Exists(other)) return null;
            try
            {
                using var fs = new FileStream(other, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var pe = new PEReader(fs);
                if (!pe.HasMetadata) return null;
                var omr = pe.GetMetadataReader();
                foreach (var handle in omr.TypeDefinitions)
                {
                    var def = omr.GetTypeDefinition(handle);
                    if (omr.GetString(def.Name) == name && omr.GetString(def.Namespace) == ns && def.GetDeclaringType().IsNil)
                        return IsEnum(omr, def) ? "SetEnum" : null;
                }
            }
            catch (Exception ex) when (ex is IOException or BadImageFormatException or UnauthorizedAccessException) { }
            return null;
        }

        private static bool IsEnum(MetadataReader mr, TypeDefinition typeDef)
        {
            if (typeDef.BaseType.Kind != HandleKind.TypeReference) return false;
            var parent = mr.GetTypeReference((TypeReferenceHandle)typeDef.BaseType);
            return mr.GetString(parent.Name) == "Enum" && mr.GetString(parent.Namespace) == "System";
        }

        /// <summary>
        /// The type's name as C# writes it (Namespace.Outer.Inner), or null when the generated code could not
        /// name it: a generic type, or one nested in a class that is not public or internal.
        /// </summary>
        private static string? GetCSharpTypeName(MetadataReader mr, TypeDefinition typeDef)
        {
            string name = mr.GetString(typeDef.Name);
            if (name.Contains('`')) return null;
            var declaring = typeDef.GetDeclaringType();
            if (declaring.IsNil)
            {
                string ns = mr.GetString(typeDef.Namespace);
                return ns.Length == 0 ? name : ns + "." + name;
            }
            var visibility = typeDef.Attributes & System.Reflection.TypeAttributes.VisibilityMask;
            if (visibility != System.Reflection.TypeAttributes.NestedPublic && visibility != System.Reflection.TypeAttributes.NestedAssembly
                && visibility != System.Reflection.TypeAttributes.NestedFamORAssem) return null;
            string? outer = GetCSharpTypeName(mr, mr.GetTypeDefinition(declaring));
            return outer == null ? null : outer + "." + name;
        }

        private static bool HasPublicParameterlessConstructor(MetadataReader mr, TypeDefinition typeDef)
        {
            foreach (var handle in typeDef.GetMethods())
            {
                var method = mr.GetMethodDefinition(handle);
                if (mr.GetString(method.Name) != ".ctor") continue;
                if ((method.Attributes & System.Reflection.MethodAttributes.MemberAccessMask) != System.Reflection.MethodAttributes.Public) continue;
                var sig = mr.GetBlobReader(method.Signature);
                sig.ReadSignatureHeader();
                if (sig.ReadCompressedInteger() == 0) return true;
            }
            return false;
        }

    }
}