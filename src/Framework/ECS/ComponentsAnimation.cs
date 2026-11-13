namespace GameEditor.Framework.ECS.Components
{
    /// <summary>
    /// Plays a clip (a <c>.anim.json</c> file) on its entity and the entities below it while the game plays — Unity's
    /// Animation component (scene block "AnimationPlayer"). The clip's curves name what they move by path below this
    /// entity; its events go to this entity's scripts (<see cref="Scripting.GameBehaviour.OnAnimationEvent"/>). A script
    /// plays another clip by setting <see cref="Clip"/> (it starts from 0), stops one by clearing <see cref="Playing"/>.
    /// </summary>
    public struct AnimationPlayer
    {
        /// <summary>The clip's key below Assets ("Animations/DoorOpen.anim.json").</summary>
        public string Clip;
        /// <summary>The clip plays when the game starts (Unity's Play Automatically); else when a script sets <see cref="Playing"/>.</summary>
        public bool PlayAutomatically;
        /// <summary>1 plays the clip at its own pace; 2 twice as fast; a negative speed plays it backwards.</summary>
        public float Speed;

        // ── Set while the game plays; not saved ──
        public bool Playing;
        /// <summary>Seconds of the clip played since it started, at its speed — round and round a looping clip it keeps
        /// counting (Unity's AnimationState.time); its pose is the clip's at this time wrapped by the clip's wrap mode.</summary>
        public float Time;
        /// <summary>The clip <see cref="Time"/> belongs to: another <see cref="Clip"/> starts from 0.</summary>
        public string? Current;

        /// <summary>A new player: nothing to play yet, playing automatically at speed 1.</summary>
        public static AnimationPlayer Default => new() { Clip = "", PlayAutomatically = true, Speed = 1f, Playing = true };
    }
}
