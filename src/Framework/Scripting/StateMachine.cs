// StateMachine.cs — §6 (examples/GameEditor/docs/EDITOR_ENHANCEMENTS_DESIGN.md): a behaviour's states, instead of an
// OnUpdate full of flags.
//
//     enum Mode { Idle, Chase, Attack }
//     readonly StateMachine<Mode> _ai = new(Mode.Idle);
//     public override void OnStart()
//     {
//         _ai.In(Mode.Idle,   update: dt => { if (Near()) _ai.Go(Mode.Chase); })
//            .In(Mode.Chase,  enter: () => Run(), update: dt => MoveTo(_target), exit: () => Walk())
//            .In(Mode.Attack, enter: () => Swing());
//     }
//     public override void OnUpdate(float dt) => _ai.Update(dt);
//
// Go changes the state at once: the old state's exit, then the new one's enter. Enter of the first state runs on the
// first Update. No reflection (built games are compiled ahead of time).

using System;
using System.Collections.Generic;

namespace GameEditor.Framework.Scripting
{
    public sealed class StateMachine<TState> where TState : struct, Enum
    {
        private readonly struct Handlers
        {
            public readonly Action? Enter, Exit;
            public readonly Action<float>? Update;
            public Handlers(Action? enter, Action<float>? update, Action? exit) { Enter = enter; Update = update; Exit = exit; }
        }

        private readonly Dictionary<TState, Handlers> _states = new();
        private bool _entered;

        public StateMachine(TState initial) { State = initial; Previous = initial; }

        /// <summary>The state it is in.</summary>
        public TState State { get; private set; }

        /// <summary>The state it was in before (the initial one before any change).</summary>
        public TState Previous { get; private set; }

        /// <summary>Seconds in the state (Update's time; 0 at each change).</summary>
        public float TimeInState { get; private set; }

        /// <summary>What a state does: on entering it, each Update while in it, on leaving it. Returns the machine (chained).</summary>
        public StateMachine<TState> In(TState state, Action? enter = null, Action<float>? update = null, Action? exit = null)
        {
            _states[state] = new Handlers(enter, update, exit);
            return this;
        }

        /// <summary>True in <paramref name="state"/>.</summary>
        public bool Is(TState state) => EqualityComparer<TState>.Default.Equals(State, state);

        /// <summary>Goes to <paramref name="state"/> now: the current state's exit, then its enter (also into the same
        /// state: it starts again).</summary>
        public void Go(TState state)
        {
            if (_entered && _states.TryGetValue(State, out var old)) old.Exit?.Invoke();
            Previous = State;
            State = state;
            TimeInState = 0f;
            _entered = true;
            if (_states.TryGetValue(state, out var now)) now.Enter?.Invoke();
        }

        /// <summary>Once a frame: the state's update (the first call enters the initial state first).</summary>
        public void Update(float deltaTime)
        {
            if (!_entered)
            {
                _entered = true;
                if (_states.TryGetValue(State, out var first)) first.Enter?.Invoke();
            }
            TimeInState += deltaTime;
            if (_states.TryGetValue(State, out var h)) h.Update?.Invoke(deltaTime);
        }
    }
}
