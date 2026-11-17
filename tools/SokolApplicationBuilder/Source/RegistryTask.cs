using System;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Task = Microsoft.Build.Utilities.Task;

namespace SokolApplicationBuilder
{
    /// <summary>
    /// --task registry --path &lt;project&gt;: writes the project's Source/Generated/RegisteredScripts.g.cs (its scripts, data
    /// assets, and its own [GameComponent] / [GameSystem] types, registered with no reflection). The editor runs it before a
    /// web build, whose compilation is ahead of time too (docs: GameEditor GAME_COMPONENTS_SYSTEMS_DESIGN.md §5).
    /// </summary>
    public class RegistryTask : Task
    {
        private readonly Options opts;

        public RegistryTask(Options opts)
        {
            this.opts = opts;
            Utils.opts = opts;
        }

        public override bool Execute()
        {
            string project = Path.GetFullPath(opts.ProjectPath);
            string? csproj = Directory.GetFiles(project, "*.csproj")
                .FirstOrDefault(f => !Path.GetFileNameWithoutExtension(f).EndsWith("Web", StringComparison.OrdinalIgnoreCase));
            if (csproj == null)
            {
                Log.LogError($"No desktop .csproj in {project}");
                return false;
            }
            Utils.GenerateRegisteredScripts(Log, project, Path.GetFileNameWithoutExtension(csproj));
            return true;
        }
    }
}
