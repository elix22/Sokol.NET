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
    public class RegisterTask : Task
    {
        Options opts;
        public RegisterTask(Options opts)
        {
            this.opts = opts;
        }

        public override bool Execute()
        {
            string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrEmpty(homeDir) || !Directory.Exists(homeDir))
            {
                homeDir = Environment.GetEnvironmentVariable("HOME") ?? "";
            }

            string configFolder = Path.Combine(homeDir, ".sokolnet_config");

            // Remove existing config folder
            if (Directory.Exists(configFolder))
            {
                Directory.Delete(configFolder, true);
            }

            // Create config folder
            Directory.CreateDirectory(configFolder);

            // Copy template
            string templatePath = Path.Combine(opts.TemplatesPath, "SokolNetHome.config");
            string configPath = Path.Combine(configFolder, "SokolNetHome.config");
            File.Copy(templatePath, configPath);

            // Get current working directory (use the path from options or current directory)
            string currentDir = string.IsNullOrEmpty(opts.ProjectPath) ? Directory.GetCurrentDirectory() : opts.ProjectPath;

            // Find the root folder by looking for project markers
            string rootDir = FindProjectRoot(currentDir);

            // Handle Windows paths
            string pathToWrite = rootDir;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Convert to Windows path format if needed
                pathToWrite = rootDir.Replace('/', '\\');
            }

            // Replace template placeholder
            string content = File.ReadAllText(configPath);
            content = content.Replace("TEMPLATE_SOKOLNET_HOME", pathToWrite);
            File.WriteAllText(configPath, content);

            // Create sokolnet_home file
            string homeFile = Path.Combine(configFolder, "sokolnet_home");
            if (File.Exists(homeFile))
            {
                File.Delete(homeFile);
            }
            File.WriteAllText(homeFile, rootDir);

            // Verify and output results
            if (File.Exists(Path.Combine(configFolder, "sokolnet_home")) &&
                File.Exists(Path.Combine(configFolder, "SokolNetHome.config")))
            {
                Log.LogMessage(MessageImportance.High, "");
                Log.LogMessage(MessageImportance.High, "Sokol.Net configured!");
                Log.LogMessage(MessageImportance.High, "");

                string sokolNetHome = File.ReadAllText(Path.Combine(configFolder, "sokolnet_home"));
                string sokolNetHomeXml = File.ReadAllText(Path.Combine(configFolder, "SokolNetHome.config"));

                Log.LogMessage(MessageImportance.High, $"cat {homeDir}/.sokolnet_config/sokolnet_home");
                Log.LogMessage(MessageImportance.High, sokolNetHome);
                Log.LogMessage(MessageImportance.High, "");
                Log.LogMessage(MessageImportance.High, $"cat {homeDir}/.sokolnet_config/SokolNetHome.config");
                Log.LogMessage(MessageImportance.High, sokolNetHomeXml);
                Log.LogMessage(MessageImportance.High, "");
            }
            else
            {
                Log.LogError("Sokol.Net configuration failure!");
                return false;
            }

            return true;
        }

        private string FindProjectRoot(string startDir)
        {
            DirectoryInfo dir = new DirectoryInfo(startDir);

            // Look for project root markers
            while (dir != null)
            {
                // Check for common project markers
                if (File.Exists(Path.Combine(dir.FullName, "Directory.Build.props")) ||
                    File.Exists(Path.Combine(dir.FullName, "sokol.csproj")) ||
                    Directory.Exists(Path.Combine(dir.FullName, ".git")) ||
                    Directory.Exists(Path.Combine(dir.FullName, "src")) && Directory.Exists(Path.Combine(dir.FullName, "examples")))
                {
                    return dir.FullName;
                }

                dir = dir.Parent;
            }

            // If no markers found, return the original directory
            return startDir;
        }
    }
}