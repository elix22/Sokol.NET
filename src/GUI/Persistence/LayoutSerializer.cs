using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sokol.GUI;

/// <summary>
/// AOT-safe JSON (de)serializer for <see cref="LayoutData"/> using
/// <see cref="JsonSerializerContext"/> source generation.
/// </summary>
public static class LayoutSerializer
{
    private static readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string Save(LayoutData layout)
        => JsonSerializer.Serialize(layout, LayoutJsonContext.Default.LayoutData);

    public static LayoutData? Load(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        return JsonSerializer.Deserialize(json, LayoutJsonContext.Default.LayoutData);
    }
}

/// <summary>
/// Source-generated serialization context for <see cref="LayoutData"/>.
/// AOT-safe because no runtime reflection is used; the generator produces
/// typed metadata at compile time.
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    GenerationMode = JsonSourceGenerationMode.Default)]
[JsonSerializable(typeof(LayoutData))]
[JsonSerializable(typeof(DockNodeData))]
[JsonSerializable(typeof(DockPanelData))]
internal partial class LayoutJsonContext : JsonSerializerContext { }
