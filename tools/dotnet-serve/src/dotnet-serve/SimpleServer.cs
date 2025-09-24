// Copyright (c) Nate McMaster.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace McMaster.DotNet.Serve;

internal class SimpleServer
{
    private readonly CommandLineOptions _options;
    private readonly IConsole _console;
    private readonly string _currentDirectory;
    private readonly IReporter _reporter;

    public SimpleServer(CommandLineOptions options, IConsole console, string currentDirectory)
    {
        _options = options;
        _console = console;
        _currentDirectory = currentDirectory;
        _reporter = new ConsoleReporter(console)
        {
            IsQuiet = options.Quiet == true,
            IsVerbose = options.Verbose == true,
        };
    }

    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        var directory = Path.GetFullPath(_options.Directory ?? _currentDirectory);
        var port = _options.Port;

        if (!CertificateLoader.TryLoadCertificate(_options, _currentDirectory, out var cert, out var certLoadError))
        {
            _reporter.Verbose(certLoadError.ToString());
            _reporter.Error(certLoadError.Message);
            return 1;
        }

        if (cert != null)
        {
            _reporter.Verbose($"Using certificate {cert.SubjectName.Name} ({cert.Thumbprint})");
        }

        void ConfigureHttps(ListenOptions options)
        {
            if (cert != null)
            {
                options.UseHttps(cert);
            }
        }

        _console.Write(ConsoleColor.DarkYellow, "Starting server, serving ");
        _console.WriteLine(Path.GetRelativePath(_currentDirectory, directory));

        var defaultExtensions = _options.GetDefaultExtensions();
        if (defaultExtensions != null)
        {
            _console.WriteLine(ConsoleColor.DarkYellow,
                $"Using default extensions " + string.Join(", ", defaultExtensions));
        }

        // Try to start the server, handling port conflicts
        var currentPort = port.GetValueOrDefault();
        const int maxRetries = 10;
        var retryCount = 0;

        while (retryCount < maxRetries)
        {
            try
            {
                // Update the port in options if it changed
                if (currentPort != port.GetValueOrDefault())
                {
                    _options.GetType().GetProperty("Port")?.SetValue(_options, currentPort);
                }

                // Build the host with the current port
                var host = new WebHostBuilder()
                    .ConfigureLogging(l =>
                    {
                        l.SetMinimumLevel(_options.MinLogLevel);
                        l.AddConsole();
                    })
                    .PreferHostingUrls(false)
                    .UseKestrel(o =>
                    {
                        if (_options.ShouldUseLocalhost())
                        {
                            if (currentPort == 0)
                            {
                                o.ListenAnyIP(0, ConfigureHttps);
                            }
                            else
                            {
                                o.ListenLocalhost(currentPort, ConfigureHttps);
                            }
                        }
                        else
                        {
                            foreach (var a in _options.Addresses)
                            {
                                if (a == IPAddress.IPv6Any)
                                {
                                    o.ListenAnyIP(currentPort, ConfigureHttps);
                                }
                                else
                                {
                                    o.Listen(a, currentPort, ConfigureHttps);
                                }
                            }
                        }
                    })
                    .UseWebRoot(directory)
                    .UseContentRoot(directory)
                    .UseEnvironment("Production")
                    .SuppressStatusMessages(true)
                    .UseStartup<Startup>()
                    .ConfigureServices(s => s.AddSingleton(_options))
                    .Build();

                await host.StartAsync(cancellationToken);
                var logger = host.Services.GetRequiredService<ILogger<SimpleServer>>();
                AfterServerStart(host, logger);
                await host.WaitForShutdownAsync(cancellationToken);
                return 0;
            }
            catch (Exception ex) when (ex is Microsoft.AspNetCore.Connections.AddressInUseException || 
                                   ex is System.Net.Sockets.SocketException || 
                                   ex.Message.Contains("address already in use", StringComparison.OrdinalIgnoreCase))
            {
                if (retryCount == 0)
                {
                    _console.WriteLine(ConsoleColor.Yellow, $"Port {currentPort} is already in use. Attempting to kill existing processes...");
                    KillProcessesUsingPort(currentPort);
                    await Task.Delay(1000, cancellationToken); // Wait for processes to terminate
                }
                else
                {
                    _console.WriteLine(ConsoleColor.Yellow, $"Port {currentPort} is still in use. Trying next available port...");
                    currentPort = FindNextAvailablePort(currentPort + 1);
                }
                retryCount++;
            }
        }

