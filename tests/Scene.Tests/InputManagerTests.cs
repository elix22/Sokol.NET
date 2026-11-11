using System.Numerics;
using GameEditor.Framework.Input;
using Sokol;
using Xunit;
using static Sokol.SApp;

namespace Scene.Tests
{
    /// <summary>
    /// InputManager frame semantics. sokol_app delivers events between frame callbacks, and the frame starts
    /// with BeginFrame: an event that arrived since the last frame must be visible to that frame's scripts
    /// (GetKeyDown, GetMouseButtonDown, MouseDelta, a touch's Began phase), for exactly one frame.
    /// </summary>
    public sealed unsafe class InputManagerTests
    {
        static void Send(sapp_event_type type, sapp_keycode key = sapp_keycode.SAPP_KEYCODE_INVALID,
            sapp_mousebutton button = sapp_mousebutton.SAPP_MOUSEBUTTON_INVALID,
            float x = 0, float y = 0, float dx = 0, float dy = 0, float scroll = 0)
        {
            var e = new sapp_event
            {
                type = type, key_code = key, mouse_button = button,
                mouse_x = x, mouse_y = y, mouse_dx = dx, mouse_dy = dy, scroll_y = scroll,
            };
            InputManager.ProcessEvent(&e);
        }

        static void Touch(sapp_event_type type, int id, float x, float y)
        {
            var e = new sapp_event { type = type, num_touches = 1 };
            e.touches[0].identifier = (nuint)id;
            e.touches[0].pos_x = x;
            e.touches[0].pos_y = y;
            InputManager.ProcessEvent(&e);
        }

        [Fact]
        public void KeyPressedBetweenFrames_IsDownForOneFrame_ThenHeld()
        {
            InputManager.BeginFrame();
            Send(sapp_event_type.SAPP_EVENTTYPE_KEY_DOWN, sapp_keycode.SAPP_KEYCODE_J);

            InputManager.BeginFrame();
            Assert.True(InputManager.GetKeyDown(Key.J), "GetKeyDown in the frame after the press");
            Assert.True(InputManager.GetKey(Key.J));

            InputManager.BeginFrame();
            Assert.False(InputManager.GetKeyDown(Key.J), "GetKeyDown lasts one frame");
            Assert.True(InputManager.GetKey(Key.J), "still held");

            Send(sapp_event_type.SAPP_EVENTTYPE_KEY_UP, sapp_keycode.SAPP_KEYCODE_J);
            InputManager.BeginFrame();
            Assert.True(InputManager.GetKeyUp(Key.J), "GetKeyUp in the frame after the release");
            Assert.False(InputManager.GetKey(Key.J));

            InputManager.BeginFrame();
            Assert.False(InputManager.GetKeyUp(Key.J), "GetKeyUp lasts one frame");
        }

        [Fact]
        public void KeyTappedBetweenFrames_IsDownAndUpInTheSameFrame()
        {
            InputManager.BeginFrame();
            Send(sapp_event_type.SAPP_EVENTTYPE_KEY_DOWN, sapp_keycode.SAPP_KEYCODE_K);
            Send(sapp_event_type.SAPP_EVENTTYPE_KEY_UP, sapp_keycode.SAPP_KEYCODE_K);

            InputManager.BeginFrame();
            Assert.True(InputManager.GetKeyDown(Key.K));
            Assert.True(InputManager.GetKeyUp(Key.K));
            Assert.False(InputManager.GetKey(Key.K));
        }

        [Fact]
        public void MouseButtonMoveAndScrollBetweenFrames_AreSeenForOneFrame()
        {
            InputManager.BeginFrame();
            Send(sapp_event_type.SAPP_EVENTTYPE_MOUSE_MOVE, x: 40, y: 30, dx: 4, dy: 3);
            Send(sapp_event_type.SAPP_EVENTTYPE_MOUSE_MOVE, x: 42, y: 31, dx: 2, dy: 1);
            Send(sapp_event_type.SAPP_EVENTTYPE_MOUSE_DOWN, button: sapp_mousebutton.SAPP_MOUSEBUTTON_LEFT);
            Send(sapp_event_type.SAPP_EVENTTYPE_MOUSE_SCROLL, scroll: 2);

            InputManager.BeginFrame();
            Assert.Equal(new Vector2(42, 31), InputManager.MousePosition);
            Assert.Equal(new Vector2(6, 4), InputManager.MouseDelta);
            Assert.Equal(2f, InputManager.ScrollDelta);
            Assert.True(InputManager.GetMouseButtonDown(0));
            Assert.True(InputManager.GetMouseButton(0));

            InputManager.BeginFrame();
            Assert.Equal(new Vector2(42, 31), InputManager.MousePosition);
            Assert.Equal(Vector2.Zero, InputManager.MouseDelta);
            Assert.Equal(0f, InputManager.ScrollDelta);
            Assert.False(InputManager.GetMouseButtonDown(0));
            Assert.True(InputManager.GetMouseButton(0));

            Send(sapp_event_type.SAPP_EVENTTYPE_MOUSE_UP, button: sapp_mousebutton.SAPP_MOUSEBUTTON_LEFT);
            InputManager.BeginFrame();
            Assert.True(InputManager.GetMouseButtonUp(0));
            Assert.False(InputManager.GetMouseButton(0));
            InputManager.BeginFrame();
            Assert.False(InputManager.GetMouseButtonUp(0));
        }

