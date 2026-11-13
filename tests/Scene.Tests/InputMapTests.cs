using System;
using GameEditor.Framework.Input;
using Sokol;
using Xunit;
using static Sokol.SApp;

namespace Scene.Tests
{
    /// <summary>
    /// A game's own input actions (G7.1): read from its table before the built-in names; an axis from its keys and the
    /// gamepad, a button from its keys, mouse button and gamepad; changed and written as the file has it.
    /// </summary>
    public sealed unsafe class InputMapTests : IDisposable
    {
        public InputMapTests()
        {
            InputMap.Read("");
            InputManager.SetGamepadState(default);
            InputManager.BeginFrame();
        }

        public void Dispose()
        {
            InputMap.Read("");
            InputManager.SetGamepadState(default);
            InputManager.BeginFrame();
        }

        static void Key(sapp_event_type type, sapp_keycode key)
        {
            var e = new sapp_event { type = type, key_code = key };
            InputManager.ProcessEvent(&e);
        }

        static void Down(sapp_keycode key) => Key(sapp_event_type.SAPP_EVENTTYPE_KEY_DOWN, key);
        static void Up(sapp_keycode key) => Key(sapp_event_type.SAPP_EVENTTYPE_KEY_UP, key);

        const string Map = "{\"actions\": {" +
            "\"Steer\": {\"kind\": \"axis\", \"negative\": [\"A\", \"LeftArrow\"], \"positive\": [\"D\", \"RightArrow\"], \"pad\": \"LeftStickX\"}," +
            "\"Boost\": {\"kind\": \"button\", \"keys\": [\"Space\"], \"mouse\": 1, \"pad\": \"East\"}," +
            "\"Jump\": {\"kind\": \"button\", \"keys\": [\"J\"], \"mouse\": -1, \"pad\": \"\"}}}";

        [Fact]
        public void An_axis_of_the_map_reads_its_keys_and_the_gamepad()
        {
            Assert.Null(InputMap.Read(Map));
            Assert.Equal(new[] { "Steer", "Boost", "Jump" }, InputMap.Names);
            Assert.Equal(0f, InputManager.GetAxis("Steer"));
            Down(sapp_keycode.SAPP_KEYCODE_D);
            InputManager.BeginFrame();
            Assert.Equal(1f, InputManager.GetAxis("Steer"));
            Assert.True(InputManager.GetButton("Steer"));                       // an axis that is not 0 is held
            Assert.True(InputManager.GetButtonDown("Steer"));
            Down(sapp_keycode.SAPP_KEYCODE_LEFT);
            InputManager.BeginFrame();
            Assert.Equal(0f, InputManager.GetAxis("Steer"));                    // both ways at once: 0
            Up(sapp_keycode.SAPP_KEYCODE_D);
            InputManager.BeginFrame();
            Assert.Equal(-1f, InputManager.GetAxis("Steer"));
            Assert.True(InputManager.GetButtonUp("Steer"));
            Up(sapp_keycode.SAPP_KEYCODE_LEFT);
            InputManager.SetGamepadState(new GamepadState { LeftStick = new System.Numerics.Vector2(0.5f, 0f) });
            InputManager.BeginFrame();
            Assert.Equal(0.5f, InputManager.GetAxis("Steer"));
        }

