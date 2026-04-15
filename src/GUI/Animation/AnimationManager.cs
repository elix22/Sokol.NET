using System;
using System.Collections.Generic;
using static Sokol.STM;

namespace Sokol.GUI;

/// <summary>
/// Per-frame ticker for all active <see cref="Tween"/> objects.
/// Use <see cref="Instance"/> (set by <see cref="Screen.Initialize"/>).
/// </summary>
public sealed class AnimationManager
{
    public static AnimationManager? Instance { get; internal set; }

    private readonly List<Tween> _tweens = [];
    private double               _lastTime;

    public AnimationManager() => Instance = this;

    public void Register(Tween tween)
    {
        if (!_tweens.Contains(tween)) _tweens.Add(tween);
    }

    public void Unregister(Tween tween) => _tweens.Remove(tween);

    public void Update()
    {
        double now   = stm_sec(stm_now());
        float  delta = (float)(now - _lastTime);
        _lastTime    = now;

        // Guard against large gaps (app was backgrounded, first frame, etc.).
        if (delta > 0.1f) delta = 0.016f;

        for (int i = _tweens.Count - 1; i >= 0; i--)
        {
            var t = _tweens[i];
            t.Tick(delta);
            if (t.IsCompleted && !t.Loop) _tweens.RemoveAt(i);
        }
    }

    /// <summary>Convenience: create, register, and start a tween in one call.</summary>
    public Tween Animate(float from, float to, float duration,
        Action<float>     onUpdate,
        Action?           onComplete = null,
        Func<float,float>? easing    = null,
        float             delay      = 0f,
        bool              loop       = false)
    {
        var t = new Tween
        {
            From         = from,
            To           = to,
            Duration     = duration,
            Delay        = delay,
            Loop         = loop,
            EasingFunc   = easing ?? Easing.EaseInOutQuad,
            OnUpdate     = onUpdate,
            OnComplete   = onComplete,
        };
        Register(t);
        t.Start();
        return t;
    }
}