        _reporter.Error($"Failed to start server after {maxRetries} attempts. Could not find an available port.");
        return 1;
    }

    private void AfterServerStart(IWebHost host, ILogger<SimpleServer> logger)
    {
        var addresses = host.ServerFeatures.Get<IServerAddressesFeature>();
        var pathBase = _options.GetPathBase();

        _console.WriteLine(GetListeningAddressText(addresses));
        foreach (var a in addresses.Addresses)
        {
            logger.LogDebug("Listening on {address}", a);
            _console.WriteLine(ConsoleColor.Green, "  " + NormalizeToLoopbackAddress(a) + pathBase);
        }

        _console.WriteLine("");
        _console.WriteLine("Press CTRL+C to exit");

        // Always open browser automatically (similar to Node.js script behavior)
        var uri = new Uri(NormalizeToLoopbackAddress(addresses.Addresses.First()));

        if (!string.IsNullOrEmpty(pathBase))
        {
            uri = new Uri(uri, pathBase);
        }

        _console.WriteLine("Opening browser...");
        LaunchBrowser(uri.ToString());

        if (_options.OpenBrowser.hasValue)
        {
            uri = new Uri(NormalizeToLoopbackAddress(addresses.Addresses.First()));

            if (!string.IsNullOrWhiteSpace(_options.OpenBrowser.path))
            {
                uri = new Uri(uri, _options.OpenBrowser.path);
            }
            else if (!string.IsNullOrEmpty(pathBase))
            {
                uri = new Uri(uri, pathBase);
            }

            LaunchBrowser(uri.ToString());
        }

        static string GetListeningAddressText(IServerAddressesFeature addresses)
        {
            if (addresses.Addresses.Any())
            {
                var url = addresses.Addresses.First();
                if (url.Contains("0.0.0.0") || url.Contains("[::]"))
                {
                    return "Listening on any IP:";
                }
            }

            return "Listening on:";
        }

        static string NormalizeToLoopbackAddress(string url)
        {
            // normalize to loopback if binding to IPAny
            url = url.Replace("0.0.0.0", "localhost");
            url = url.Replace("[::]", "localhost");

            return url;
        }
    }

    private void LaunchBrowser(string url)
    {
        var psi = new ProcessStartInfo();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            psi.FileName = "open";
            psi.ArgumentList.Add(url);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            psi.FileName = "xdg-open";
            psi.ArgumentList.Add(url);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            psi.FileName = "cmd";
            psi.ArgumentList.Add("/C");
            psi.ArgumentList.Add("start");
            psi.ArgumentList.Add(url);
        }
        else
        {
            _console.Write(ConsoleColor.Red, "Could not determine how to launch the browser for this OS platform.");
            return;
        }

        Process.Start(psi);
    }

    private void KillProcessesUsingPort(int port)
    {
        try
        {
            // Use lsof to find processes using the port
            var psi = new ProcessStartInfo
            {
                FileName = "lsof",
                Arguments = $"-ti:{port}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process != null)
            {
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
                {
                    var pids = output.Trim().Split('\n')
                        .Where(line => !string.IsNullOrWhiteSpace(line))
                        .Select(line => line.Trim())
                        .ToArray();

                    foreach (var pid in pids)
                    {
                        if (int.TryParse(pid, out var processId))
                        {
                            try
                            {
                                var proc = Process.GetProcessById(processId);
                                _console.WriteLine(ConsoleColor.Yellow, $"Killing process {processId} ({proc.ProcessName}) using port {port}");
                                proc.Kill();
                                proc.WaitForExit(5000); // Wait up to 5 seconds for the process to exit
                            }
                            catch (Exception ex)
                            {
                                _console.WriteLine(ConsoleColor.Red, $"Failed to kill process {processId}: {ex.Message}");
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _console.WriteLine(ConsoleColor.Red, $"Failed to kill processes using port {port}: {ex.Message}");
        }
    }

    private int FindNextAvailablePort(int startingPort)
    {
        const int maxPort = 65535;
        for (int port = startingPort; port <= maxPort; port++)
        {
            if (IsPortAvailable(port))
            {
                return port;
            }
        }

        // If we can't find a port in the upper range, try from 1024 upwards
        for (int port = 1024; port < startingPort; port++)
        {
            if (IsPortAvailable(port))
            {
                return port;
            }
        }

        throw new Exception("No available ports found");
    }

    private bool IsPortAvailable(int port)
    {
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.Bind(new IPEndPoint(IPAddress.Loopback, port));
            socket.Close();
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }
}