        [Fact]
        public void A_button_of_the_map_reads_its_keys_mouse_and_gamepad_and_a_built_in_name_is_moved()
        {
            InputMap.Read(Map);
            Down(sapp_keycode.SAPP_KEYCODE_SPACE);
            InputManager.BeginFrame();
            Assert.True(InputManager.GetButtonDown("Boost"));
            Assert.Equal(1f, InputManager.GetAxis("Boost"));                    // a button is also an axis, 0 or 1
            Assert.False(InputManager.GetButton("Jump"));                       // "Jump" is J now, not Space
            Up(sapp_keycode.SAPP_KEYCODE_SPACE);
            InputManager.BeginFrame();
            Assert.True(InputManager.GetButtonUp("Boost"));
            Assert.False(InputManager.GetButton("Boost"));

            var e = new sapp_event { type = sapp_event_type.SAPP_EVENTTYPE_MOUSE_DOWN, mouse_button = sapp_mousebutton.SAPP_MOUSEBUTTON_RIGHT };
            InputManager.ProcessEvent(&e);
            InputManager.BeginFrame();
            Assert.True(InputManager.GetButton("Boost"));
            Assert.True(InputManager.GetButtonDown("Boost"));
            e.type = sapp_event_type.SAPP_EVENTTYPE_MOUSE_UP;
            InputManager.ProcessEvent(&e);
            InputManager.BeginFrame();
            InputManager.SetGamepadState(new GamepadState { East = true });
            Assert.True(InputManager.GetButton("Boost"));

            Down(sapp_keycode.SAPP_KEYCODE_J);
            InputManager.BeginFrame();
            Assert.True(InputManager.GetButton("Jump"));
            Up(sapp_keycode.SAPP_KEYCODE_J);

            // A name the map does not have: the built-in one, as before.
            Down(sapp_keycode.SAPP_KEYCODE_W);
            InputManager.BeginFrame();
            Assert.Equal(1f, InputManager.GetAxis("Vertical"));
            Up(sapp_keycode.SAPP_KEYCODE_W);
            InputManager.BeginFrame();

            // An on-screen control's value comes first.
            InputManager.SetVirtualAxis("Steer", -0.25f);
            Assert.Equal(-0.25f, InputManager.GetAxis("Steer"));
        }

        [Fact]
        public void The_map_is_changed_with_its_refusals_and_written_as_its_file_has_it()
        {
            Assert.Null(InputMap.Set("Steer", kind: "axis", negative: "A, LeftArrow", positive: "d,RightArrow", pad: "LeftStickX"));
            Assert.Null(InputMap.Set("Boost", kind: "button", keys: "Space", mouse: 0, pad: "South"));
            Assert.Contains("no key", InputMap.Set("Boost", keys: "Space, Spcae"));
            Assert.Contains("no key", InputMap.Set("Boost", keys: "7"));
            Assert.Contains("no kind of action", InputMap.Set("Boost", kind: "trigger"));
            Assert.Contains("no mouse button", InputMap.Set("Boost", mouse: 5));
            Assert.Contains("no gamepad button", InputMap.Set("Boost", pad: "LeftStickX"));
            Assert.Contains("no gamepad axis", InputMap.Set("Steer", pad: "South"));
            Assert.Contains("needs a name", InputMap.Set(" "));
            Assert.Equal(new[] { GameEditor.Framework.Input.Key.Space }, InputMap.Find("Boost")!.Keys);   // a refused change changes nothing
            Assert.Null(InputMap.Set("Boost", mouse: -1));                        // only the field given changes
            Assert.Equal(new[] { GameEditor.Framework.Input.Key.Space }, InputMap.Find("Boost")!.Keys);
            Assert.Equal((-1, "South"), (InputMap.Find("Boost")!.Mouse, InputMap.Find("Boost")!.Pad));

            string file = InputMap.Write();
            Assert.Contains("\"positive\": [\n        \"D\",\n        \"RightArrow\"\n      ]", file.Replace("\r\n", "\n"));
            InputMap.Read(file);
            Assert.Equal(new[] { "Steer", "Boost" }, InputMap.Names);
            var steer = InputMap.Find("Steer")!;
            Assert.True(steer.IsAxis);
            Assert.Equal(new[] { GameEditor.Framework.Input.Key.A, GameEditor.Framework.Input.Key.LeftArrow }, steer.Negative);
            Assert.True(InputMap.Remove("Steer"));
            Assert.False(InputMap.Remove("Steer"));
            Assert.Equal(new[] { "Boost" }, InputMap.Names);
            Assert.NotNull(InputMap.Read("{\"actions\": {\"X\": {\"kind\": \"button\", \"keys\": [\"Nope\"]}}}"));   // told, the rest kept
            Assert.Equal(new[] { "X" }, InputMap.Names);
        }
    }
}
