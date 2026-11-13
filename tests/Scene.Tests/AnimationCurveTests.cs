using System;
using System.Collections.Generic;
using System.Numerics;
using GameEditor.Framework.Animation;
using GameEditor.Framework.Core;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;
using GameEditor.Framework.Renderer.Server.Animation;
using GameEditor.Framework.Scene;
using Xunit;

namespace Scene.Tests
{
    /// <summary>
    /// G6 A1 (ANIMATION_SYSTEM_IMPLEMENTATION_PLAN.md §17): the animation data — keyframes with tangent modes, a float
    /// curve evaluated by cubic Hermite with wrap modes, a clip of curves bound by (path, component, property), events, the
    /// `.anim.json` file, and the binder writing a clip into an entity's hierarchy.
    /// </summary>
    public sealed class AnimationCurveTests : IDisposable
    {
        static readonly ECSWorld World = ECSWorld.Instance;
        readonly List<string> _warnings = new();

        public AnimationCurveTests()
        {
            World.Clear();
            SceneManager.NewScene("Curves");
            Logger.OnLog += OnLog;
        }

        public void Dispose() => Logger.OnLog -= OnLog;

        void OnLog(LogLevel level, string msg)
        {
            if (level == LogLevel.Warning) _warnings.Add(msg);
        }

        static void Near(float want, float got, float tol = 1e-4f) => Assert.True(MathF.Abs(want - got) <= tol, $"want {want}, got {got}");

        // ── evaluating ───────────────────────────────────────────────────────────────

        [Fact]
        public void Hermite_MatchesHandWorkedValues()
        {
            // 0 → 10 over 2 s, out tangent 3 /s, in tangent -1 /s: h00·0 + h10·3·2 + h01·10 + h11·(-1)·2 at s = 0.25.
            var c = new AnimationCurve(new Keyframe(0, 0, 0, 3), new Keyframe(2, 10, -1, 0));
            float s = 0.25f, s2 = s * s, s3 = s2 * s;
            float want = (s3 - 2 * s2 + s) * 6 + (-2 * s3 + 3 * s2) * 10 + (s3 - s2) * -2;
            Near(want, c.Evaluate(0.5f));
            Near(0, c.Evaluate(0));
            Near(10, c.Evaluate(2));
        }

        [Fact]
        public void Hermite_IsCGltfBonesCubicSpline()
        {
            // The same keys as a glTF cubic-spline translation (in, value, out per key): the bone's x equals the curve.
            float[] times = { 0f, 0.7f, 1.5f, 2f };
            float[] values = { 0f, 2f, -1f, 3f };
            float[] ins = { 0f, 1.5f, -4f, 2f };
            float[] outs = { 5f, 0.5f, 1f, 0f };
            var keys = new Keyframe[4];
            var spline = new Vector3[12];
            for (int i = 0; i < 4; i++)
            {
                keys[i] = new Keyframe(times[i], values[i], ins[i], outs[i]);
                spline[i * 3] = new Vector3(ins[i], 0, 0);
                spline[i * 3 + 1] = new Vector3(values[i], 0, 0);
                spline[i * 3 + 2] = new Vector3(outs[i], 0, 0);
            }
            var curve = new AnimationCurve(keys);
            var bone = new CGltfBone("b", 0);
            bone.SetTranslationKeys(times, spline, CGltfInterpolation.CubicSpline);
            for (float t = 0f; t <= 2f; t += 0.05f)
            {
                bone.Update(t);
                Near(bone.LocalTransform.M41, curve.Evaluate(t), 1e-4f);
            }
        }

        [Fact]
        public void Constant_HoldsUntilTheNextKey()
        {
            var c = new AnimationCurve(new Keyframe(0, 1), new Keyframe(1, 5), new Keyframe(2, 2));
            ref var k = ref c[0];
            k.ModeOut = TangentMode.Constant;
            c.UpdateTangents(0);
            Near(1, c.Evaluate(0.99f));
            Assert.True(float.IsPositiveInfinity(c[0].OutTangent));
            float next = c.Evaluate(1.5f);                // the next segment is not held: between its keys
            Assert.True(next < 5f && next > 2f, $"{next}");
        }

        [Fact]
        public void Linear_PointsAtTheNeighbours()
        {
            var c = new AnimationCurve(new Keyframe(0, 0), new Keyframe(1, 2), new Keyframe(3, 0));
            for (int i = 0; i < 3; i++) { c[i].ModeIn = TangentMode.Linear; c[i].ModeOut = TangentMode.Linear; }
            c.UpdateAllTangents();
            Near(2, c[1].InTangent);
            Near(-1, c[1].OutTangent);
            Near(1, c.Evaluate(0.5f));                  // both ends linear: a straight line
            Near(1, c.Evaluate(2f));
        }

