using System;
using System.Numerics;

namespace Sokol
{
    public enum LightType
    {
        Directional = 0,
        Point = 1,
        Spot = 2
    }

    public class Light
    {
        public LightType Type { get; set; }
        public Vector3 Position { get; set; }
        public Vector3 Direction { get; set; }
        public Vector3 Color { get; set; }
        public float Intensity { get; set; }
        public float Range { get; set; }              // For point and spot lights
        public float SpotInnerAngle { get; set; }     // In degrees
        public float SpotOuterAngle { get; set; }     // In degrees
        public bool Enabled { get; set; }

        public Light()
        {
            Type = LightType.Directional;
            Position = Vector3.Zero;
            Direction = new Vector3(0, -1, 0);        // Default pointing down
            Color = Vector3.One;                       // White light
            Intensity = 1.0f;
            Range = 10.0f;
            SpotInnerAngle = 12.5f;                    // Default 12.5 degrees
            SpotOuterAngle = 17.5f;                    // Default 17.5 degrees
            Enabled = true;
        }

        public static Light CreateDirectionalLight(Vector3 direction, Vector3 color, float intensity = 1.0f)
        {
            return new Light
            {
                Type = LightType.Directional,
                Direction = Vector3.Normalize(direction),
                Color = color,
                Intensity = intensity,
                Enabled = true
            };
        }

        public static Light CreatePointLight(Vector3 position, Vector3 color, float range, float intensity = 1.0f)
        {
            return new Light
            {
                Type = LightType.Point,
                Position = position,
                Color = color,
                Intensity = intensity,
                Range = range,
                Enabled = true
            };
        }

        public static Light CreateSpotLight(Vector3 position, Vector3 direction, Vector3 color, 
                                           float range, float innerAngle, float outerAngle, float intensity = 1.0f)
        {
            return new Light
            {
                Type = LightType.Spot,
                Position = position,
                Direction = Vector3.Normalize(direction),
                Color = color,
                Intensity = intensity,
                Range = range,
                SpotInnerAngle = innerAngle,
                SpotOuterAngle = outerAngle,
                Enabled = true
            };
        }
    }
}
