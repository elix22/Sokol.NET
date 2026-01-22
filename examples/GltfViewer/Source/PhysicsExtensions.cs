using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Nodes;
using SharpGLTF.Schema2;
using System.Collections.Generic;
using System.Numerics;
using System.Diagnostics.CodeAnalysis;
using static Sokol.SLog;
/// <summary>
/// Flexible JSON converter for integer arrays that can handle both int[] and string[] node references
/// </summary>
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
public class FlexibleIntArrayConverter : JsonConverter<int[]?>
{
    public override int[]? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;
            
        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException($"Expected array, got {reader.TokenType}");
            
        var list = new List<int>();
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
                break;
                
            if (reader.TokenType == JsonTokenType.Number)
            {
                try
                {
                    // Try to get as Int32 first
                    if (reader.TryGetInt32(out int intValue))
                    {
                        list.Add(intValue);
                    }
                    else
                    {
                        // Might be a double or out of range - try to convert
                        double doubleValue = reader.GetDouble();
                        int convertedValue = (int)Math.Round(doubleValue);
                        
                        // Only warn if there's a significant fractional part (not just 10.0 -> 10)
                        if (Math.Abs(doubleValue - convertedValue) > 0.001)
                        {
                            Console.WriteLine($"[Physics] Warning: Converting non-integer node index {doubleValue} to {convertedValue}");
                        }
                        
                        list.Add(convertedValue);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Physics] Error parsing node index as number: {ex.Message}");
                }
            }
            else if (reader.TokenType == JsonTokenType.String)
            {
                // Handle string node references (e.g., "ChildA" -> parse to index or skip)
                string? str = reader.GetString();
                if (int.TryParse(str, out int intValue))
                {
                    list.Add(intValue);
                }
                else
                {
                    // GLB might use string node names instead of indices
                    // For now, skip non-numeric strings
                    Console.WriteLine($"[Physics] Warning: Non-numeric node reference in trigger: '{str}'");
                }
            }
        }
        
        return list.Count > 0 ? list.ToArray() : null;
    }
    
    public override void Write(Utf8JsonWriter writer, int[]? value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }
        
        writer.WriteStartArray();
        foreach (var item in value)
        {
            writer.WriteNumberValue(item);
        }
        writer.WriteEndArray();
    }
}

/// <summary>
/// JSON serialization context for OMI physics extensions
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = false, PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(OMI_physics_shape))]
[JsonSerializable(typeof(OMI_physics_body))]
internal partial class OMIPhysicsJsonContext : JsonSerializerContext
{
}

/// <summary>
/// OMI_physics_shape extension data
/// Spec: https://github.com/omigroup/gltf-extensions/tree/main/extensions/2.0/OMI_physics_shape
/// </summary>
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
public class OMI_physics_shape
{
    public PhysicsShape[]? Shapes { get; set; }

    public class PhysicsShape
    {
        public string? Type { get; set; }  // "box", "sphere", "capsule", "cylinder", "convex", "trimesh"
        
        // Box shape properties
        public BoxShape? Box { get; set; }
        
        // Sphere shape properties
        public SphereShape? Sphere { get; set; }
        
        // Capsule shape properties
        public CapsuleShape? Capsule { get; set; }
        
        // Cylinder shape properties
        public CylinderShape? Cylinder { get; set; }
        
        // Convex/Trimesh properties
        public MeshShape? Convex { get; set; }
        public MeshShape? Trimesh { get; set; }
    }

    public class BoxShape
    {
        public float[]? Size { get; set; }  // [x, y, z] half extents
    }

    public class SphereShape
    {
        public float? Radius { get; set; }
    }

    public class CapsuleShape
    {
        public float? Height { get; set; }  // Mid-height (distance between hemisphere centers), default: 1.0
        public float? RadiusBottom { get; set; }  // Bottom radius, default: 0.5
        public float? RadiusTop { get; set; }  // Top radius, default: 0.5
    }

    public class CylinderShape
    {
        public float? Height { get; set; }  // Total height, default: 2.0
        public float? RadiusBottom { get; set; }  // Bottom radius, default: 0.5
        public float? RadiusTop { get; set; }  // Top radius, default: 0.5
    }

    public class MeshShape
    {
        public int Mesh { get; set; }  // Index of mesh to use
    }
}

/// <summary>
/// OMI_physics_body extension data
/// Spec: https://github.com/omigroup/gltf-extensions/tree/main/extensions/2.0/OMI_physics_body
/// </summary>
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
public class OMI_physics_body
{
    public MotionData? Motion { get; set; }
    public ColliderData? Collider { get; set; }
    public TriggerData? Trigger { get; set; }
    
    // Document-level arrays
    public PhysicsMaterial[]? PhysicsMaterials { get; set; }
    public CollisionFilter[]? CollisionFilters { get; set; }

