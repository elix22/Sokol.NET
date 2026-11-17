namespace GameEditor.Framework.Renderer.Server.Lod
{
    /// <summary>
    /// One level in a LOD ladder.  Levels should be sorted descending by
    /// <see cref="ScreenCoverageThreshold"/> (highest coverage = closest = LOD 0).
    ///
    /// Attach a <see cref="LodGroup"/> to an entity (Frent ECS component) alongside
    /// <see cref="GameEditor.Framework.ECS.Components.MeshRenderer"/> to enable
    /// per-entity LOD selection.  When no <see cref="LodGroup"/> is present the renderer
    /// treats the entity as single-level (always LOD 0) and never distance-culls it.
    /// </summary>
    public struct LodLevel
    {
        /// <summary>
        /// Coverage fraction at which the renderer switches <em>to</em> this level.
        /// Coverage ≈ worldRadius / distanceToCamera (dimensionless ratio in [0, ∞)).
        /// Level 0 threshold is typically 1.0 (covers at least its own diameter on screen).
        /// </summary>
        public float ScreenCoverageThreshold;

        /// <summary>
        /// Hysteresis margin to avoid LOD flickering near threshold boundaries.
        /// Switch <em>down</em> when coverage &lt; threshold − margin;
        /// switch <em>up</em>   when coverage &gt; threshold + margin.
        /// Default: 0.02 (2 % of screen height).
        /// </summary>
        public float HysteresisMargin;

        /// <summary>
        /// The mesh drawn at this level (a mesh path as <see cref="GameEditor.Framework.ECS.Components.MeshRenderer.MeshPath"/>
        /// has it: a primitive, or a mesh of a model); null or empty: the entity's own mesh (Phase H E6). The entity's material
        /// is used; a model's mesh keeps its own sub-mesh materials as its MeshRenderer would.
        /// </summary>
        public string? Mesh;

        public LodLevel(float threshold, float hysteresis = 0.02f, string? mesh = null)
        {
            ScreenCoverageThreshold = threshold;
            HysteresisMargin        = hysteresis;
            Mesh                    = mesh;
        }
    }

    /// <summary>
    /// ECS component that defines a LOD ladder for an entity.
    /// Levels must be sorted <b>descending</b> by <see cref="LodLevel.ScreenCoverageThreshold"/>
    /// (highest coverage = closest detail = index 0).
    ///
    /// Each level's <see cref="LodLevel.HysteresisMargin"/> must be smaller than the
    /// coverage gap to the next coarser threshold (the constructor asserts this). If a
    /// margin is too large, that level's stay-bar (<c>threshold − margin</c>) collapses to
    /// at or below the next threshold and the entity can never downgrade or cull from it —
    /// it gets pinned at that level forever.
    ///
    /// Example (3-level object) — note the explicit margins, each well under its gap:
    /// <code>
    ///   new LodGroup(
    ///       new LodLevel(0.10f,  0.01f),   // LOD 0 — high detail when close
    ///       new LodLevel(0.02f,  0.004f),  // LOD 1 — medium detail
    ///       new LodLevel(0.005f, 0.001f)); // LOD 2 — low detail; below 0.005 → Skip (cull)
    /// </code>
    /// (The <see cref="LodLevel"/> default margin of 0.02 suits ladders whose thresholds
    /// live on a ~[0,1] coverage scale; small-threshold ladders like the one above must
    /// pass smaller margins, or the constructor assert will fire — see
    /// <see cref="LodSelector.Skip"/> and RENDERING_SERVER_M3_REVIEW.md §3.10.)
    /// </summary>
    public struct LodGroup
    {
        /// <summary>LOD levels sorted descending by ScreenCoverageThreshold.</summary>
        public LodLevel[] Levels;

        public LodGroup(params LodLevel[] levels)
        {
            System.Diagnostics.Debug.Assert(levels.Length <= 16,
                $"LodGroup: {levels.Length} levels exceeds the 4-bit sort-key capacity (max 16). Wrap-around would mis-group draw commands.");

            // Each level's hysteresis margin must be smaller than the coverage gap to the
            // next coarser level (the last level's "next" is the Skip/cull boundary at 0).
            // Otherwise the stay-bar (threshold − margin) drops to <= the coarser threshold
            // and the entity can never downgrade/cull from this level — it is pinned forever.
            // This also catches levels that are not sorted strictly descending (gap <= 0).
            // See RENDERING_SERVER_M3_REVIEW.md §3.10.
            for (int i = 0; i < levels.Length; i++)
            {
                float nextThr  = (i + 1 < levels.Length) ? levels[i + 1].ScreenCoverageThreshold : 0f;
                float gapBelow = levels[i].ScreenCoverageThreshold - nextThr;
                System.Diagnostics.Debug.Assert(levels[i].HysteresisMargin < gapBelow,
                    $"LodGroup level {i}: HysteresisMargin {levels[i].HysteresisMargin} must be < the gap to the next coarser threshold ({gapBelow}); a larger margin pins the entity at this level (can never downgrade/cull). Lower the margin or space the thresholds further apart.");
            }

            Levels = levels;
        }
    }
}
