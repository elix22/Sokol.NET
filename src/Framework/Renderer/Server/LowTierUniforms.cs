// LowTierUniforms.cs — hands the engine's full uniform blocks to the LOW_TIER shaders, whose blocks are smaller.
//
// A LOW_TIER shader (compiled with --defines LOW_TIER, GLSL ES 3.0 only) is made when the GPU refuses the full
// one: the PowerVR GE8320 of a Redmi 6A has 1024 fragment uniform components, and the full light block alone is
// 257 vec4 (1028). The engine still fills its full blocks the one way it always does; the call that applies one
// to a LOW_TIER pipeline goes through here and copies the parts the smaller block has:
//
//   light block (PBR):   lights_data[128] spot_shadow_vp[32] point_shadow_vp[96] ambient_num
//                     →  lights_data[ 64] spot_shadow_vp[16] point_shadow_vp[24] ambient_num
//   fs block (Blinn):    4 vec4 of material + camera, the same three arrays, ambient_sky[9]
//   skinned vs block:    8 vec4, finalBonesMatrices[128], csm_vp[n]  →  8 vec4, finalBonesMatrices[1], csm_vp[n]
//
// The numbers are the LOW_TIER ones of pbr_fs_uniforms.glsl / blinn_phong.glsl / pbr_vs_uniforms.glsl: 16 lights,
// 4 spot-shadow matrices (atlas slices 4-7), 1 point-shadow light, no bone array (a skinned mesh reads its bones
// from the joint texture). The shaders themselves ignore a light, a spot shadow or a point shadow past those.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static Sokol.SG;

namespace GameEditor.Framework.Renderer.Server
{
    internal static unsafe class LowTierUniforms
    {
        // Full block, in vec4.
        private const int FullLights = 128, FullSpot = 32, FullPoint = 96;
        private const int FullBones  = 128 * 4;
        // LOW_TIER block, in vec4.
        public  const int Lights = 16, SpotShadows = 4, PointShadowLights = 1;
        private const int LowLights = Lights * 4, LowSpot = SpotShadows * 4, LowPoint = PointShadowLights * 24;
        private const int LowBones  = 1 * 4;

        private const int PbrLightVec4s   = LowLights + LowSpot + LowPoint + 1;          // 105
        private const int BlinnHeadVec4s  = 4, BlinnSkyVec4s = 9;
        private const int BlinnFsVec4s    = BlinnHeadVec4s + LowLights + LowSpot + LowPoint + BlinnSkyVec4s; // 117
        private const int SkinVsHeadVec4s = 8;                                           // view_proj, eye_pos, morph weights, flags

        // One scratch block, big enough for every packed block (the largest is a skinned vs block with 4 cascades).
        private static readonly Vector4[] _scratch = new Vector4[128];

        /// <summary>The PBR light block (binding 2): <c>sg_apply_uniforms</c> with the full block, or packed for LOW_TIER.</summary>
        public static void ApplyPbrLights<T>(int slot, in T full, bool low) where T : unmanaged
        {
            if (!low) { sg_apply_uniforms(slot, Range(in full)); return; }
            ReadOnlySpan<Vector4> s = AsVec4(in full);
            Span<Vector4> d = _scratch;
            s.Slice(0, LowLights).CopyTo(d);
            s.Slice(FullLights, LowSpot).CopyTo(d.Slice(LowLights));
            s.Slice(FullLights + FullSpot, LowPoint).CopyTo(d.Slice(LowLights + LowSpot));
            d[LowLights + LowSpot + LowPoint] = s[FullLights + FullSpot + FullPoint];   // ambient_num
            ApplyScratch(slot, PbrLightVec4s);
        }

        /// <summary>The Blinn-Phong fragment block (binding 1): full, or packed for LOW_TIER.</summary>
        public static void ApplyBlinnFs<T>(int slot, in T full, bool low) where T : unmanaged
        {
            if (!low) { sg_apply_uniforms(slot, Range(in full)); return; }
            ReadOnlySpan<Vector4> s = AsVec4(in full);
            Span<Vector4> d = _scratch;
            int h = BlinnHeadVec4s;
            s.Slice(0, h).CopyTo(d);
            s.Slice(h, LowLights).CopyTo(d.Slice(h));
            s.Slice(h + FullLights, LowSpot).CopyTo(d.Slice(h + LowLights));
            s.Slice(h + FullLights + FullSpot, LowPoint).CopyTo(d.Slice(h + LowLights + LowSpot));
            s.Slice(h + FullLights + FullSpot + FullPoint, BlinnSkyVec4s).CopyTo(d.Slice(h + LowLights + LowSpot + LowPoint));
            ApplyScratch(slot, BlinnFsVec4s);
        }

        /// <summary>A skinned PBR vertex block (binding 0): full, or without its bone array for LOW_TIER.</summary>
        public static void ApplySkinnedVs<T>(int slot, in T full, bool low) where T : unmanaged
        {
            if (!low) { sg_apply_uniforms(slot, Range(in full)); return; }
            ReadOnlySpan<Vector4> s = AsVec4(in full);
            Span<Vector4> d = _scratch;
            int tail = s.Length - SkinVsHeadVec4s - FullBones;   // the cascades' matrices after the bones
            s.Slice(0, SkinVsHeadVec4s).CopyTo(d);
            d.Slice(SkinVsHeadVec4s, LowBones).Clear();
            s.Slice(SkinVsHeadVec4s + FullBones, tail).CopyTo(d.Slice(SkinVsHeadVec4s + LowBones));
            ApplyScratch(slot, SkinVsHeadVec4s + LowBones + tail);
        }

        /// <summary>
        /// The skinned shadow caster's block keeps its bones LAST, so LOW_TIER takes the same struct cut short:
        /// mvp, model, the flags and one bone.
        /// </summary>
        public static void ApplySkinnedCaster<T>(int slot, in T full, bool low) where T : unmanaged
        {
            sg_range r = Range(in full);
            if (low) r.size = (nuint)((4 + 4 + 1 + LowBones) * 16);
            sg_apply_uniforms(slot, r);
        }

        /// <summary>The packed size of each LOW_TIER block, for the check against sokol-shdc's own sizes.</summary>
        public static int PbrLightBytes  => PbrLightVec4s * 16;
        public static int BlinnFsBytes   => BlinnFsVec4s * 16;
        public static int SkinnedVsBytes(int cascades) => (SkinVsHeadVec4s + LowBones + cascades * 4) * 16;
        public static int SkinnedCasterBytes => (4 + 4 + 1 + LowBones) * 16;

        private static ReadOnlySpan<Vector4> AsVec4<T>(in T value) where T : unmanaged
            => MemoryMarshal.Cast<T, Vector4>(new ReadOnlySpan<T>(in value));

        private static sg_range Range<T>(in T value) where T : unmanaged
            => new sg_range { ptr = Unsafe.AsPointer(ref Unsafe.AsRef(in value)), size = (nuint)sizeof(T) };

        private static void ApplyScratch(int slot, int vec4s)
        {
            fixed (Vector4* p = _scratch)
                sg_apply_uniforms(slot, new sg_range { ptr = p, size = (nuint)(vec4s * 16) });
        }
    }
}
