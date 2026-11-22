// CGltfBone.cs — ported as-is from examples/CGltfViewer/Source/CGltfBone.cs (namespace only;
// dropped the unused `using static Sokol.SLog;`). Stores raw keyframe arrays + interpolates inline.
using System.Numerics;

namespace GameEditor.Framework.Renderer.Server.Animation
{
    public enum CGltfInterpolation
    {
        Linear,
        Step,
        CubicSpline
    }

    public class CGltfBone
    {
        // Keyframe arrays (sorted by time)
        private float[]? _transKeys;
        private Vector3[]? _transValues;
        private CGltfInterpolation _transInterp;

        private float[]? _rotKeys;
        private Quaternion[]? _rotValues;
        private CGltfInterpolation _rotInterp;

        private float[]? _scaleKeys;
        private Vector3[]? _scaleValues;
        private CGltfInterpolation _scaleInterp;

        // Cached result from last Update()
        private Vector3 _translation;
        private Quaternion _rotation = Quaternion.Identity;
        private Vector3 _scale = Vector3.One;

        // Which channels are actually present
        private bool _hasTranslation;
        private bool _hasRotation;
        private bool _hasScale;

        public string Name { get; private set; }
        public int ID { get; private set; }
        public Matrix4x4 LocalTransform { get; private set; } = Matrix4x4.Identity;

        public CGltfBone(string name, int id)
        {
            Name = name;
            ID = id;
        }

        /// <summary>The node's own (rest) transform: a channel the clip does not have keeps it (glTF: an unanimated property
        /// stays the node's). gltfpack drops every track equal to the rest pose; without this such a bone sat at zero.</summary>
        public void SetRest(Vector3 translation, Quaternion rotation, Vector3 scale)
        {
            _translation = _restTranslation = translation;
            _rotation    = _restRotation    = rotation;
            _scale       = _restScale       = scale;
        }

        private Vector3 _restTranslation;
        private Quaternion _restRotation = Quaternion.Identity;
        private Vector3 _restScale = Vector3.One;

        public void SetTranslationKeys(float[] times, Vector3[] values, CGltfInterpolation interp)
        {
            _transKeys = times;
            _transValues = values;
            _transInterp = interp;
            _hasTranslation = times.Length > 0;
        }

        public void SetRotationKeys(float[] times, Quaternion[] values, CGltfInterpolation interp)
        {
            _rotKeys = times;
            _rotValues = values;
            _rotInterp = interp;
            _hasRotation = times.Length > 0;
        }

        public void SetScaleKeys(float[] times, Vector3[] values, CGltfInterpolation interp)
        {
            _scaleKeys = times;
            _scaleValues = values;
            _scaleInterp = interp;
            _hasScale = times.Length > 0;
        }

        public void Update(float time)
        {
            if (_hasTranslation) _translation = SampleVec3(_transKeys!, _transValues!, time, _transInterp);
            if (_hasRotation)    _rotation    = SampleQuat(_rotKeys!, _rotValues!, time, _rotInterp);
            if (_hasScale)       _scale       = SampleVec3(_scaleKeys!, _scaleValues!, time, _scaleInterp);

            LocalTransform = Matrix4x4.CreateScale(_scale) *
                             Matrix4x4.CreateFromQuaternion(_rotation) *
                             Matrix4x4.CreateTranslation(_translation);
        }

        public Matrix4x4 GetLocalTransform() => LocalTransform;

        public void GetAnimatedChannels(out bool hasT, out bool hasR, out bool hasS,
                                         out Vector3 t, out Quaternion r, out Vector3 s)
        {
            hasT = _hasTranslation; t = _translation;
            hasR = _hasRotation;    r = _rotation;
            hasS = _hasScale;       s = _scale;
        }

        public CGltfBone Clone(int newId)
        {
            var clone = new CGltfBone(Name, newId);
            clone.SetRest(_restTranslation, _restRotation, _restScale);
            if (_hasTranslation) clone.SetTranslationKeys(_transKeys!, _transValues!, _transInterp);
            if (_hasRotation)    clone.SetRotationKeys(_rotKeys!, _rotValues!, _rotInterp);
            if (_hasScale)       clone.SetScaleKeys(_scaleKeys!, _scaleValues!, _scaleInterp);
            return clone;
        }

