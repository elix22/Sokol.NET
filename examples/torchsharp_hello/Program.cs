// torchsharp_hello — device probe, CPU vs MPS matmul benchmark, policy-net latency table.
// Usage: dotnet run -c Release [-- --bench]   (--bench also writes torch_bench.json)

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using TorchSharp;
using static TorchSharp.torch;

bool writeBench = args.Contains("--bench");

Console.WriteLine($"OS                 : {RuntimeInformation.OSDescription} {RuntimeInformation.OSArchitecture}");
Console.WriteLine($".NET               : {Environment.Version}");
Console.WriteLine($"cuda_is_available  = {torch.cuda_is_available()}");
Console.WriteLine($"mps_is_available   = {torch.mps_is_available()}");

var devices = new List<Device> { CPU };
if (torch.mps_is_available()) {
    // Prove the device really works: allocate, compute, read back.
    using var t = torch.arange(4, float32, device: MPS) * 2;
    Console.WriteLine($"MPS probe          : {string.Join(",", t.cpu().data<float>().ToArray())} on {t.device}");
    devices.Add(MPS);
}
Console.WriteLine();

// MPS kernels run asynchronously; reading a scalar back waits for the queue to drain.
static void Sync(Tensor t) => t.flatten()[0].item<float>();

static double TimeMs(int iters, Func<Tensor> step)
{
    for (int i = 0; i < 3; i++) {
        using var scope = NewDisposeScope();
        Sync(step());
    }
    var sw = Stopwatch.StartNew();
    using (var scope = NewDisposeScope()) {
        Tensor last = step();
        for (int i = 1; i < iters; i++) last = step();
        Sync(last);
    }
    return sw.Elapsed.TotalMilliseconds / iters;
}

// --- matmul -----------------------------------------------------------------------------------
var matmul = new List<object>();
Console.WriteLine("matmul float32 (ms per op, GFLOP/s)");
Console.WriteLine($"{"n",6} {"cpu ms",10} {"cpu GF/s",10} {"mps ms",10} {"mps GF/s",10} {"speedup",8}");
foreach (int n in new[] { 256, 512, 1024, 2048, 4096 }) {
    var ms = new Dictionary<string, double>();
    foreach (var dev in devices) {
        using var a = randn(n, n, device: dev);
        using var b = randn(n, n, device: dev);
        ms[dev.type.ToString()] = TimeMs(n >= 2048 ? 10 : 50, () => a.matmul(b));
    }
    double gf(double t) => 2.0 * n * n * n / (t * 1e6);
    double cpu = ms["CPU"];
    double mps = ms.GetValueOrDefault("MPS", double.NaN);
    Console.WriteLine($"{n,6} {cpu,10:F3} {gf(cpu),10:F1} {mps,10:F3} {gf(mps),10:F1} {cpu / mps,7:F1}x");
    matmul.Add(new { n, cpu_ms = cpu, mps_ms = mps });
}
Console.WriteLine();

// --- policy network latency (FR-T.3) ----------------------------------------------------------
// Gymnasium Hopper sizes: 11 observations, 3 actions, 2x64 Tanh MLP (CleanRL default).
const int Obs = 11, Act = 3;
var policy = new List<object>();
Console.WriteLine("policy MLP 11-64-64-3 Tanh (ms per step)");
Console.WriteLine($"{"device",6} {"batch",6} {"infer ms",10} {"train ms",10}");
foreach (var dev in devices) {
    foreach (int batch in new[] { 1, 64, 1024 }) {
        var net = nn.Sequential(
            ("l1", nn.Linear(Obs, 64)), ("t1", nn.Tanh()),
            ("l2", nn.Linear(64, 64)), ("t2", nn.Tanh()),
            ("l3", nn.Linear(64, Act))).to(dev);
        var opt = optim.Adam(net.parameters(), 3e-4);
        using var x = randn(batch, Obs, device: dev);
        using var y = randn(batch, Act, device: dev);

        double infer;
        using (no_grad()) infer = TimeMs(200, () => net.forward(x));
        double train = TimeMs(200, () => {
            opt.zero_grad();
            var loss = nn.functional.mse_loss(net.forward(x), y);
            loss.backward();
            opt.step();
            return loss;
        });
        Console.WriteLine($"{dev.type,6} {batch,6} {infer,10:F3} {train,10:F3}");
        policy.Add(new { device = dev.type.ToString(), batch, infer_ms = infer, train_ms = train });
        opt.Dispose();
        net.Dispose();
    }
}

if (writeBench) {
    var json = JsonSerializer.Serialize(new {
        created = DateTime.UtcNow.ToString("o"),
        os = RuntimeInformation.OSDescription,
        arch = RuntimeInformation.OSArchitecture.ToString(),
        cores = Environment.ProcessorCount,
        torch_threads = torch.get_num_threads(),
        matmul,
        policy,
    }, new JsonSerializerOptions { WriteIndented = true });
    File.WriteAllText("torch_bench.json", json);
    Console.WriteLine($"\nwrote {Path.GetFullPath("torch_bench.json")}");
}
