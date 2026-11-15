// Events.cs — §6 (examples/GameEditor/docs/EDITOR_ENHANCEMENTS_DESIGN.md): typed events between scripts.
//
//     public readonly record struct PlayerDamaged(int Amount);
//     Events.Publish(new PlayerDamaged(3));                  // what happened
//     On<PlayerDamaged>(e => _health -= e.Amount);           // in a script: who cares (ends with the script)
//
// One static list of handlers per event type (a generic class: no reflection, no lookup by name - a game compiled
// ahead of time has every type it uses). A handler a script added is taken away when that script ends (it leaves the
// scene, Play stops): no handler outlives its script. A handler added in a handler runs from the next Publish; one
// taken away in a handler is not called again.

using System;
using System.Collections.Generic;

namespace GameEditor.Framework.Scripting
{
    public static class Events
    {
        // Every channel used, to empty them all (Play stops) and to take a script's handlers out of each.
        private static readonly List<IChannel> _channels = new();

        private interface IChannel
        {
            void RemoveOwner(object owner);
            void Clear();
        }

        private sealed class Channel<T> : IChannel
        {
            public static readonly Channel<T> Instance = Make();
            private readonly List<(object? Owner, Action<T> Handler)> _handlers = new();
            private (object? Owner, Action<T> Handler)[] _snapshot = Array.Empty<(object?, Action<T>)>();
            private bool _changed;

            private static Channel<T> Make()
            {
                var c = new Channel<T>();
                lock (_channels) _channels.Add(c);
                return c;
            }

            public void Add(object? owner, Action<T> handler) { _handlers.Add((owner, handler)); _changed = true; }

            public bool Remove(Action<T> handler)
            {
                for (int i = 0; i < _handlers.Count; i++)
                    if (_handlers[i].Handler == handler) { _handlers.RemoveAt(i); _changed = true; return true; }
                return false;
            }

            public void RemoveOwner(object owner)
            {
                if (_handlers.RemoveAll(h => ReferenceEquals(h.Owner, owner)) > 0) _changed = true;
            }

            public void Clear() { _handlers.Clear(); _changed = true; }

            public int Count => _handlers.Count;

            public void Publish(T e)
            {
                if (_changed) { _snapshot = _handlers.ToArray(); _changed = false; }
                var now = _snapshot;
                for (int i = 0; i < now.Length; i++)
                {
                    var (owner, handler) = now[i];
                    if (_changed && !Still(owner, handler)) continue;      // taken away by a handler before it
                    try { handler(e); }
                    catch (Exception ex) { Core.Logger.Error($"[Events] {typeof(T).Name}: {ex.Message}"); }
                }
            }

            private bool Still(object? owner, Action<T> handler)
            {
                for (int i = 0; i < _handlers.Count; i++)
                    if (_handlers[i].Handler == handler && ReferenceEquals(_handlers[i].Owner, owner)) return true;
                return false;
            }
        }

        /// <summary>Tells every handler of <typeparamref name="T"/> that it happened (in the order they were added). A
        /// handler that throws is logged; the others still run.</summary>
        public static void Publish<T>(T e) => Channel<T>.Instance.Publish(e);

        /// <summary>
        /// Calls <paramref name="handler"/> on each <typeparamref name="T"/> published, until <see cref="Off{T}"/> - or,
        /// with an <paramref name="owner"/> (a script: <see cref="GameBehaviour.On{T}"/> passes itself), until that
        /// script ends. Returns the handler, for Off.
        /// </summary>
        public static Action<T> On<T>(Action<T> handler, object? owner = null)
        {
            Channel<T>.Instance.Add(owner, handler);
            return handler;
        }

        /// <summary>Takes a handler away; false when it was not there.</summary>
        public static bool Off<T>(Action<T> handler) => Channel<T>.Instance.Remove(handler);

        /// <summary>How many handlers <typeparamref name="T"/> has now.</summary>
        public static int Listeners<T>() => Channel<T>.Instance.Count;

        /// <summary>Every handler <paramref name="owner"/> added, of every event, taken away (a script ended).</summary>
        public static void RemoveOwner(object owner)
        {
            IChannel[] all;
            lock (_channels) all = _channels.ToArray();
            foreach (var c in all) c.RemoveOwner(owner);
        }

        /// <summary>Every handler of every event taken away (Play stopped, a scene changed).</summary>
        public static void Clear()
        {
            IChannel[] all;
            lock (_channels) all = _channels.ToArray();
            foreach (var c in all) c.Clear();
        }
    }
}
