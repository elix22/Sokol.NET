// Runs exported robot policies (ONNX) with onnxruntime on random observations and reports the
// inference latency. The benchmark runs on a worker thread; results go to the screen and to the
// system log (logcat on Android, syslog on iOS, stderr on desktop).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.ML.OnnxRuntime;
using Sokol;
using static Sokol.SApp;
using static Sokol.SG;
using static Sokol.SGlue;
using static Sokol.SLog;
using static Sokol.SDebugText;

public static unsafe class OnnxPolicyMobileApp
{
    static readonly string[] Models = { "inverted_pendulum_policy.onnx", "hopper_policy.onnx" };
    static readonly int[] Batches = { 1, 64 };
    const int WarmupRuns = 200;
    const int TimedRuns = 2000;
    const int CheckObservations = 16;

    static sg_pass_action pass_action;
    static readonly List<string> lines = new List<string>();
    static readonly object linesLock = new object();
    static bool finished;
    static bool shotRequested, shotReported;

    [UnmanagedCallersOnly]
    private static unsafe void Init()
    {
        sg_setup(new sg_desc()
        {
            environment = sglue_environment(),
            logger = { func = &slog_func }
        });

        sdtx_desc_t desc = default;
        desc.fonts[0] = sdtx_font_kc853();
        desc.logger.func = &slog_func;
        sdtx_setup(desc);

        pass_action = default;
        pass_action.colors[0].load_action = sg_load_action.SG_LOADACTION_CLEAR;
        pass_action.colors[0].clear_value = new sg_color { r = 0.08f, g = 0.10f, b = 0.14f, a = 1.0f };

        SFilesystem.Initialize();
        var models = new List<(string name, byte[] bytes)>();
        foreach (string name in Models)
        {
            var (data, status) = SFilesystem.LoadFileSync(name);
            if (data == null)
                Print($"{name}: load failed ({status})");
            else
                models.Add((name, data));
        }

        new Thread(() => RunBenchmarks(models)) { IsBackground = true, Name = "onnx-bench" }.Start();
    }

    static void RunBenchmarks(List<(string name, byte[] bytes)> models)
    {
        try
        {
            Print($"onnxruntime {OrtEnv.Instance().GetVersionString()}, {RuntimeInformation.RuntimeIdentifier}");
            Print($"CPU EP, 1 intra-op thread, {WarmupRuns} warm-up + {TimedRuns} timed runs per row");
            foreach (var (name, bytes) in models)
                RunModel(name, bytes);
        }
        catch (Exception e)
        {
            Print($"FAILED: {e.GetType().Name}: {e.Message}");
        }
        Print("done");
        finished = true;
    }

    static void RunModel(string name, byte[] bytes)
    {
        using var options = new SessionOptions
        {
            IntraOpNumThreads = 1,
            InterOpNumThreads = 1,
            ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
        };
        long t0 = Stopwatch.GetTimestamp();
        using var session = new InferenceSession(bytes, options);
        double loadMs = Stopwatch.GetElapsedTime(t0).TotalMilliseconds;

        string inName = session.InputNames[0], outName = session.OutputNames[0];
        int obsDim = session.InputMetadata[inName].Dimensions[1];
        int actDim = session.OutputMetadata[outName].Dimensions[1];
        Print($"{name}: obs {obsDim} -> action {actDim}, {bytes.Length / 1024.0:0.0} KB, session {loadMs:0.0} ms");

        var rng = new Random(1);
        using var runOptions = new RunOptions();
        var inNames = new[] { inName };
        var outNames = new[] { outName };

        // Same seeded observations on every device: the checksum must match the desktop run.
        {
            var obs = new float[obsDim];
            var act = new float[actDim];
            using var inValue = OrtValue.CreateTensorValueFromMemory(obs, new long[] { 1, obsDim });
            using var outValue = OrtValue.CreateTensorValueFromMemory(act, new long[] { 1, actDim });
            double sum = 0;
            for (int i = 0; i < CheckObservations; i++)
            {
                FillNormal(rng, obs);
                session.Run(runOptions, inNames, new[] { inValue }, outNames, new[] { outValue });
                foreach (float a in act)
                    sum += a;
            }
            Print($"  check: sum of actions over {CheckObservations} seeded obs = {sum:0.000000}");
        }

        foreach (int batch in Batches)
        {
            var obs = new float[batch * obsDim];
            var act = new float[batch * actDim];
            using var inValue = OrtValue.CreateTensorValueFromMemory(obs, new long[] { batch, obsDim });
            using var outValue = OrtValue.CreateTensorValueFromMemory(act, new long[] { batch, actDim });
            var inputs = new[] { inValue };
            var outputs = new[] { outValue };
            var micros = new double[TimedRuns];

            for (int i = 0; i < WarmupRuns + TimedRuns; i++)
            {
                FillNormal(rng, obs);
                long start = Stopwatch.GetTimestamp();
                session.Run(runOptions, inNames, inputs, outNames, outputs);
                long end = Stopwatch.GetTimestamp();
                if (i >= WarmupRuns)
                    micros[i - WarmupRuns] = (end - start) * 1e6 / Stopwatch.Frequency;
            }

            Array.Sort(micros);
            double mean = 0;
            foreach (double m in micros)
                mean += m;
            mean /= TimedRuns;
            Print($"  batch {batch,2}: mean {mean,7:0.0} us  p50 {micros[TimedRuns / 2],7:0.0}  p99 {micros[TimedRuns * 99 / 100],7:0.0}  min {micros[0],7:0.0}  ({mean / batch:0.00} us/obs)");
        }
    }

