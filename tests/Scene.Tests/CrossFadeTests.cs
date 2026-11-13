using System;
using System.Numerics;
using GameEditor.Framework.Renderer.Server.Animation;
using Xunit;

namespace Scene.Tests
{
    /// <summary>
    /// G6 A6 (ANIMATION_SYSTEM_IMPLEMENTATION_PLAN.md §22): a crossfade blends each bone's local translation, rotation and
    /// scale — translation and scale lerped, rotation slerped — by a weight that rises smoothly from 0 to 1.
    /// </summary>
    public sealed class CrossFadeTests
    {
        static void Near(float want, float got, float tol = 1e-4f) => Assert.True(MathF.Abs(want - got) <= tol, $"want {want}, got {got}");

        static Matrix4x4 Local(Vector3 t, Quaternion r, Vector3 s) => Matrix4x4.CreateScale(s) * Matrix4x4.CreateFromQuaternion(r) * Matrix4x4.CreateTranslation(t);

        [Fact]
        public void Blend_Translation_And_Scale_Lerped()
        {
            var a = Local(Vector3.Zero, Quaternion.Identity, Vector3.One);
            var b = Local(new Vector3(2, 0, -4), Quaternion.Identity, new Vector3(3, 3, 3));
            Assert.True(Matrix4x4.Decompose(CGltfAnimator.Blend(a, b, 0.5f), out var s, out _, out var t));
            Near(1, t.X); Near(-2, t.Z); Near(2, s.X);
            Assert.Equal(a, CGltfAnimator.Blend(a, b, 0f));                    // the ends are the clips' own
            Assert.True(Matrix4x4.Decompose(CGltfAnimator.Blend(a, b, 1f), out _, out _, out var t1));
            Near(2, t1.X);
        }

        [Fact]
        public void Blend_Rotation_Slerped_NotLerped()
        {
            // A half turn about Y blended half way: a quarter turn, the bone still its own length (a matrix lerp would shrink it to nothing).
            var a = Local(Vector3.Zero, Quaternion.Identity, Vector3.One);
            var b = Local(Vector3.Zero, Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI * 0.999f), Vector3.One);
            var m = CGltfAnimator.Blend(a, b, 0.5f);
            Assert.True(Matrix4x4.Decompose(m, out var s, out var r, out _));
            Near(1, s.X, 1e-3f); Near(1, s.Z, 1e-3f);
            var x = Vector3.Transform(Vector3.UnitX, r);
            Near(0, x.X, 2e-3f);                                              // +X turned a quarter: to ∓Z
            Near(1, MathF.Abs(x.Z), 2e-3f);
        }

        [Fact]
        public void Weight_RisesSmoothly_FromZeroToOne()
        {
            Near(0, CGltfAnimator.FadeWeightAt(0f));
            Near(0.5f, CGltfAnimator.FadeWeightAt(0.5f));
            Near(1, CGltfAnimator.FadeWeightAt(1f));
            Near(1, CGltfAnimator.FadeWeightAt(3f));
            float last = 0f;
            for (float x = 0f; x <= 1f; x += 0.01f) { float w = CGltfAnimator.FadeWeightAt(x); Assert.True(w >= last); last = w; }
            Assert.True(CGltfAnimator.FadeWeightAt(0.05f) < 0.05f);         // eased in: no step at the start
        }
    }
}
