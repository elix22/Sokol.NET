using System;
using System.Runtime.InteropServices;
using M = System.Runtime.InteropServices.MarshalAsAttribute;
using U = System.Runtime.InteropServices.UnmanagedType;
using System.Runtime.CompilerServices;
using System.Numerics;
namespace Sokol
{
    public static unsafe partial class SG
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct sg_color
        {
            public sg_color()
            {
                r = 0;
                g = 0;
                b = 0;
                a = 1;

            }

            public sg_color(ReadOnlySpan<float> arr)
            {
                if (arr.Length >= 4)
                {
                    r = arr[0];
                    g = arr[1];
                    b = arr[2];
                    a = arr[3];
                }
                else
                {
                    r = g = b = 0;
                    a = 1;
                }
            }
            public float r;
            public float g;
            public float b;
            public float a;

            public ref Vector3 AsVector3
            {
                get => ref Unsafe.As<float, Vector3>(ref r);
            }

            public ref Vector4 AsVector4
            {
                get => ref Unsafe.As<float, Vector4>(ref r);
            }

            public static implicit operator sg_color(Vector3 v)
            {
                return new sg_color { r = v.X, g = v.Y, b = v.Z, a = 1 };
            }

            public static implicit operator sg_color(Vector4 v)
            {
                return new sg_color { r = v.X, g = v.Y, b = v.Z, a = v.W };
            }

            public static implicit operator sg_color(float[] arr)
            {
                if (arr.Length == 0)
                {
                    return new sg_color { r = 0, g = 0, b = 0, a = 1 };
                }
                else
                if (arr.Length == 1)
                {
                    return new sg_color { r = arr[0], g = arr[0], b = arr[0], a = 1 };
                }
                else if (arr.Length == 2)
                {
                    return new sg_color { r = arr[0], g = arr[1], b = 0, a = 1 };
                }
                else if (arr.Length == 3)
                {
                    return new sg_color { r = arr[0], g = arr[1], b = arr[2], a = 1 };
                }
                return new sg_color { r = arr[0], g = arr[1], b = arr[2], a = arr[3] };
            }

            /// <summary>
            /// Provides span access over the four color components.
            /// </summary>
            public Span<float> AsSpan
            {
                get
                {
                    // Use "fixed" to pin the SG memory and create a Span over the four floats.
                    fixed (float* ptr = &r)
                    {
                        return new Span<float>(ptr, 4);
                    }
                }
            }
        }

    }

}

/*
    sg_color clearColor = state.pass_action.colors[0].clear_value;
        Vector3 color = new Vector3(state.pass_action.colors[0].clear_value.r, state.pass_action.colors[0].clear_value.g, state.pass_action.colors[0].clear_value.b);
        ImGui.ColorEdit3("clear color", ref color, 0);
        state.pass_action.colors[0].clear_value = new sg_color { r = color[0], g = color[1], b = color[2], a = 1 };

*/