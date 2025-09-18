using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Common
{
    public static class Helpers
    {
        public enum Family
        {
            param,
            field,
            ret,
        }

        public static string ConvertToCSharpType(string type, Family family = Family.field)
        {
            if (!type.Equals("const char*"))
                type = type.Replace("const ", "");

            if (type.Contains("(*)")) // Delegates
                return "IntPtr";

            switch (type)
            {
                case "char":
                case "unsigned char":
                    return "byte";
                case "bool":
                    switch (family)
                    {
                        case Family.param:
                            return "[MarshalAs(UnmanagedType.I1)] bool";
                        case Family.ret:
                            return "bool";
                        case Family.field:
                        default:
                            return "byte";
                    }
                case "bool*":
                    if (family == Family.param)
                        return "[MarshalAs(UnmanagedType.I1)] ref byte";
                    else
                        return "byte*";
                case "char*":
                if (family == Family.param)
                    return "[MarshalAs(UnmanagedType.I1)] ref byte";
                else
                    return "byte*";
                case "const char*":
                case "unsigned char*":
                    switch (family)
                    {
                        case Family.param:
                            return "[MarshalAs(UnmanagedType.LPUTF8Str)] string";
                        case Family.ret:
                            return "string";
                        case Family.field:
                        default:
                            return "byte*";
                    }
                case "ImVec2":
                    return "Vector2";
                case "ImVec2*":
                if (family == Family.param)
                    return "ref Vector2";
                else
                    return "Vector2*";
                case "ImVec3":
                    return "Vector3";
                case "ImVec3*":
                    if (family == Family.param)
                        return "ref Vector3";
                    else
                        return "Vector3*";
                case "ImVec4":
                    return "Vector4";
                case "ImVec4*":
                    if (family == Family.param)
                        return "ref Vector4";
                    else
                        return "Vector4*";
                case "ImU8":
                    return "byte";
                case "ImU8*":
                    if (family == Family.param)
                        return "ref byte";
                    else
                        return "byte*";
                case "ImS8":
                    return "sbyte";
                case "ImS8*":
                    if (family == Family.param)
                        return "ref sbyte";
                    else
                        return "sbyte*";
                case "ImU16":
                case "ImWchar16":
                case "unsigned short":
                case "ImWchar":
                case "ImDrawIdx":
                    return "ushort";
                case "ImU16*":
                case "ImDrawIdx*":
                case "ImWchar*":
                    if (family == Family.param)
                        return "ref ushort";
                    else
                        return "ushort*";
                case "ImS16":
                    return "short";
                case "ImS16*":
                    if (family == Family.param)
                        return "ref short";
                    else
                        return "short*";
                case "ImU32":
                case "unsigned int":
                case "ImGuiID":
                case "size_t":
                    return "uint";
                case "unsigned int*":
                case "ImU32*":
                case "size_t*":
                case "ImGuiID*":
                    if (family == Family.param)
                        return "ref uint";
                    else
                        return "uint*";
                case "ImS32":
                    return "int";
                case "ImS32*":
                    if (family == Family.param)
                        return "ref int";
                    else
                        return "int*";
                case "ImU64":
                case "ImTextureID":
                    return "ulong";
                case "ImU64*":
                if (family == Family.param)
                        return "ref ulong";
                    else
                        return "ulong*";
                case "ImS64":
                case "ImGuiSelectionUserData":
                    return "long";
                case "ImS64*":
                    if (family == Family.param)
                        return "ref long";
                    else
                        return "long*";
                case "unsigned char**":
                    return "byte**";
                case "char* const[]":
                    switch (family)
                    {
                        case Family.param:
                            return "[MarshalAs(UnmanagedType.CustomMarshaler, MarshalTypeRef = typeof(Utf8StringArrayMarshaler))] string[]";
                        case Family.ret:
                            return "string[]";
                        case Family.field:
                        default:
                            return "byte**";
                    }
                case "ImVector_ImDrawCmd":
                case "ImVector_ImDrawIdx":
                case "ImVector_ImDrawVert":
                case "ImVector_ImVec4":
                case "ImVector_ImTextureID":
                case "ImVector_ImVec2":
                case "ImVector_ImDrawChannel":
                case "ImVector_ImFontPtr":
                case "ImVector_ImFontAtlasCustomRect":
                case "ImVector_ImFontConfig":
                case "ImVector_ImWchar":
                case "ImVector_ImGuiStoragePair":
                case "ImVector_ImGuiTextRange":
                case "ImVector_char":
                case "ImVector_float":
                case "ImVector_ImFontGlyph":
                case "ImVector_ImGuiPlatformMonitor":
                case "ImVector_ImGuiViewportPtr":
                case "ImVector_ImU8":
                case "ImVector_ImU16":
                case "ImVector_ImU32":
                case "ImVector_ImGuiSelectionRequest":
                case "ImVector_ImDrawListPtr":
                    return "ImVector";
                case "ImVector_ImWchar*":
                case "ImVector_ImGuiTextRange*":
                    return "ImVector*";
                case "ImGuiContext*":
                case "ImNodesContext*":
                case "ImPlotContext*":
                case "EditorContext*":
                case "ImNodesEditorContext*":
                case "ImNodesMiniMapNodeHoveringCallback":
                case "ImNodesMiniMapNodeHoveringCallbackUserData":
                case "ImDrawListSharedData*":
                case "ImFontBuilderIO*":
                case "ImDrawCallback":
                case "ImGuiWindow*":
                case "ImGuiMemAllocFunc":
                case "ImGuiMemFreeFunc":
                case "ImGuiPlatformIO*":
                case "ImPlotTransform":
                    return "IntPtr";
                case "ImGuiMemAllocFunc*":
                case "ImGuiMemFreeFunc*":
                    return "IntPtr*";
                case "float[2]":
                    if (family == Family.param)
                        return "ref Vector2";
                    else
                        return "Vector2*";
                case "ImVec2[2]":
                    return "Vector2*";
                case "float[3]":
                    if (family == Family.param)
                        return "ref Vector3";
                    else
                        return "Vector3*";
                case "float[4]":
                    if (family == Family.param)
                        return "ref Vector4";
                    else
                        return "Vector4*";
                case "int[2]":
                case "int[3]":
                case "int[4]":
                    return "int*";
                case "ImPlotFormatter":
                    return "void*";
                case "ImPlotGetter":
                    return "ImPlotPoint*";
                case "ImGuiKeyChord":
                    return "ImGuiKey";
                case "float*":
                    if (family == Family.param)
                        return "ref float";
                    else
                        return "float*";
                case "int*":
                    if (family == Family.param)
                        return "ref int";
                    else
                        return "int*";
                default:
                    return type;
            }
        }

        public static string ConvertDefault(string value, string type, Specification spec)
        {
            if (type.EndsWith("bool"))
                return value == "NULL" ? "false" : "true";

            if (spec.Enums.Exists(e => e.FrienlyName == type) && value != "0")
                return $"({type}) {value}";

            if (type == "ImGuiContext*" || type == "ImPlotContext" || type == "EditorContext*")
                return "IntPtr.Zero";

            if (value == "NULL")
                return "null";

            if (value.StartsWith("ImVec"))
                return null;

            return value;
        }

        public static string GetReturnTypeHeader(string csType)
        {
            switch (csType)
            {
                case "string":
                    return "[return:MarshalAs(UnmanagedType.LPUTF8Str)]";
                case "bool":
                    return "[return:MarshalAs(UnmanagedType.I1)]";
                default:
                    return null;
            }
        }

        public static bool SupportFixed(string type)
        {
            switch (type)
            {
                case "bool":
                case "byte":
                case "short":
                case "int":
                case "long":
                case "char":
                case "sbyte":
                case "ushort":
                case "uint":
                case "ulong":
                case "float":
                case "double":
                    return true;
                default:
                    return false;
            }
        }
    }
}
