using System;

namespace Sokol.GUI;

/// <summary>
/// Animates a float value from <see cref="From"/> to <see cref="To"/> over <see cref="Duration"/> seconds.
/// </summary>
public sealed class Tween
{
    public float  From       { get; set; }
    public float  To         { get; set; }
    public float  Duration   { get; set; }   // seconds
    public float  Delay      { get; set; }   // seconds
    public bool   Loop       { get; set; }
    public bool   AutoReverse { get; set; }

    public Func<float, float> EasingFunc { get; set; } = Easing.Linear;
    public Action<float>?     OnUpdate   { get; set; }
    public Action?            OnComplete { get; set; }

    private float _elapsed;
    private bool  _running;
    private bool  _reversed;

    public float Value { get; private set; }
    public bool  IsRunning   => _running;
    public bool  IsCompleted { get; private set; }

    public void Start()
    {
        _elapsed   = 0;
        _running   = true;
        IsCompleted = false;
        _reversed  = false;
        Value      = From;
    }

    public void Stop()  { _running = false; }
    public void Reset() { _elapsed = 0; Value = From; _running = false; IsCompleted = false; }

    internal void Tick(float deltaSeconds)
    {
        if (!_running) return;

        _elapsed += deltaSeconds;
        float t2 = MathF.Max(0f, _elapsed - Delay);
        if (t2 <= 0) return;

        float t = Duration > 0 ? MathF.Min(t2 / Duration, 1f) : 1f;
        float eased = EasingFunc(_reversed ? 1f - t : t);
        Value = From + (To - From) * eased;
        OnUpdate?.Invoke(Value);

        if (t >= 1f)
        {
            if (AutoReverse && !_reversed)
            {
                _reversed = true;
                _elapsed  = Delay;
            }
            else if (Loop)
            {
                _reversed = false;
                _elapsed  = Delay;
            }
            else
            {
                _running    = false;
                IsCompleted = true;
                OnComplete?.Invoke();
            }
        }
    }
}
