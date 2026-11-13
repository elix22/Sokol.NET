using GameEditor.Framework.Input;
using Sokol;
using Xunit;
using static Sokol.SApp;

namespace Scene.Tests
{
    /// <summary>Tilt steering (H5 step K7): one axis of gravity, a dead zone and a clamp - nothing else (R9); a setting, off by
    /// default; the horizontal axis takes it only while it is on and nothing else steers.</summary>
    public sealed unsafe class TiltTests : System.IDisposable
    {
        public TiltTests()
        {
            InputManager.TiltEnabled = false;
            InputManager.TiltOverride = null;
            InputManager.TiltFullAngle = 30f;
            InputManager.TiltDeadZone = 0.1f;
            InputManager.BeginFrame();
        }

        public void Dispose()
        {
            InputManager.TiltEnabled = false;
            InputManager.TiltOverride = null;
            Key(sapp_keycode.SAPP_KEYCODE_LEFT, false);
            InputManager.BeginFrame();
        }

        static float G(float degrees) => 9.80665f * System.MathF.Sin(degrees * System.MathF.PI / 180f);

        static void Key(sapp_keycode key, bool down)
        {
            var e = new sapp_event { type = down ? sapp_event_type.SAPP_EVENTTYPE_KEY_DOWN : sapp_event_type.SAPP_EVENTTYPE_KEY_UP, key_code = key };
            InputManager.ProcessEvent(&e);
        }

        [Fact]
        public void The_steer_is_the_tilt_scaled_with_a_dead_zone_and_a_clamp()
        {
            Assert.Equal(0f, InputManager.TiltFrom(0f));
            Assert.Equal(0f, InputManager.TiltFrom(G(2.5f)));                 // within the dead zone (a tenth of 30 degrees is about 3)
            Assert.Equal(1f, InputManager.TiltFrom(G(30f)), 3);              // the full angle: a full steer
            Assert.Equal(1f, InputManager.TiltFrom(G(70f)));                 // beyond it: clamped
            Assert.Equal(-1f, InputManager.TiltFrom(-G(70f)));
            float half = InputManager.TiltFrom(G(14.5f));                   // about half of the way past the dead zone
            Assert.InRange(half, 0.4f, 0.5f);
            Assert.Equal(-half, InputManager.TiltFrom(-G(14.5f)), 5);       // left as right
        }

        [Fact]
        public void The_horizontal_axis_takes_the_tilt_only_while_it_is_on_and_a_key_goes_before_it()
        {
            InputManager.TiltOverride = G(30f);
            InputManager.BeginFrame();
            Assert.Equal(0f, InputManager.GetAxis("Horizontal"));             // off by default: nothing
            Assert.Equal(0f, InputManager.Tilt);

            InputManager.TiltEnabled = true;
            InputManager.BeginFrame();
            Assert.Equal(1f, InputManager.GetAxis("Horizontal"), 3);         // on: the tilt steers right
            Assert.Equal(1f, InputManager.Tilt, 3);

            Key(sapp_keycode.SAPP_KEYCODE_LEFT, true);
            InputManager.BeginFrame();
            Assert.Equal(-1f, InputManager.GetAxis("Horizontal"));            // a key steers over it

            Key(sapp_keycode.SAPP_KEYCODE_LEFT, false);
            InputManager.TiltEnabled = false;
            InputManager.BeginFrame();
            Assert.Equal(0f, InputManager.GetAxis("Horizontal"));             // off again
        }
    }
}
