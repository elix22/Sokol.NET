using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using Imgui;
using static Imgui.ImguiNative;
using GameEditor.Framework.Core;

namespace GameEditor.UI
{
    /// <summary>
    /// Dockable "Build &amp; Deploy" panel.
    /// Lets the user select a deployment target for the current project and stream
    /// SokolApplicationBuilder output to the Console Window.
    /// </summary>
    public static class BuildDeployPanel
    {
        private static readonly string[] _targetLabels =
        {
            "Desktop (macOS / Win / Linux)",
            "Web (WebAssembly)",
            "Android APK",
            "Android AAB (Play Store)",
            "iOS"
        };

        // CLI arguments passed after "--" to SokolApplicationBuilder
        private static readonly string[] _taskArgs =
        {
            "--task prepare --architecture desktop",
            "--task prepare --architecture web",
            "--task AndroidBuild --architecture android",
            "--task AndroidBuildRelease --architecture android",
            "--task iOSBuild --architecture ios"
        };

        private static int     _selectedTarget = 0;
        private static Process? _buildProcess  = null;
        private static bool     _isBuilding    = false;

        // Thread-safe queue drained on the main ImGui thread each frame
        private static readonly ConcurrentQueue<(bool isErr, string msg)> _outputQueue = new();

        public static void Draw()
        {
            // Drain process output on the main thread (safe from both main + background threads)
            while (_outputQueue.TryDequeue(out var item))
            {
                if (item.isErr) Logger.Warning(item.msg);
                else            Logger.Info(item.msg);
            }

            // Check if a running build has finished
            if (_isBuilding && _buildProcess != null && _buildProcess.HasExited)
                _isBuilding = false;

            byte open = 1;
            if (!igBegin("Build & Deploy", ref open, ImGuiWindowFlags.None))
            {
                igEnd();
                return;
            }

            // ── Project info ─────────────────────────────────────────────────
            var cfg    = ConfigManager.Config;
            string folder = ConfigManager.ProjectFolder ?? string.Empty;

            igText($"Project : {cfg?.ProjectName ?? "(no project loaded)"}");
            if (folder.Length > 0)
                igTextWrapped($"Folder  : {folder}");

            igSeparator();

            // ── Target ───────────────────────────────────────────────────────
            igSetNextItemWidth(240f);
            igCombo_Str_arr("Target##bd", ref _selectedTarget,
                _targetLabels, _targetLabels.Length, -1);

            igSpacing();

            // ── Build / Cancel ───────────────────────────────────────────────
            if (_isBuilding)
            {
                igTextColored(new Vector4(0.3f, 1f, 0.3f, 1f), "Building...");
                igSameLine(0, 10);
                if (igButton("Cancel##bd", new Vector2(70, 0)))
                {
                    try { _buildProcess?.Kill(entireProcessTree: true); } catch { }
                    Logger.Warning("Build cancelled.");
                    _isBuilding = false;
                }
            }
            else
            {
                bool canBuild = ConfigManager.HasProject;
                if (!canBuild) igBeginDisabled(true);
                if (igButton("Build##bd", new Vector2(80, 0)))
                    StartBuild(folder, ConfigManager.GetSokolAppBuilderPath());
                if (!canBuild)
                {
                    igEndDisabled();
                    igSameLine(0, 8);
                    igTextDisabled("(open a project first)");
                }
            }

            // ── Diagnostics collapsible ──────────────────────────────────────
            igSpacing();
            if (igCollapsingHeader_TreeNodeFlags("Builder path", ImGuiTreeNodeFlags.None))
            {
                igTextWrapped(ConfigManager.GetSokolAppBuilderPath());
            }

            igEnd();
        }

        private static void StartBuild(string projectPath, string builderPath)
        {
            string args = $"run --project \"{builderPath}\" -- "
                        + $"{_taskArgs[_selectedTarget]} "
                        + $"--path \"{projectPath}\"";

            Logger.Info($"[Build] dotnet {args}");

            var psi = new ProcessStartInfo("dotnet", args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true
            };

            _buildProcess = Process.Start(psi);
            if (_buildProcess == null)
            {
                Logger.Error("Failed to start build process.");
                return;
            }

            _isBuilding = true;

            // Enqueue output from background threads; drained in Draw() on main thread.
            _buildProcess.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null) _outputQueue.Enqueue((false, e.Data));
            };
            _buildProcess.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null) _outputQueue.Enqueue((true, e.Data));
            };
            _buildProcess.BeginOutputReadLine();
            _buildProcess.BeginErrorReadLine();
        }
    }
}