        [Fact]
        public void ClampedAuto_NeverOvershoots_AutoIsCatmullRom()
        {
            // A steep rise then a near-flat one: Catmull-Rom (Auto) goes past the third key; ClampedAuto does not.
            var keys = new[] { new Keyframe(0, 0), new Keyframe(1, 10), new Keyframe(2, 10.5f), new Keyframe(3, 11) };
            var clamped = new AnimationCurve(keys);
            var auto = new AnimationCurve(keys);
            for (int i = 0; i < 4; i++) { auto[i].ModeIn = TangentMode.Auto; auto[i].ModeOut = TangentMode.Auto; }
            auto.UpdateAllTangents();
            Near((10.5f - 0f) / 2f, auto[1].OutTangent);                 // (next - previous) / their distance
            float prev = clamped.Evaluate(0);
            float autoMax = 0;
            for (float t = 0; t <= 3f; t += 0.01f)
            {
                float v = clamped.Evaluate(t);
                Assert.True(v >= prev - 1e-4f, $"ClampedAuto goes down at {t}: {prev} → {v}");   // a rising run stays rising
                prev = v;
                if (t <= 2f) autoMax = MathF.Max(autoMax, auto.Evaluate(t));
            }
            Assert.True(autoMax > 10.5f, $"Auto overshoots the third key: {autoMax}");
            Near(0, clamped[0].OutTangent);                               // the end keys flat
            var peak = new AnimationCurve(new Keyframe(0, 0), new Keyframe(1, 4), new Keyframe(2, 1));
            Near(0, peak[1].OutTangent);                                  // a peak flat
        }

        [Fact]
        public void WrapModes_BeforeAndAfter()
        {
            var c = new AnimationCurve(new Keyframe(1, 0, 1, 1), new Keyframe(3, 2, 1, 1));   // a straight line 1→3 s, 0→2
            Near(0, c.Evaluate(0));
            Near(2, c.Evaluate(9));                                       // ClampForever
            c.PostWrap = WrapMode.Loop;
            c.PreWrap = WrapMode.Loop;
            Near(0.5f, c.Evaluate(3.5f));
            Near(1.5f, c.Evaluate(0.5f));
            c.PostWrap = WrapMode.PingPong;
            Near(1.5f, c.Evaluate(3.5f));                                 // coming back
            Near(0.5f, c.Evaluate(5.5f));                                 // and going again
        }

        [Fact]
        public void Hint_SameAsSearch_ForwardAndBack()
        {
            var keys = new Keyframe[20];
            for (int i = 0; i < 20; i++) keys[i] = new Keyframe(i * 0.37f, MathF.Sin(i));
            var c = new AnimationCurve(keys);
            int hint = -1;
            for (float t = -0.5f; t < 8f; t += 0.013f) Assert.Equal(c.Evaluate(t), c.Evaluate(t, ref hint));
            for (float t = 8f; t > -0.5f; t -= 0.029f) Assert.Equal(c.Evaluate(t), c.Evaluate(t, ref hint));
        }

        [Fact]
        public void Evaluating_AllocatesNothing()
        {
            var c = new AnimationCurve(new Keyframe(0, 0), new Keyframe(1, 3), new Keyframe(2, 1)) { PostWrap = WrapMode.Loop };
            int hint = -1;
            float sum = c.Evaluate(0.5f, ref hint);                     // the first call's JIT out of the way
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (float t = 0; t < 10f; t += 0.01f) sum += c.Evaluate(t, ref hint) + c.Evaluate(t);
            Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
            Assert.True(sum != 0);
        }

        // ── editing ──────────────────────────────────────────────────────────────────

        [Fact]
        public void AddKey_RefusesATimeTaken_MoveKeyGivesTheNewIndex()
        {
            var c = new AnimationCurve(new Keyframe(0, 0), new Keyframe(1, 1), new Keyframe(2, 0));
            Assert.Equal(-1, c.AddKey(1, 5));
            Assert.Equal(2, c.AddKey(1.5f, 3));
            Assert.Equal(4, c.Length);
            int now = c.MoveKey(1, new Keyframe(1.8f, 1));               // past 1.5: index 1 → 2
            Assert.Equal(2, now);
            for (int i = 1; i < c.Length; i++) Assert.True(c[i - 1].Time < c[i].Time);
            Near(1.8f, c[2].Time);
            int kept = c.MoveKey(2, new Keyframe(0, 7));                  // onto another key's time: the old time kept
            Near(1.8f, c[kept].Time);
            Near(7, c[kept].Value);
            c.RemoveKey(0);
            Assert.Equal(3, c.Length);
        }

        // ── the file ─────────────────────────────────────────────────────────────────