    public class MotionData
    {
        public string Type { get; set; } = "dynamic";  // "static", "kinematic", "dynamic"
        public float? Mass { get; set; }  // Default: 1.0 kg
        public float[]? InertiaDiagonal { get; set; }  // [x, y, z] inertia tensor diagonal
        public float[]? InertiaOrientation { get; set; }  // [x, y, z, w] quaternion
        public float[]? CenterOfMass { get; set; }  // [x, y, z] offset
        public float[]? LinearVelocity { get; set; }  // [x, y, z] m/s
        public float[]? AngularVelocity { get; set; }  // [x, y, z] rad/s
        public float? GravityFactor { get; set; }  // Default: 1.0
    }

    public class ColliderData
    {
        public int Shape { get; set; }  // Index into OMI_physics_shape.shapes array
        public int? PhysicsMaterial { get; set; }  // Index into physicsMaterials array
        public int? CollisionFilter { get; set; }  // Index into collisionFilters array
    }

    public class TriggerData
    {
        public int? Shape { get; set; }  // Optional: single shape for this trigger
        
        [JsonConverter(typeof(FlexibleIntArrayConverter))]
        public int[]? Nodes { get; set; }  // Optional: array of node indices for compound triggers
        
        public int? CollisionFilter { get; set; }  // Index into collisionFilters array
    }
    
    public class PhysicsMaterial
    {
        public float? StaticFriction { get; set; }  // Default: 0.6
        public float? DynamicFriction { get; set; }  // Default: 0.6
        public float? Restitution { get; set; }  // Default: 0.0 (no bounce)
        public string? FrictionCombine { get; set; }  // "average", "minimum", "maximum", "multiply" (default: "average")
        public string? RestitutionCombine { get; set; }  // "average", "minimum", "maximum", "multiply" (default: "average")
    }
    
    public class CollisionFilter
    {
        public string[]? CollisionSystems { get; set; }  // Layer names this object is a member of
        public string[]? CollideWithSystems { get; set; }  // Layer names to collide with (whitelist)
        public string[]? NotCollideWithSystems { get; set; }  // Layer names to NOT collide with (blacklist)
    }
}

/// <summary>
/// Helper to parse physics extensions from glTF
/// </summary>
public static class PhysicsExtensionParser
{
    private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
        TypeInfoResolver = OMIPhysicsJsonContext.Default
    };

    public static OMI_physics_shape? ParsePhysicsShapeExtension(ModelRoot modelRoot)
    {
        try
        {
            // Access unknown extensions by filtering the Extensions collection
            var ext = modelRoot.Extensions.OfType<SharpGLTF.IO.UnknownNode>()
                .FirstOrDefault(e => e.Name == "OMI_physics_shape");
            if (ext != null)
            {
                // Convert Properties dictionary to JsonObject and serialize
                var jsonObj = new JsonObject();
                foreach (var kvp in ext.Properties)
                {
                    jsonObj[kvp.Key] = kvp.Value?.DeepClone();
                }
                var json = jsonObj.ToJsonString();
                Info($"[Physics] Parsing OMI_physics_shape extension: {json.Substring(0, Math.Min(100, json.Length))}...");
                return JsonSerializer.Deserialize(json, OMIPhysicsJsonContext.Default.OMI_physics_shape);
            }
        }
        catch (Exception ex)
        {
            Error($"Failed to parse OMI_physics_shape: {ex.Message}");
        }
        
        return null;
    }

    public static OMI_physics_body? ParsePhysicsBodyDocumentExtension(ModelRoot modelRoot)
    {
        try
        {
            var ext = modelRoot.Extensions.OfType<SharpGLTF.IO.UnknownNode>()
                .FirstOrDefault(e => e.Name == "OMI_physics_body");
            if (ext != null)
            {
                // Convert Properties dictionary to JsonObject and serialize
                var jsonObj = new JsonObject();
                foreach (var kvp in ext.Properties)
                {
                    jsonObj[kvp.Key] = kvp.Value?.DeepClone();
                }
                var json = jsonObj.ToJsonString();
                Info($"[Physics] Parsing OMI_physics_body document extension");
                return JsonSerializer.Deserialize(json, OMIPhysicsJsonContext.Default.OMI_physics_body);
            }
        }
        catch (Exception ex)
        {
            Error($"Failed to parse OMI_physics_body document extension: {ex.Message}");
        }
        
        return null;
    }

    public static OMI_physics_body? ParsePhysicsBodyExtension(Node node)
    {
        try
        {
            var ext = node.Extensions.OfType<SharpGLTF.IO.UnknownNode>()
                .FirstOrDefault(e => e.Name == "OMI_physics_body");
            if (ext != null)
            {
                // Convert Properties dictionary to JsonObject and serialize
                var jsonObj = new JsonObject();
                foreach (var kvp in ext.Properties)
                {
                    jsonObj[kvp.Key] = kvp.Value?.DeepClone();
                }
                var json = jsonObj.ToJsonString();
                Info($"[Physics] Parsing OMI_physics_body for node '{node.Name}': {json.Substring(0, Math.Min(100, json.Length))}...");
                return JsonSerializer.Deserialize(json, OMIPhysicsJsonContext.Default.OMI_physics_body);
            }
        }
        catch (Exception ex)
        {
            Error($"Failed to parse OMI_physics_body for node {node.Name}: {ex.Message}");
        }
        
        return null;
    }
}

