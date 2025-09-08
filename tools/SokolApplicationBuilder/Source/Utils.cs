
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// Modified by Eli Aloni (A.K.A. elix22)

using System;
using System.Diagnostics;
using System.Reflection;
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
        

    }
}