    // Standard normal observations (Box-Muller), from a seeded System.Random so every platform sees the same numbers.
    static void FillNormal(Random rng, float[] values)
    {
        for (int i = 0; i < values.Length; i++)
        {
            double u1 = 1.0 - rng.NextDouble(), u2 = rng.NextDouble();
            values[i] = (float)(Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2));
        }
    }

    static string? ScreenshotPath()
    {
#if __ANDROID__
        return null; // no app directory adb can read from a release build; adb screencap works instead
#elif __IOS__
        return System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "onnx_policy_mobile.png");
#else
        return System.IO.Path.Combine(AppContext.BaseDirectory, "onnx_policy_mobile.png");
#endif
    }

    static void Print(string line)
    {
        lock (linesLock)
            lines.Add(line);
        slog_func_native("onnx_policy", 3, 0, line, 0, "", null);
    }

    [UnmanagedCallersOnly]
    private static unsafe void Frame()
    {
        SFilesystem.Update();

        // ~100 columns of text across the screen whatever its size, so a phone in landscape fits every row.
        float w = sapp_widthf(), h = sapp_heightf();
        float scale = Math.Max(1.0f, w / (100 * 8));
        sdtx_canvas(w / scale, h / scale);
        sdtx_origin(1, 1);
        sdtx_color3b(0xf0, 0xc0, 0x40);
        sdtx_puts(finished ? "onnx_policy_mobile - finished\n\n" : "onnx_policy_mobile - running...\n\n");
        sdtx_color3b(0xe0, 0xe0, 0xe0);
        lock (linesLock)
            foreach (string line in lines)
            {
                sdtx_puts(line);
                sdtx_crlf();
            }

        // One in-app screenshot of the results (an iOS 26 device can't be screenshotted from the Mac without a tunnel).
        string? shot = ScreenshotPath();
        if (finished && shot != null && !shotRequested)
            shotRequested = ScreenCapture.Request(shot);

        ScreenCapture.BeginFrame(pass_action); // swapchain pass, or the capture target when armed
        sdtx_draw();
        sg_end_pass();
        ScreenCapture.Commit();

        if (shotRequested && !shotReported && !ScreenCapture.Pending)
        {
            shotReported = true;
            Print(ScreenCapture.LastError ?? $"screenshot: {ScreenCapture.LastPath}");
        }
    }

    [UnmanagedCallersOnly]
    private static unsafe void Event(sapp_event* e)
    {
    }

    [UnmanagedCallersOnly]
    static void Cleanup()
    {
        SFilesystem.Shutdown();
        ScreenCapture.Shutdown();
        sdtx_shutdown();
        sg_shutdown();
    }

    public static SApp.sapp_desc sokol_main()
    {
        return new SApp.sapp_desc()
        {
            init_cb = &Init,
            frame_cb = &Frame,
            event_cb = &Event,
            cleanup_cb = &Cleanup,
            width = 1280,
            height = 720,
            sample_count = 1,
            window_title = "onnx_policy_mobile",
            icon = { sokol_default = true },
            logger = { func = &slog_func }
        };
    }
}