        const string Door = """
        { "sokolnetAnim": 1, "name": "DoorOpen", "length": 1.5, "frameRate": 30, "wrap": "Loop",
          "curves": [
            { "path": "Panel", "component": "Transform", "property": "LocalEulerAngles.y",
              "keys": [ { "t": 0, "v": 0 }, { "t": 1.5, "v": 90 } ] },
            { "path": "", "component": "Light", "property": "Intensity", "post": "PingPong",
              "keys": [ { "t": 0, "v": 0, "in": 0, "out": 4, "mi": "Free", "mo": "Free" },
                        { "t": 1, "v": 3, "in": "Infinity", "out": 0, "mi": "Constant", "mo": "ClampedAuto" } ] }
          ],
          "events": [ { "t": 0.2, "fn": "OnDoorCreak", "s": "creak", "f": 0.5, "i": 2 } ] }
        """;

        [Fact]
        public void File_ReadEvaluatedAndWrittenBack()
        {
            var clip = AnimClipJson.Read(Door);
            Assert.Equal("DoorOpen", clip.Name);
            Near(1.5f, clip.Length);
            Near(30, clip.FrameRate);
            Assert.Equal(WrapMode.Loop, clip.Wrap);
            var door = clip.GetCurve("Panel", "Transform", "LocalEulerAngles.y")!;
            Near(45, door.Evaluate(0.75f));                              // two flat ends: an S, half way at half time
            var light = clip.GetCurve("", "Light", "Intensity")!;
            Near(0, light.Evaluate(0.99f));                              // the second key's in side Constant: held
            Near(3, light.Evaluate(1));
            Near(0, light.Evaluate(1.5f));                               // PingPong: 1.5 s reads 0.5 s, the held part
            Assert.Equal(WrapMode.PingPong, light.PostWrap);
            var ev = Assert.Single(clip.Events);
            Assert.Equal(("OnDoorCreak", "creak", 0.5f, 2), (ev.FunctionName, ev.StringParameter, ev.FloatParameter, ev.IntParameter));

            string written = AnimClipJson.Write(clip);
            var again = AnimClipJson.Read(written);
            Assert.Equal(written, AnimClipJson.Write(again));
            for (float t = 0; t <= 2; t += 0.1f)
            {
                Near(door.Evaluate(t), again.GetCurve("Panel", "Transform", "LocalEulerAngles.y")!.Evaluate(t));
                Near(light.Evaluate(t), again.GetCurve("", "Light", "Intensity")!.Evaluate(t));
            }
            Assert.DoesNotContain("\"mi\": \"ClampedAuto\"", written);   // the defaults left out
            Assert.Contains("\"Infinity\"", written);
            Assert.Throws<FormatException>(() => AnimClipJson.Read("""{ "sokolnetAnim": 1, "wrap": "Sometimes" }"""));
        }

        // ── the binder ───────────────────────────────────────────────────────────────

        [Fact]
        public void Binder_WritesTheHierarchy_SkipsWhatIsNotThere()
        {
            var root = World.CreateEntity("Door");
            World.AddComponent(root, new LightComponent { Type = LightType.Spot, Color = Vector3.One, Intensity = 1, OuterAngle = 30, Enabled = true });
            var panel = World.CreateEntity("Panel");
            ref var pt = ref World.GetComponent<Transform>(panel);
            pt.Parent = root;
            pt.EulerAngles = new Vector3(10, 0, 0);                    // an angle no curve animates: kept

            var clip = AnimClipJson.Read(Door);
            clip.SetCurve("Panel", "Transform", "LocalPosition.x", new AnimationCurve(new Keyframe(0, 0, 2, 2), new Keyframe(1, 2, 2, 2)));
            clip.SetCurve("", "Light", "Color.g", new AnimationCurve(new Keyframe(0, 1, -1, -1), new Keyframe(1, 0, -1, -1)));
            clip.SetCurve("", "Light", "SpotAngle", new AnimationCurve(new Keyframe(0, 80)));
            clip.SetCurve("Hinge", "Transform", "LocalPosition.y", new AnimationCurve(new Keyframe(0, 1)));   // no Hinge
            clip.SetCurve("", "Transform", "Wobble", new AnimationCurve(new Keyframe(0, 1)));                 // no such property

            var bound = PropertyBinder.Bind(clip, root);
            Assert.Equal(5, bound.Bound);
            Assert.Equal(2, bound.Missing);
            Assert.Equal(2, _warnings.Count);
            Assert.Contains(_warnings, w => w.Contains("Hinge"));

            PropertyBinder.Apply(bound, 0.75f);
            var t = World.GetComponent<Transform>(panel);
            Assert.True(MathF.Abs(t.EulerAngles.Y - 45) < 0.01f && MathF.Abs(t.EulerAngles.X - 10) < 0.01f, $"one rotation of the three angles: {t.EulerAngles}");
            Near(1.5f, t.Position.X);
            var l = World.GetComponent<LightComponent>(root);
            Near(0.25f, l.Color.Y);
            Near(40, l.OuterAngle);                                       // a full cone angle, kept as a half-angle
            Near(0, l.Intensity);
            PropertyBinder.Apply(bound, 1f);
            Near(3, World.GetComponent<LightComponent>(root).Intensity);
            Assert.Equal(2, _warnings.Count);                             // applying warns of nothing
        }
    }
}
