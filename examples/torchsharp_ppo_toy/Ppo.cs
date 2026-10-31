// Clipped PPO with GAE for a discrete-action actor-critic (CleanRL / SB3 formulation).
// Written as plain code here first; it moves to Sokol.Robotics (with Gaussian actions,
// normalisers and checkpoints) in plan step 3g.1.

using TorchSharp;
using TorchSharp.Modules;
using static TorchSharp.torch;

public sealed class PpoConfig
{
    public int NumEnvs { get; init; } = 8;
    public int StepsPerEnv { get; init; } = 32;        // rollout length per update
    public int MinibatchSize { get; init; } = 256;
    public int Epochs { get; init; } = 20;
    public double LearningRate { get; init; } = 1e-3;  // linearly annealed to 0 over TotalSteps
    public long TotalSteps { get; init; } = 200_000;
    public float Gamma { get; init; } = 0.98f;
    public float GaeLambda { get; init; } = 0.8f;
    public double ClipRange { get; init; } = 0.2;
    public bool ClipValueLoss { get; init; } = false;
    public double EntropyCoef { get; init; } = 0.0;
    public double ValueCoef { get; init; } = 0.5;
    public double MaxGradNorm { get; init; } = 0.5;
    public int Hidden { get; init; } = 64;
}

/// <summary>Separate actor and critic MLPs (Tanh), orthogonal init as in SB3/CleanRL.</summary>
public sealed class ActorCritic : nn.Module<Tensor, (Tensor logits, Tensor value)>
{
    readonly Sequential actor, critic;

    public ActorCritic(int obsSize, int numActions, int hidden) : base(nameof(ActorCritic))
    {
        actor = nn.Sequential(
            Layer(obsSize, hidden, Math.Sqrt(2)), nn.Tanh(),
            Layer(hidden, hidden, Math.Sqrt(2)), nn.Tanh(),
            Layer(hidden, numActions, 0.01));
        critic = nn.Sequential(
            Layer(obsSize, hidden, Math.Sqrt(2)), nn.Tanh(),
            Layer(hidden, hidden, Math.Sqrt(2)), nn.Tanh(),
            Layer(hidden, 1, 1.0));
        RegisterComponents();
    }

    static Linear Layer(int inSize, int outSize, double gain)
    {
        var l = nn.Linear(inSize, outSize);
        using (no_grad()) {
            nn.init.orthogonal_(l.weight!, gain);
            nn.init.zeros_(l.bias!);
        }
        return l;
    }

    public override (Tensor logits, Tensor value) forward(Tensor obs) =>
        (actor.forward(obs), critic.forward(obs).squeeze(-1));

    protected override void Dispose(bool disposing)
    {
        if (disposing) { actor.Dispose(); critic.Dispose(); }
        base.Dispose(disposing);
    }
}

/// <summary>One rollout, time-major [T, N], kept on the CPU.</summary>
public sealed class RolloutBuffer
{
    public readonly int T, N, ObsSize;
    public readonly float[] Obs, LogProbs, Rewards, Values;
    public readonly long[] Actions;
    public readonly bool[] Dones;   // transition t ended the episode (terminated or truncated)

    public RolloutBuffer(int t, int n, int obsSize)
    {
        T = t; N = n; ObsSize = obsSize;
        Obs = new float[t * n * obsSize];
        LogProbs = new float[t * n];
        Rewards = new float[t * n];
        Values = new float[t * n];
        Actions = new long[t * n];
        Dones = new bool[t * n];
    }
}

public readonly record struct PpoStats(float PolicyLoss, float ValueLoss, float Entropy, float ApproxKl, float ClipFrac);

public sealed class Ppo : IDisposable
{
    readonly ActorCritic _net;
    readonly optim.Optimizer _opt;
    readonly PpoConfig _cfg;
    readonly Device _device;
    readonly int _obsSize;

    public Ppo(ActorCritic net, int obsSize, PpoConfig cfg, Device device)
    {
        _net = net.to(device);
        _obsSize = obsSize;
        _cfg = cfg;
        _device = device;
        _opt = optim.Adam(_net.parameters(), cfg.LearningRate, eps: 1e-5);
    }

    Tensor ObsTensor(float[] obs, int n) => tensor(obs, new long[] { n, _obsSize }).to(_device);

    /// <summary>Samples actions for n observations; one device→CPU copy per call.</summary>
    public void Act(float[] obs, int n, Span<long> actions, Span<float> logProbs, Span<float> values)
    {
        using var scope = NewDisposeScope();
        using var noGrad = no_grad();
        var (logits, value) = _net.forward(ObsTensor(obs, n));
        var logpAll = logits.log_softmax(-1);
        var a = logpAll.exp().multinomial(1);
        var lp = logpAll.gather(-1, a).squeeze(-1);
        var packed = cat(new[] { a.squeeze(-1).to(float32), lp, value }).cpu().data<float>().ToArray();
        for (int i = 0; i < n; i++) {
            actions[i] = (long)packed[i];
            logProbs[i] = packed[n + i];
            values[i] = packed[2 * n + i];
        }
    }

