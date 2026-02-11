using System;
using System.Collections.Generic;
using static Sokol.SLog;
using static Sokol.SG;

namespace Sokol
{
    /// <summary>
    /// Diagnostic tool to track sokol_gfx view creation and destruction.
    /// Helps identify view leaks during development.
    /// </summary>
    public static class ViewTracker
    {
        private static int _viewsCreated = 0;
        private static int _viewsDestroyed = 0;
        private static readonly Dictionary<uint, string> _activeViews = new Dictionary<uint, string>();
        private static bool _enabled = true;

        public static void SetEnabled(bool enabled)
        {
            _enabled = enabled;
        }

        public static void TrackViewCreation(sg_view view, string label)
        {
            if (!_enabled) return;
            
            _viewsCreated++;
            _activeViews[view.id] = label;
            
            if (_viewsCreated % 10 == 0)
            {
                Info($"[ViewTracker] Views: {_viewsCreated} created, {_viewsDestroyed} destroyed, {GetActiveCount()} active");
            }
        }

        public static void TrackViewDestruction(sg_view view)
        {
            if (!_enabled) return;
            
            if (view.id != 0 && _activeViews.ContainsKey(view.id))
            {
                _viewsDestroyed++;
                _activeViews.Remove(view.id);
            }
        }

        public static int GetActiveCount() => _activeViews.Count;
        public static int GetCreatedCount() => _viewsCreated;
        public static int GetDestroyedCount() => _viewsDestroyed;

        public static void PrintStats()
        {
            Info("=== View Tracker Statistics ===", "ViewTracker");
            Info($"  Total Created: {_viewsCreated}", "ViewTracker");
            Info($"  Total Destroyed: {_viewsDestroyed}", "ViewTracker");
            Info($"  Currently Active: {GetActiveCount()}", "ViewTracker");
            
            if (GetActiveCount() > 0)
            {
                Info($"  Active Views:", "ViewTracker");
                foreach (var kvp in _activeViews)
                {
                    Info($"    - ID {kvp.Key}: {kvp.Value}", "ViewTracker");
                }
            }
            
            if (_viewsCreated - _viewsDestroyed > 100)
            {
                Warning($"  WARNING: Potential view leak detected! {_viewsCreated - _viewsDestroyed} views not destroyed", "ViewTracker");
            }
        }

        public static void Reset()
        {
            _viewsCreated = 0;
            _viewsDestroyed = 0;
            _activeViews.Clear();
        }
    }
}
