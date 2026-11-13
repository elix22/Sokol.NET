// AnimationClip.cs — a clip of float curves bound by (path, component, property), and its events: Unity's model
// (docs: ANIMATION_SYSTEM_IMPLEMENTATION_PLAN.md §17).

using System.Collections.Generic;

namespace GameEditor.Framework.Animation
{
    /// <summary>
    /// What a curve animates: the entity at <see cref="Path"/> below the animated one (child names joined by '/', "" = the
    /// animated entity itself), its <see cref="Component"/> ("Transform", "Light", "Camera") and <see cref="Property"/>
    /// ("LocalPosition.x", "Intensity", "Color.r", ...) — see <see cref="PropertyBinder"/>.
    /// </summary>
    // A plain readonly struct, not a record struct: a record struct copied out of an array by a variable index crashed a
    // NativeAOT release build (Jambolaya, 2026-09-08).
    public readonly struct CurveBinding : System.IEquatable<CurveBinding>
    {
        public readonly string Path;
        public readonly string Component;
        public readonly string Property;

        public CurveBinding(string path, string component, string property) { Path = path; Component = component; Property = property; }

        public bool Equals(CurveBinding o) => Path == o.Path && Component == o.Component && Property == o.Property;
        public override bool Equals(object? obj) => obj is CurveBinding o && Equals(o);
        public override int GetHashCode() => System.HashCode.Combine(Path, Component, Property);
        public static bool operator ==(CurveBinding a, CurveBinding b) => a.Equals(b);
        public static bool operator !=(CurveBinding a, CurveBinding b) => !a.Equals(b);
        public override string ToString() => $"{Path}/{Component}.{Property}";
    }

    /// <summary>A function of the animated entity's scripts called when the clip's time passes <see cref="Time"/>.</summary>
    public struct AnimationEvent
    {
        public float Time;
        public string FunctionName;
        public string StringParameter;
        public float FloatParameter;
        public int IntParameter;
    }

    public sealed class AnimationClip
    {
        public string Name = "";
        /// <summary>Seconds; at least the last key's time.</summary>
        public float Length;
        /// <summary>The editor's grid of frames (Unity's 60 by default) — curves are evaluated at any time, not sampled.</summary>
        public float FrameRate = 60f;
        /// <summary>What a player does at the end: stop (Once), go round (Loop), back and forth (PingPong), hold (ClampForever).</summary>
        public WrapMode Wrap = WrapMode.Once;
        public readonly List<(CurveBinding Binding, AnimationCurve Curve)> Curves = new();
        public readonly List<AnimationEvent> Events = new();

        /// <summary>Puts <paramref name="curve"/> on the binding (replacing one there); the length grows to its last key.</summary>
        public void SetCurve(string path, string component, string property, AnimationCurve curve)
        {
            var b = new CurveBinding(path, component, property);
            int i = Curves.FindIndex(c => c.Binding == b);
            if (i >= 0) Curves[i] = (b, curve); else Curves.Add((b, curve));
            if (curve.Length > 0 && curve[curve.Length - 1].Time > Length) Length = curve[curve.Length - 1].Time;
        }

        public AnimationCurve? GetCurve(string path, string component, string property)
        {
            var b = new CurveBinding(path, component, property);
            foreach (var c in Curves) if (c.Binding == b) return c.Curve;
            return null;
        }
    }
}
