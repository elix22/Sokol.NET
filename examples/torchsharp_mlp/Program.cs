// torchsharp_mlp — fits y = sin(x) + noise with a small MLP on CPU and MPS, then checks a
// save/load round trip reproduces the outputs exactly. Exit code 0 = all checks passed.
// Usage: dotnet run -c Release

using System.Diagnostics;
using TorchSharp;
using static TorchSharp.torch;

const int Samples = 2048, Steps = 3000;
const double NoiseStd = 0.05, MaxTestMse = 0.01;

var devices = new List<Device> { CPU };
if (torch.mps_is_available()) devices.Add(MPS);

bool ok = true;
foreach (var dev in devices) {
    torch.manual_seed(1234);
    using var scope = NewDisposeScope();

    // Data is generated on the CPU so both devices train on the same samples.
    var x = (rand(Samples, 1) * 2 - 1) * Math.PI;
    var y = sin(x) + randn(Samples, 1) * NoiseStd;
    var xTest = linspace(-Math.PI, Math.PI, 512).unsqueeze(1);
    var yTest = sin(xTest);
    x = x.to(dev); y = y.to(dev); xTest = xTest.to(dev); yTest = yTest.to(dev);

    var net = MakeNet().to(dev);
    var opt = optim.Adam(net.parameters(), 3e-3);
    var sw = Stopwatch.StartNew();
    Console.WriteLine($"[{dev.type}] training {Steps} full-batch steps on {Samples} samples");
    for (int step = 1; step <= Steps; step++) {
        using var stepScope = NewDisposeScope();
        opt.zero_grad();
        var loss = nn.functional.mse_loss(net.forward(x), y);
        loss.backward();
        opt.step();
        if (step == 1 || step % 500 == 0)
            Console.WriteLine($"[{dev.type}]   step {step,5}  train mse {loss.item<float>():F5}");
    }

    double testMse;
    using (no_grad()) testMse = nn.functional.mse_loss(net.forward(xTest), yTest).item<float>();
    bool fit = testMse < MaxTestMse;
    Console.WriteLine($"[{dev.type}] test mse vs clean sin(x) = {testMse:F5} (< {MaxTestMse}) {(fit ? "PASS" : "FAIL")}  [{sw.Elapsed.TotalSeconds:F1} s]");

    // Round trip: save, load into a freshly initialised net, compare outputs on the same device.
    string path = Path.Combine(Path.GetTempPath(), $"torchsharp_mlp_{dev.type}.dat");
    net.save(path);
    var loaded = MakeNet();
    loaded.load(path);
    loaded.to(dev);
    float maxDiff;
    using (no_grad()) maxDiff = (net.forward(xTest) - loaded.forward(xTest)).abs().max().item<float>();
    bool same = maxDiff == 0f;
    Console.WriteLine($"[{dev.type}] save/load round trip max |diff| = {maxDiff} {(same ? "PASS" : "FAIL")}  ({new FileInfo(path).Length} bytes)");
    File.Delete(path);

    opt.Dispose();
    net.Dispose();
    loaded.Dispose();
    ok &= fit && same;
    Console.WriteLine();
}

Console.WriteLine(ok ? "ALL PASS" : "FAILED");
return ok ? 0 : 1;

static nn.Module<Tensor, Tensor> MakeNet() => nn.Sequential(
    ("l1", nn.Linear(1, 64)), ("t1", nn.Tanh()),
    ("l2", nn.Linear(64, 64)), ("t2", nn.Tanh()),
    ("l3", nn.Linear(64, 1)));