    public void ActGreedy(float[] obs, int n, Span<long> actions)
    {
        using var scope = NewDisposeScope();
        using var noGrad = no_grad();
        var a = _net.forward(ObsTensor(obs, n)).logits.argmax(-1).cpu().data<long>().ToArray();
        a.CopyTo(actions);
    }

    public float[] Values(float[] obs, int n)
    {
        using var scope = NewDisposeScope();
        using var noGrad = no_grad();
        return _net.forward(ObsTensor(obs, n)).value.cpu().data<float>().ToArray();
    }

    /// <param name="lastValues">V(s_T) per env, bootstraps the end of the rollout.</param>
    /// <param name="lrFrac">Remaining fraction of training, scales the learning rate.</param>
    public PpoStats Update(RolloutBuffer buf, float[] lastValues, double lrFrac)
    {
        int n = buf.N, b = buf.T * buf.N;
        foreach (var g in _opt.ParamGroups) g.LearningRate = _cfg.LearningRate * lrFrac;

        // GAE(λ) on the CPU (a few hundred elements).
        var adv = new float[b];
        var ret = new float[b];
        for (int i = 0; i < n; i++) {
            float lastGae = 0;
            for (int t = buf.T - 1; t >= 0; t--) {
                int k = t * n + i;
                float nextV = t == buf.T - 1 ? lastValues[i] : buf.Values[k + n];
                float nonTerminal = buf.Dones[k] ? 0f : 1f;
                float delta = buf.Rewards[k] + _cfg.Gamma * nextV * nonTerminal - buf.Values[k];
                lastGae = delta + _cfg.Gamma * _cfg.GaeLambda * nonTerminal * lastGae;
                adv[k] = lastGae;
                ret[k] = lastGae + buf.Values[k];
            }
        }

        using var scope = NewDisposeScope();
        var bObs = ObsTensor(buf.Obs, b);
        var bAct = tensor(buf.Actions).to(_device);
        var bLogp = tensor(buf.LogProbs).to(_device);
        var bVal = tensor(buf.Values).to(_device);
        var bAdv = tensor(adv).to(_device);
        var bRet = tensor(ret).to(_device);

        Tensor pgLoss = null!, vLoss = null!, entropy = null!, approxKl = null!, clipFrac = null!;
        for (int epoch = 0; epoch < _cfg.Epochs; epoch++) {
            var perm = randperm(b, device: _device);
            for (int start = 0; start < b; start += _cfg.MinibatchSize) {
                var idx = perm.narrow(0, start, Math.Min(_cfg.MinibatchSize, b - start));
                var mbAdv = bAdv.index_select(0, idx);
                mbAdv = (mbAdv - mbAdv.mean()) / (mbAdv.std() + 1e-8);

                var (logits, newValue) = _net.forward(bObs.index_select(0, idx));
                var logpAll = logits.log_softmax(-1);
                var newLogp = logpAll.gather(-1, bAct.index_select(0, idx).unsqueeze(-1)).squeeze(-1);
                var logRatio = newLogp - bLogp.index_select(0, idx);
                var ratio = logRatio.exp();

                pgLoss = max(-mbAdv * ratio, -mbAdv * ratio.clamp(1 - _cfg.ClipRange, 1 + _cfg.ClipRange)).mean();

                var mbRet = bRet.index_select(0, idx);
                if (_cfg.ClipValueLoss) {
                    var oldV = bVal.index_select(0, idx);
                    var vClipped = oldV + (newValue - oldV).clamp(-_cfg.ClipRange, _cfg.ClipRange);
                    vLoss = 0.5 * max((newValue - mbRet).pow(2), (vClipped - mbRet).pow(2)).mean();
                } else {
                    vLoss = 0.5 * (newValue - mbRet).pow(2).mean();
                }
                entropy = -(logpAll.exp() * logpAll).sum(-1).mean();

                var loss = pgLoss - _cfg.EntropyCoef * entropy + _cfg.ValueCoef * vLoss;
                _opt.zero_grad();
                loss.backward();
                nn.utils.clip_grad_norm_(_net.parameters(), _cfg.MaxGradNorm);
                _opt.step();

                using (no_grad()) {
                    approxKl = ((ratio - 1) - logRatio).mean();
                    clipFrac = ((ratio - 1).abs() > _cfg.ClipRange).to(float32).mean();
                }
            }
        }
        var stats = stack(new[] { pgLoss.detach(), vLoss.detach(), entropy.detach(), approxKl, clipFrac }).cpu().data<float>().ToArray();
        return new PpoStats(stats[0], stats[1], stats[2], stats[3], stats[4]);
    }

    public void Dispose()
    {
        _opt.Dispose();
        _net.Dispose();
    }
}
