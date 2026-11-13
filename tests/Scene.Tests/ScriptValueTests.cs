using System;
using System.Collections.Generic;
using System.Numerics;
using Frent;
using GameEditor.Framework.Scripting;
using Xunit;

namespace Scene.Tests
{
    /// <summary>
    /// A script's field set from a scene's text with no reflection (ScriptValue — what a game compiled ahead of time
    /// uses, through the setter the builder writes for each script), against the way by the field's name that the
    /// editor's Play uses: the same text must be the same value.
    /// </summary>
    public class ScriptValueTests
    {
        public enum Mode { Walk, Run, Fly }

        [Flags] public enum Sides { None = 0, Left = 1, Right = 2, Top = 4 }

        public sealed class Probe : GameBehaviour
        {
            public float F;
            public double D;
            public int I;
            public uint U;
            public long L;
            public ulong UL;
            public short S;
            public byte B;
            public bool Flag;
            public string Text = "";
            public char C;
            public Mode M;
            public Sides Side;
            public Vector2 V2;
            public Vector3 V3;
            public Vector4 V4;
            public Quaternion Q;
            public Entity Other;
        }

        // The setter as the builder writes it for a script: a switch on the field's name, the overload picked by its type.
        static bool Set(GameBehaviour script, string field, in ScriptFieldValue value)
        {
            var s = (Probe)script;
            switch (field)
            {
                case "F": return ScriptValue.Set(ref s.F, value);
                case "D": return ScriptValue.Set(ref s.D, value);
                case "I": return ScriptValue.Set(ref s.I, value);
                case "U": return ScriptValue.Set(ref s.U, value);
                case "L": return ScriptValue.Set(ref s.L, value);
                case "UL": return ScriptValue.Set(ref s.UL, value);
                case "S": return ScriptValue.Set(ref s.S, value);
                case "B": return ScriptValue.Set(ref s.B, value);
                case "Flag": return ScriptValue.Set(ref s.Flag, value);
                case "Text": return ScriptValue.Set(ref s.Text, value);
                case "C": return ScriptValue.Set(ref s.C, value);
                case "M": return ScriptValue.SetEnum(ref s.M, value);
                case "Side": return ScriptValue.SetEnum(ref s.Side, value);
                case "V2": return ScriptValue.Set(ref s.V2, value);
                case "V3": return ScriptValue.Set(ref s.V3, value);
                case "V4": return ScriptValue.Set(ref s.V4, value);
                case "Q": return ScriptValue.Set(ref s.Q, value);
                case "Other": return ScriptValue.Set(ref s.Other, value);
                default: return false;
            }
        }

        static readonly Dictionary<string, string> Texts = new()
        {
            ["F"] = "7.5", ["D"] = "0.25", ["I"] = "-5", ["U"] = "4000000000", ["L"] = "6000000000", ["UL"] = "18000000000000000000",
            ["S"] = "-12", ["B"] = "200", ["Flag"] = "true", ["Text"] = "racer, with a comma", ["C"] = "z", ["M"] = "Fly", ["Side"] = "Right, Top",
            ["V2"] = "[1,4]", ["V3"] = "[3,1.5,0]", ["V4"] = "[1, 0.5, 0.25, 0.5]", ["Q"] = "[0,0.70710677,0,0.70710677]",
        };

        [Fact]
        public void EveryKind_IsSetFromItsText()
        {
            var p = new Probe();
            foreach (var (field, text) in Texts) Assert.True(Set(p, field, new ScriptFieldValue(text)), field);
            Assert.Equal(7.5f, p.F);
            Assert.Equal(0.25, p.D);
            Assert.Equal(-5, p.I);
            Assert.Equal(4000000000u, p.U);
            Assert.Equal(6000000000L, p.L);
            Assert.Equal(18000000000000000000UL, p.UL);
            Assert.Equal((short)-12, p.S);
            Assert.Equal((byte)200, p.B);
            Assert.True(p.Flag);
            Assert.Equal("racer, with a comma", p.Text);
            Assert.Equal('z', p.C);
            Assert.Equal(Mode.Fly, p.M);
            Assert.Equal(Sides.Right | Sides.Top, p.Side);
            Assert.Equal(new Vector2(1, 4), p.V2);
            Assert.Equal(new Vector3(3, 1.5f, 0), p.V3);
            Assert.Equal(new Vector4(1, 0.5f, 0.25f, 0.5f), p.V4);
            Assert.Equal(new Quaternion(0, 0.70710677f, 0, 0.70710677f), p.Q);
        }

