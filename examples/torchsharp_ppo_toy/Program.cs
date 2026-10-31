// torchsharp_ppo_toy — PPO on a pure-C# CartPole-v1, trained on CPU and/or MPS.
// Solved = mean return of the last 100 training episodes >= 475 within the time budget;
// afterwards 100 greedy evaluation episodes are reported too. Exit code 0 = solved everywhere.
// Usage: dotnet run -c Release [-- --device cpu|mps|both] [--seed N]

using System.Diagnostics;
using TorchSharp;
using static TorchSharp.torch;

const double SolveThreshold = 475, TimeBudgetSeconds = 120;

string deviceArg = ArgValue("--device") ?? "both";
int seed = int.Parse(ArgValue("--seed") ?? "1");

var devices = new List<Device>();
if (deviceArg is "cpu" or "both") devices.Add(CPU);
if (deviceArg is "mps" or "both") {
    if (torch.mps_is_available()) devices.Add(MPS);
    else Console.WriteLine("MPS not available, skipping");
}

var cfg = new PpoConfig();
bool allSolved = true;
foreach (var dev in devices) {
    var (solved, evalMean) = Train(dev, cfg, seed);
    allSolved &= solved && evalMean >= SolveThreshold;
    Console.WriteLine();
}
Console.WriteLine(allSolved ? "ALL PASS" : "FAILED");
return allSolved ? 0 : 1;

(bool solved, double evalMean) Train(Device dev, PpoConfig cfg, int seed)
{
    torch.manual_seed(seed);
    const int O = CartPoleVecEnv.ObsSize;
    int n = cfg.NumEnvs, T = cfg.StepsPerEnv;
    var env = new CartPoleVecEnv(n, seed);
    using var ppo = new Ppo(new ActorCritic(O, CartPoleVecEnv.NumActions, cfg.Hidden), O, cfg, dev);
    var buf = new RolloutBuffer(T, n, O);
    var truncObs = new float[n * O];

    Console.WriteLine($"[{dev.type}] PPO CartPole-v1: {n} envs x {T} steps, {cfg.Epochs} epochs, minibatch {cfg.MinibatchSize}, budget {TimeBudgetSeconds} s");
    var sw = Stopwatch.StartNew();
    long steps = 0;
    int update = 0;
    double rolling = 0;
    bool solved = false;
    while (steps < cfg.TotalSteps && sw.Elapsed.TotalSeconds < TimeBudgetSeconds) {
        for (int t = 0; t < T; t++) {
            int k = t * n;
            Array.Copy(env.Obs, 0, buf.Obs, k * O, n * O);
            ppo.Act(env.Obs, n, buf.Actions.AsSpan(k, n), buf.LogProbs.AsSpan(k, n), buf.Values.AsSpan(k, n));
            env.Step(buf.Actions.AsSpan(k, n), buf.Rewards.AsSpan(k, n));

            // A time-limit cut is not a failure: bootstrap with V(final obs) instead of 0.
            int truncated = 0;
            for (int i = 0; i < n; i++) {
                buf.Dones[k + i] = env.Terminated[i] || env.Truncated[i];
                if (env.Truncated[i]) Array.Copy(env.FinalObs, i * O, truncObs, truncated++ * O, O);
            }
            if (truncated > 0) {
                var v = ppo.Values(truncObs, truncated);
                for (int i = 0, j = 0; i < n; i++)
                    if (env.Truncated[i]) buf.Rewards[k + i] += cfg.Gamma * v[j++];
            }
        }
        steps += T * n;
        update++;

        var stats = ppo.Update(buf, ppo.Values(env.Obs, n), 1.0 - (double)steps / cfg.TotalSteps);

        var done = env.CompletedReturns;
        if (done.Count >= 100) {
            rolling = done.Skip(done.Count - 100).Average();
            solved = rolling >= SolveThreshold;
        }
        if (update % 25 == 0 || solved)
            Console.WriteLine($"[{dev.type}]   update {update,4}  steps {steps,7}  episodes {done.Count,5}  last-100 mean {rolling,6:F1}  " +
                              $"ent {stats.Entropy:F3}  kl {stats.ApproxKl:F4}  clip {stats.ClipFrac:F3}  {steps / sw.Elapsed.TotalSeconds,6:F0} steps/s");
        if (solved) break;
    }
    double seconds = sw.Elapsed.TotalSeconds;
    Console.WriteLine($"[{dev.type}] {(solved ? "SOLVED" : "NOT SOLVED")}: last-100 mean {rolling:F1} after {steps} steps in {seconds:F1} s");

    // Greedy evaluation: 100 envs, each contributes its first episode.
    var evalEnv = new CartPoleVecEnv(100, seed + 1000);
    var actions = new long[100];
    var rewards = new float[100];
    var finished = new bool[100];
    int remaining = 100;
    var evalReturn = new double[100];
    while (remaining > 0) {
        ppo.ActGreedy(evalEnv.Obs, 100, actions);
        evalEnv.Step(actions, rewards);
        for (int i = 0; i < 100; i++) {
            if (finished[i]) continue;
            evalReturn[i] += rewards[i];
            if (evalEnv.Terminated[i] || evalEnv.Truncated[i]) { finished[i] = true; remaining--; }
        }
    }
    double evalMean = evalReturn.Average();
    Console.WriteLine($"[{dev.type}] greedy eval over 100 episodes: mean {evalMean:F1}, min {evalReturn.Min():F0}");
    return (solved, evalMean);
}

string? ArgValue(string name)
{
    int i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}