        // -----------------------------------------------------------------------
        // Interpolation helpers
        // -----------------------------------------------------------------------

        private static Vector3 SampleVec3(float[] times, Vector3[] values, float t, CGltfInterpolation interp)
        {
            if (values.Length == 0) return Vector3.Zero;
            if (values.Length == 1) return values[0];

            // Clamp
            if (t <= times[0]) return values[0];
            if (t >= times[^1]) return values[^1];

            int i = FindKeyIndex(times, t);

            if (interp == CGltfInterpolation.Step)
                return values[i];

            float dt = times[i + 1] - times[i];
            float alpha = dt < 1e-7f ? 0f : (t - times[i]) / dt;

            if (interp == CGltfInterpolation.CubicSpline)
            {
                // CubicSpline stores [inTangent, value, outTangent] per keyframe
                int stride = 3;
                var p0 = values[i * stride + 1];
                var m0 = dt * values[i * stride + 2];
                var p1 = values[(i + 1) * stride + 1];
                var m1 = dt * values[(i + 1) * stride];
                return CubicHermiteVec3(p0, m0, p1, m1, alpha);
            }

            return Vector3.Lerp(values[i], values[i + 1], alpha);
        }

        private static Quaternion SampleQuat(float[] times, Quaternion[] values, float t, CGltfInterpolation interp)
        {
            if (values.Length == 0) return Quaternion.Identity;
            if (values.Length == 1) return values[0];

            if (t <= times[0]) return values[0];
            if (t >= times[^1]) return values[^1];

            int i = FindKeyIndex(times, t);

            if (interp == CGltfInterpolation.Step)
                return values[i];

            float dt = times[i + 1] - times[i];
            float alpha = dt < 1e-7f ? 0f : (t - times[i]) / dt;

            if (interp == CGltfInterpolation.CubicSpline)
            {
                int stride = 3;
                var p0 = Quaternion.Normalize(values[i * stride + 1]);
                var m0 = Scale(values[i * stride + 2], dt);
                var p1 = Quaternion.Normalize(values[(i + 1) * stride + 1]);
                var m1 = Scale(values[(i + 1) * stride], dt);
                return Quaternion.Normalize(CubicHermiteQuat(p0, m0, p1, m1, alpha));
            }

            return Quaternion.Normalize(Quaternion.Slerp(values[i], values[i + 1], alpha));
        }

        private static int FindKeyIndex(float[] times, float t)
        {
            // Binary search
            int lo = 0, hi = times.Length - 2;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) >> 1;
                if (times[mid] <= t) lo = mid; else hi = mid - 1;
            }
            return lo;
        }

        private static Vector3 CubicHermiteVec3(Vector3 p0, Vector3 m0, Vector3 p1, Vector3 m1, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            float h00 = 2*t3 - 3*t2 + 1;
            float h10 = t3 - 2*t2 + t;
            float h01 = -2*t3 + 3*t2;
            float h11 = t3 - t2;
            return h00*p0 + h10*m0 + h01*p1 + h11*m1;
        }

        private static Quaternion CubicHermiteQuat(Quaternion p0, Quaternion m0, Quaternion p1, Quaternion m1, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            float h00 = 2*t3 - 3*t2 + 1;
            float h10 = t3 - 2*t2 + t;
            float h01 = -2*t3 + 3*t2;
            float h11 = t3 - t2;
            return new Quaternion(
                h00*p0.X + h10*m0.X + h01*p1.X + h11*m1.X,
                h00*p0.Y + h10*m0.Y + h01*p1.Y + h11*m1.Y,
                h00*p0.Z + h10*m0.Z + h01*p1.Z + h11*m1.Z,
                h00*p0.W + h10*m0.W + h01*p1.W + h11*m1.W
            );
        }

        private static Quaternion Scale(Quaternion q, float s)
            => new Quaternion(q.X * s, q.Y * s, q.Z * s, q.W * s);
    }
}