        [Fact]
        public void TheTypedWay_AndTheWayByName_ReadATextAlike()
        {
            var typed = new Probe();
            var named = new Probe();
            foreach (var (field, text) in Texts) Set(typed, field, new ScriptFieldValue(text));
            named.ApplySerializedProperties(Texts);
            foreach (var field in typeof(Probe).GetFields())
                Assert.Equal(field.GetValue(named), field.GetValue(typed));
        }

        [Theory]
        [InlineData("F", "fast")]
        [InlineData("I", "2.5")]
        [InlineData("B", "300")]
        [InlineData("M", "Swim")]
        [InlineData("Side", "Left, Under")]
        [InlineData("V3", "[1,2]")]
        [InlineData("C", "")]
        [InlineData("Nothing", "1")]
        public void ATextThatIsNoValue_LeavesTheField_AndSaysSo(string field, string text)
        {
            var p = new Probe { F = 1, I = 2, B = 3, M = Mode.Run, Side = Sides.Left, V3 = Vector3.One, C = 'k' };
            Assert.False(Set(p, field, new ScriptFieldValue(text)));
            Assert.Equal((1f, 2, (byte)3, Mode.Run, Sides.Left, Vector3.One, 'k'), (p.F, p.I, p.B, p.M, p.Side, p.V3, p.C));
        }

        [Fact]
        public void AnEnum_IsReadWhateverTheCase()
        {
            var p = new Probe();
            Assert.True(Set(p, "M", new ScriptFieldValue("fly")));
            Assert.Equal(Mode.Fly, p.M);
        }

        [Fact]
        public void AnEntity_IsGivenAsItIs()
        {
            var world = GameEditor.Framework.ECS.ECSWorld.Instance;
            Entity other = world.CreateEntity();
            try
            {
                var p = new Probe();
                Assert.True(Set(p, "Other", new ScriptFieldValue(other)));
                Assert.Equal(other, p.Other);
                Assert.True(Set(p, "Other", new ScriptFieldValue(default(Entity))));
                Assert.True(p.Other.IsNull);
            }
            finally { world.DestroyEntity(other); }
        }

        [Fact]
        public void AScriptOfAScene_GetsItsValuesThroughItsSetter_NotByTheFieldsNames()
        {
            var world = GameEditor.Framework.ECS.ECSWorld.Instance;
            Entity e = world.CreateEntity("WithProbe");
            var asked = new List<string>();
            try
            {
                world.AddComponent(e, new GameEditor.Framework.ECS.Components.ScriptComponent
                {
                    TypeName = nameof(Probe), Properties = new Dictionary<string, string> { ["F"] = "7.5", ["M"] = "Fly" },
                });
                // A setter that sets F alone: M stays as it was, which the way by name would have set.
                ScriptSystem.RegisterType<Probe>((GameBehaviour script, string field, in ScriptFieldValue value) =>
                {
                    asked.Add(field);
                    return field == "F" && ScriptValue.Set(ref ((Probe)script).F, value);
                });
                int before = ScriptSystem.FieldsSetByGeneratedCode;
                ScriptSystem.PopulateFromScene(world);
                Assert.Equal(new[] { "F", "M" }, asked);
                Assert.Equal(before + 1, ScriptSystem.FieldsSetByGeneratedCode);      // F was set; M was refused, and said
            }
            finally
            {
                ScriptSystem.StopAll();
                ScriptSystem.ClearFactories();
                world.DestroyEntity(e);
            }
        }

        [Fact]
        public void ARegisteredSetter_IsWhatAScriptIsRegisteredWith()
        {
            try
            {
                Assert.False(ScriptSystem.HasGeneratedTypes);
                ScriptSystem.RegisterType<Probe>(Set);
                Assert.True(ScriptSystem.HasFactory(nameof(Probe)));
                Assert.True(ScriptSystem.HasFieldSetter(nameof(Probe)));
                Assert.True(ScriptSystem.HasGeneratedTypes);
            }
            finally { ScriptSystem.ClearFactories(); }
            Assert.False(ScriptSystem.HasFieldSetter(nameof(Probe)));
        }
    }
}
