using System;
using System.Numerics;
using Frent;
using GameEditor.Framework.Assets;
using GameEditor.Framework.ECS;
using GameEditor.Framework.ECS.Components;

namespace GameEditor.Framework.Renderer.Server.Sprites2D
{
    /// <summary>
    /// Where a sprite is in the world and when it is drawn — the same answers the renderer draws by, for whoever
    /// needs them without drawing: picking a sprite with the pointer, an outline around it, the box of a 2D body.
    /// </summary>
    public static class SpriteGeometry
    {
        /// <summary>
        /// The quad of a frame of <paramref name="size"/> world units with its <paramref name="pivot"/> on the
        /// entity's position, placed by the entity's world matrix: its bottom-left corner and its two edges.
        /// </summary>
        public static void Quad(in Matrix4x4 model, Vector2 size, Vector2 pivot, out Vector3 origin, out Vector3 axisX, out Vector3 axisY)
        {
            origin = Vector3.Transform(new Vector3(-pivot.X * size.X, -pivot.Y * size.Y, 0f), model);
            axisX  = Vector3.TransformNormal(new Vector3(size.X, 0f, 0f), model);
            axisY  = Vector3.TransformNormal(new Vector3(0f, size.Y, 0f), model);
        }

        /// <summary>
        /// The quad an entity's sprite is drawn on. False when the entity draws none: no sprite, hidden, not active,
        /// or its image has not arrived yet. A sprite whose image is missing is the one-unit square drawn in its place.
        /// </summary>
        public static bool TryGetQuad(ECSWorld world, Entity entity, out Vector3 origin, out Vector3 axisX, out Vector3 axisY)
        {
            origin = axisX = axisY = default;
            if (!world.TryGetComponent<SpriteRenderer>(entity, out var sprite) || !sprite.Visible) return false;
            if (!world.TryGetComponent<ActiveFlag>(entity, out var active) || !active.Active) return false;
            if (!world.TryGetComponent<Transform>(entity, out var transform)) return false;
            var answer = SpriteLibrary.Find(sprite.Texture, out SpriteFrame frame);
            if (answer == SpriteLibrary.Answer.Loading) return false;
            if (answer == SpriteLibrary.Answer.Missing) frame = Missing;
            Quad(Transform.GetWorldMatrix(world, transform), frame.Size, frame.Pivot, out origin, out axisX, out axisY);
            return true;
        }

        /// <summary>What is drawn for a sprite whose image cannot be read: one unit, its middle on the entity.</summary>
        internal static SpriteFrame Missing => new() { Size = Vector2.One, Pivot = new Vector2(0.5f, 0.5f) };

        /// <summary>
        /// The key sprites are drawn by, smaller first: a lower layer, then a lower order, then farther from the
        /// camera. Of two sprites that cover a point, the one with the larger key is the one seen.
        /// </summary>
        public static ulong DrawKey(int layer, int order, Vector3 position, in Matrix4x4 viewProj)
        {
            ulong l = (ulong)(Math.Clamp(layer, short.MinValue, short.MaxValue) - short.MinValue);
            ulong o = (ulong)(Math.Clamp(order, short.MinValue, short.MaxValue) - short.MinValue);
            Vector4 clip = Vector4.Transform(new Vector4(position, 1f), viewProj);
            float depth = clip.W != 0f ? clip.Z / clip.W : 0f;              // 0 at the near plane, 1 at the far one
            uint far = uint.MaxValue - (uint)(Math.Clamp(depth, 0f, 1f) * 4294967040f);
            return (l << 48) | (o << 32) | far;
        }

        /// <summary>
        /// Where a ray meets a quad: <paramref name="t"/> along the ray (from its origin, in units of
        /// <paramref name="direction"/>'s length). False when it misses, runs along the quad, or the quad is behind.
        /// </summary>
        public static bool RayHitsQuad(Vector3 rayOrigin, Vector3 direction, Vector3 origin, Vector3 axisX, Vector3 axisY, out float t)
        {
            t = 0f;
            Vector3 normal = Vector3.Cross(axisX, axisY);
            float facing = Vector3.Dot(normal, direction);
            if (MathF.Abs(facing) < 1e-12f) return false;
            t = Vector3.Dot(normal, origin - rayOrigin) / facing;
            if (t < 0f) return false;
            Vector3 p = rayOrigin + direction * t - origin;
            // p = u * axisX + v * axisY, for edges that need not be at right angles (a sheared parent).
            float xx = Vector3.Dot(axisX, axisX), yy = Vector3.Dot(axisY, axisY), xy = Vector3.Dot(axisX, axisY);
            float det = xx * yy - xy * xy;
            if (det <= 0f) return false;
            float px = Vector3.Dot(p, axisX), py = Vector3.Dot(p, axisY);
            float u = (px * yy - py * xy) / det, v = (py * xx - px * xy) / det;
            return u >= 0f && u <= 1f && v >= 0f && v <= 1f;
        }

        /// <summary>
        /// The sprite seen along a ray: of the sprites the ray meets no farther than <paramref name="maxT"/> (the
        /// first mesh it meets: a sprite behind a mesh is hidden by it), the one drawn last. False when there is none.
        /// </summary>
        public static bool Pick(ECSWorld world, Vector3 rayOrigin, Vector3 direction, in Matrix4x4 viewProj, float maxT, out Entity hit)
        {
            hit = default;
            if (!world.HasAnyComponent<SpriteRenderer>()) return false;
            bool any = false;
            ulong best = 0;
            foreach (Entity e in world.Entities)
            {
                if (!TryGetQuad(world, e, out Vector3 origin, out Vector3 axisX, out Vector3 axisY)) continue;
                if (!RayHitsQuad(rayOrigin, direction, origin, axisX, axisY, out float t) || t > maxT) continue;
                var sprite = world.GetComponent<SpriteRenderer>(e);
                if (sprite.Color.W <= 0f) continue;                 // fully clear: nothing of it is seen
                var model = Transform.GetWorldMatrix(world, world.GetComponent<Transform>(e));
                ulong key = DrawKey(sprite.Layer, sprite.Order, model.Translation, in viewProj);
                if (any && key < best) continue;                    // of equal keys the later entity is drawn later
                any = true; best = key; hit = e;
            }
            return any;
        }
    }
}
