// SkinnedMeshRenderer.cs — ECS component for one primitive of a GPU-skinned glTF character.
//
// Holds ONLY serializable keys — the live SkinnedMesh + CGltfAnimator live in
// SkinnedCharacterRegistry (keyed by CharacterKey) and are resolved at draw time. This keeps the
// component serializable, so skinned characters survive scene save/load and the Play→Stop snapshot
// (the registry is a static that outlives the round-trip).
// (Per [[project_animation_rendering_boundary]] this is the renderer-owned GPU side; a later stage
// moves the joints into real ECS entities.)

namespace GameEditor.Framework.Renderer.Server.Animation
{
    public struct SkinnedMeshRenderer
    {
        /// <summary>SkinnedCharacterRegistry key (the source glTF path) — shared by the character's primitives.</summary>
        public string CharacterKey;

        /// <summary>Running primitive index within the character (selects the SkinnedMesh in the registry entry).</summary>
        public int PrimIndex;

        /// <summary>PbrMaterial registry key ("path#material"); empty = fallback material.</summary>
        public string MaterialKey;

        public bool Visible;
        public bool ReceivesShadows;
        public bool CastsShadows;

        // ── Playback (G6 A0): the character's animator follows its first skinned mesh's settings ──────────────
        /// <summary>The clip it plays, by its name in the character's file (SkinnedCharacterRegistry.Entry.Clips); "" = the first.</summary>
        public string Clip;
        /// <summary>1 = the clip as made; 2 = twice as fast.</summary>
        public float Speed;
        /// <summary>Time moves; off, the pose holds.</summary>
        public bool Playing;
        /// <summary>Round and round; off, it stops on the clip's last pose.</summary>
        public bool Loop;

    }
}
