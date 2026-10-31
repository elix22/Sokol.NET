// VecEnv throughput: one mjModel, N mjData stepped in lock-step with Parallel.For (one barrier per
// env step, as an RL rollout does between policy calls). Random controls; an env resets when the
// Gymnasium termination rule fires or after 1000 env steps. One env step = Gymnasium frame_skip
// physics steps (InvertedPendulum-v5: 2, Hopper-v5: 4).
// Usage: mujoco_vecenv_bench [--seconds S] [--json file]
using System.Diagnostics;
using System.Text.Json;
using Mujoco;
using static Mujoco.MujocoLib;

unsafe
{
    double seconds = 2;
    string? jsonPath = null;
    for (int i = 0; i < args.Length - 1; i++)
    {
        if (args[i] == "--seconds") seconds = double.Parse(args[i + 1]);
        if (args[i] == "--json") jsonPath = args[i + 1];
    }

    var models = new (string Name, int FrameSkip, Func<IntPtr, bool> Unhealthy)[]
    {
        // InvertedPendulum-v5: terminate when |pole angle| > 0.2
        ("inverted_pendulum", 2, d => Math.Abs(((mjData_*)d)->qpos[1]) > 0.2),
        // Hopper-v5: healthy z > 0.7, |angle| < 0.2 (qpos = rootx, rootz, rooty, ...)
        ("hopper", 4, d => ((mjData_*)d)->qpos[1] < 0.7 || Math.Abs(((mjData_*)d)->qpos[2]) > 0.2),
    };
    int[] envCounts = [1, 8, 16, 64];

    Console.WriteLine($"MuJoCo {MjHelpers.Version}, {Environment.ProcessorCount} logical cores, {seconds} s per row");
    Console.WriteLine($"{"model",-18} {"N",4} {"env-steps/s",12} {"mj_step/s",12} {"resets/s",10}");
    var results = new List<Dictionary<string, object>>();

    foreach (var (name, frameSkip, unhealthy) in models)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "mjcf", name + ".xml");
        mjModel_* m = MjHelpers.LoadXml(name + ".xml", File.ReadAllBytes(path));
        foreach (int n in envCounts)
        {
            var data = new IntPtr[n];
            var episodeSteps = new int[n];
            var rngs = new Random[n];
            for (int e = 0; e < n; e++)
            {
                data[e] = (IntPtr)mj_makeData(m);
                rngs[e] = new Random(e + 1);
            }
            long resets = 0;

            void EnvStep(int e)
            {
                var d = (mjData_*)data[e];
                var rng = rngs[e];
                for (int a = 0; a < (int)m->nu; a++)
                {
                    double lo = m->actuator_ctrlrange[2 * a], hi = m->actuator_ctrlrange[2 * a + 1];
                    d->ctrl[a] = lo + (hi - lo) * rng.NextDouble();
                }
                for (int k = 0; k < frameSkip; k++)
                    mj_step(m, d);
                if (++episodeSteps[e] >= 1000 || unhealthy((IntPtr)d))
                {
                    mj_resetData(m, d);
                    episodeSteps[e] = 0;
                    Interlocked.Increment(ref resets);
                }
            }

            // warm-up, then run for the configured wall time
            for (int s = 0; s < 200; s++)
                Parallel.For(0, n, EnvStep);
            resets = 0;
            long steps = 0;
            var sw = Stopwatch.StartNew();
            while (sw.Elapsed.TotalSeconds < seconds)
            {
                for (int s = 0; s < 100; s++)
                    Parallel.For(0, n, EnvStep);
                steps += 100;
            }
            double t = sw.Elapsed.TotalSeconds;
            double envSteps = steps * n / t;
            Console.WriteLine($"{name,-18} {n,4} {envSteps,12:N0} {envSteps * frameSkip,12:N0} {resets / t,10:N0}");
            results.Add(new() { ["model"] = name, ["n"] = n, ["env_steps_per_s"] = Math.Round(envSteps), ["mj_steps_per_s"] = Math.Round(envSteps * frameSkip), ["frame_skip"] = frameSkip });

            foreach (var d in data)
                mj_deleteData((mjData_*)d);
        }
        mj_deleteModel(m);
    }

    if (jsonPath != null)
    {
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["mujoco"] = MjHelpers.Version,
            ["logical_cores"] = Environment.ProcessorCount,
            ["seconds_per_row"] = seconds,
            ["date"] = DateTime.Now.ToString("yyyy-MM-dd"),
            ["results"] = results,
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"wrote {jsonPath}");
    }
}
