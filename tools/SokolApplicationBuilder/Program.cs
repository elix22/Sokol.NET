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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Security;
using CommandLine;
using Microsoft.Build.Framework;

namespace SokolApplicationBuilder
{

    class Program
    {
        static SokolBuildEngine buildEngine = new SokolBuildEngine();
        static int Main(string[] args)
        {
            Utils.RegisterMSBuild();
            int exitCode = 0;
            Parser.Default.ParseArguments<Options>(args)
                .WithParsed(opts => exitCode = RunOptions(opts))
                .WithNotParsed(errors => exitCode = 1);
            return exitCode;
        }

        static int RunOptions(Options opts)
        {
            if (opts.Task != string.Empty)
            {
                // Validate required options for specific tasks
                if (opts.Task != "register" && string.IsNullOrEmpty(opts.ProjectPath))
                {
                    Console.WriteLine("ERROR: --path is required for task '" + opts.Task + "'");
                    return 1;
                }

                // Check if sokolnet_home config exists, if not, run register task first
                if (opts.Task != "register")
                {
                    string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    if (string.IsNullOrEmpty(homeDir) || !Directory.Exists(homeDir))
                    {
                        homeDir = Environment.GetEnvironmentVariable("HOME") ?? "";
                    }
                    string sokolNetHomeFile = Path.Combine(homeDir, ".sokolnet_config", "sokolnet_home");
                    
                    if (!File.Exists(sokolNetHomeFile))
                    {
                        Console.WriteLine("SokolNetHome configuration not found. Running register task first...");
                        
                        // Create a temporary options object for register task
                        var registerOpts = new Options
                        {
                            Task = "register",
                            ProjectPath = opts.ProjectPath // Use the same path if provided
                        };
                        
                        // Run register task
                        var registerTask = new RegisterTask(registerOpts);
                        registerTask.BuildEngine = buildEngine;
                        bool registerResult = registerTask.Execute();
                        
                        if (!registerResult)
                        {
                            Console.WriteLine("ERROR: Failed to create SokolNetHome configuration. Cannot proceed with task '" + opts.Task + "'.");
                            return 1;
                        }
                        
                        Console.WriteLine("SokolNetHome configuration created successfully. Continuing with task '" + opts.Task + "'...");
                        Console.WriteLine();
                    }
                }

                bool taskSuccess = false;
                switch (opts.Task)
                {
                    case "build":
                        {
                            switch (opts.Arch)
                            {
                                case "android":
                                    {
                                        var task = new AndroidBuildTask(opts);
                                        task.BuildEngine = buildEngine;
                                        taskSuccess = task.Execute();
                                    }
                                    break;

                                case "ios":
                                    {
                                        var task = new IOSBuildTask(opts);
                                        task.BuildEngine = buildEngine;
                                        taskSuccess = task.Execute();
                                    }
                                    break;

                                case "desktop":
                                    {
                                        var task = new DesktopBuildTask(opts);
                                        task.BuildEngine = buildEngine;
                                        taskSuccess = task.Execute();
                                    }
                                    break;

                                case "web":
                                    {
                                        var task = new WebBuildTask(opts);
                                        task.BuildEngine = buildEngine;
                                        taskSuccess = task.Execute();
                                    }
                                    break;

                                default:
                                    {
                                        Console.WriteLine("Unknown Architecture " + opts.Arch);
                                        return 1;
                                    }
                            }

                        }
                        break;

                    case "clean":
                        {

                            var task = new CleanTask(opts);
                            task.BuildEngine = buildEngine;
                            taskSuccess = task.Execute();
                        }
                        break;

                    case "register":
                        {
                            var task = new RegisterTask(opts);
                            task.BuildEngine = buildEngine;
                            taskSuccess = task.Execute();
                        }
                        break;

                    case "prepare":
                        {
                            var task = new PrepareTask(opts);
                            task.BuildEngine = buildEngine;
                            taskSuccess = task.Execute();
                        }
                        break;

                    default:
                        {
                            Console.WriteLine("Unknown task " + opts.Task);
                            return 1;
                        }
                }
                
                return taskSuccess ? 0 : 1;
            }
            return 0;
        }
    }
}
