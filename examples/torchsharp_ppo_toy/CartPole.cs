// Gymnasium CartPole-v1 dynamics in plain C# (Euler integration, 500-step time limit),
// vectorised over N independent environments with automatic reset.

public sealed class CartPoleVecEnv
{
    public const int ObsSize = 4, NumActions = 2, MaxSteps = 500;

    const float Gravity = 9.8f, MassCart = 1.0f, MassPole = 0.1f, TotalMass = MassCart + MassPole;
    const float Length = 0.5f, PoleMassLength = MassPole * Length, ForceMag = 10.0f, Tau = 0.02f;
    const float XThreshold = 2.4f, ThetaThreshold = 12f * 2f * MathF.PI / 360f;

    readonly Random _rng;
    readonly float[] _state;   // [N, 4]: x, x_dot, theta, theta_dot
    readonly int[] _steps;
    readonly float[] _episodeReturn;

    public int Count { get; }
    public float[] Obs { get; }            // [N, 4], observation after the last Step/Reset
    public float[] FinalObs { get; }       // [N, 4], last observation of an episode that just ended
    public bool[] Terminated { get; }      // pole fell or cart left the track
    public bool[] Truncated { get; }       // hit the time limit (episode cut, not failed)
    public List<float> CompletedReturns { get; } = new();

    public CartPoleVecEnv(int count, int seed)
    {
        Count = count;
        _rng = new Random(seed);
        _state = new float[count * ObsSize];
        _steps = new int[count];
        _episodeReturn = new float[count];
        Obs = new float[count * ObsSize];
        FinalObs = new float[count * ObsSize];
        Terminated = new bool[count];
        Truncated = new bool[count];
        for (int i = 0; i < count; i++) ResetEnv(i);
    }

    void ResetEnv(int i)
    {
        for (int k = 0; k < ObsSize; k++) {
            _state[i * ObsSize + k] = (float)(_rng.NextDouble() * 0.1 - 0.05);
            Obs[i * ObsSize + k] = _state[i * ObsSize + k];
        }
        _steps[i] = 0;
        _episodeReturn[i] = 0;
    }

    /// <summary>Advances every env by one step; reward is 1 per step, including the last.</summary>
    public void Step(ReadOnlySpan<long> actions, Span<float> rewards)
    {
        for (int i = 0; i < Count; i++) {
            int o = i * ObsSize;
            float x = _state[o], xDot = _state[o + 1], theta = _state[o + 2], thetaDot = _state[o + 3];
            float force = actions[i] == 1 ? ForceMag : -ForceMag;
            float cos = MathF.Cos(theta), sin = MathF.Sin(theta);
            float temp = (force + PoleMassLength * thetaDot * thetaDot * sin) / TotalMass;
            float thetaAcc = (Gravity * sin - cos * temp) / (Length * (4f / 3f - MassPole * cos * cos / TotalMass));
            float xAcc = temp - PoleMassLength * thetaAcc * cos / TotalMass;
            x += Tau * xDot;
            xDot += Tau * xAcc;
            theta += Tau * thetaDot;
            thetaDot += Tau * thetaAcc;
            _state[o] = x; _state[o + 1] = xDot; _state[o + 2] = theta; _state[o + 3] = thetaDot;

            rewards[i] = 1f;
            _steps[i]++;
            _episodeReturn[i] += 1f;
            Terminated[i] = x < -XThreshold || x > XThreshold || theta < -ThetaThreshold || theta > ThetaThreshold;
            Truncated[i] = !Terminated[i] && _steps[i] >= MaxSteps;

            for (int k = 0; k < ObsSize; k++) Obs[o + k] = _state[o + k];
            if (Terminated[i] || Truncated[i]) {
                for (int k = 0; k < ObsSize; k++) FinalObs[o + k] = _state[o + k];
                CompletedReturns.Add(_episodeReturn[i]);
                ResetEnv(i);
            }
        }
    }
}