        [Fact]
        public void TouchBetweenFrames_BeganThenStationaryThenEnded()
        {
            InputManager.BeginFrame();
            Touch(sapp_event_type.SAPP_EVENTTYPE_TOUCHES_BEGAN, 7, 100, 50);

            InputManager.BeginFrame();
            Assert.Equal(1, InputManager.TouchCount);
            Assert.Equal(TouchPhase.Began, InputManager.GetTouch(0).Phase);
            Assert.Equal(new Vector2(100, 50), InputManager.GetTouch(0).Position);

            InputManager.BeginFrame();
            Assert.Equal(TouchPhase.Stationary, InputManager.GetTouch(0).Phase);

            Touch(sapp_event_type.SAPP_EVENTTYPE_TOUCHES_MOVED, 7, 110, 50);
            InputManager.BeginFrame();
            Assert.Equal(TouchPhase.Moved, InputManager.GetTouch(0).Phase);
            Assert.Equal(new Vector2(10, 0), InputManager.GetTouch(0).DeltaPosition);

            Touch(sapp_event_type.SAPP_EVENTTYPE_TOUCHES_ENDED, 7, 110, 50);
            InputManager.BeginFrame();
            Assert.Equal(TouchPhase.Ended, InputManager.GetTouch(0).Phase);   // the script sees the lift
            Assert.Equal(0, InputManager.TouchCount);

            InputManager.BeginFrame();
            Assert.Equal(default, InputManager.GetTouch(0));                   // then the slot is free
        }

        [Fact]
        public void PointerViewport_ReportsPositionsInGamePixels()
        {
            // The game is drawn from window pixel (200, 100), half a game pixel per window pixel.
            InputManager.SetPointerViewport(200, 100, 0.5f);
            try
            {
                InputManager.BeginFrame();
                Send(sapp_event_type.SAPP_EVENTTYPE_MOUSE_MOVE, x: 300, y: 160, dx: 10, dy: -4);
                Touch(sapp_event_type.SAPP_EVENTTYPE_TOUCHES_BEGAN, 3, 260, 100);
                InputManager.BeginFrame();
                Assert.Equal(new Vector2(50, 30), InputManager.MousePosition);
                Assert.Equal(new Vector2(5, -2), InputManager.MouseDelta);
                Assert.Equal(new Vector2(30, 0), InputManager.GetTouch(0).Position);
                Touch(sapp_event_type.SAPP_EVENTTYPE_TOUCHES_ENDED, 3, 260, 100);
                InputManager.BeginFrame();
            }
            finally
            {
                InputManager.SetPointerViewport(0, 0);
                InputManager.BeginFrame();
            }
        }

        [Fact]
        public void VirtualKeySetBetweenFrames_IsDownForOneFrame()
        {
            InputManager.BeginFrame();
            InputManager.SetVirtualKey(Key.L, true);    // e.g. an on-screen button's tap handler, run from the event callback

            InputManager.BeginFrame();
            Assert.True(InputManager.GetKeyDown(Key.L));
            Assert.True(InputManager.GetKey(Key.L));

            InputManager.BeginFrame();
            Assert.False(InputManager.GetKeyDown(Key.L));
            Assert.True(InputManager.GetKey(Key.L));

            InputManager.SetVirtualKey(Key.L, false);
            InputManager.BeginFrame();
            Assert.True(InputManager.GetKeyUp(Key.L));
            Assert.False(InputManager.GetKey(Key.L));
            InputManager.BeginFrame();
            Assert.False(InputManager.GetKeyUp(Key.L));
        }
    }
}
