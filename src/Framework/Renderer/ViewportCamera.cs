using System;
using System.Numerics;
using static Sokol.SApp;

namespace GameEditor.Framework.Renderer
{
    /// <summary>
    /// Orbit/pan/zoom viewport camera adapted from CGltfViewer.
    /// </summary>
    public class ViewportCamera
    {
        public Matrix4x4 View { get; private set; }
        public Matrix4x4 Proj { get; private set; }
        public Vector3 EyePos { get; private set; }
        public Matrix4x4 ViewProj => View * Proj;

        private Vector3 _center = Vector3.Zero;
        private float _distance = 5f;
        private float _yaw = 0f;
        private float _pitch = 0.3f;
        private float _fov = 60f;
        private float _nearZ = 0.1f;
        private float _farZ = 1000f;

        // Input state
        private bool _orbiting;
        private bool _panningRMB; // right button pan
        private float _lastMouseX;
        private float _lastMouseY;

        public Vector3 Center { get => _center; set => _center = value; }
        public float Distance { get => _distance; set => _distance = MathF.Max(0.01f, value); }

        public void Init(Vector3 center, float distance, float yaw = 0f, float pitch = 0.3f,
                         float fov = 60f, float nearZ = 0.1f, float farZ = 1000f)
        {
            _center = center;
            _distance = distance;
            _yaw = yaw;
            _pitch = pitch;
            _fov = fov;
            _nearZ = nearZ;
            _farZ = farZ;
        }

        public void Update(int width, int height)
        {
            if (width <= 0 || height <= 0) return;
            float aspect = (float)width / height;

            Proj = Matrix4x4.CreatePerspectiveFieldOfView(
                _fov * MathF.PI / 180f, aspect, _nearZ, _farZ);

            Vector3 offset = new Vector3(
                MathF.Cos(_pitch) * MathF.Sin(_yaw),
                MathF.Sin(_pitch),
                MathF.Cos(_pitch) * MathF.Cos(_yaw));

            EyePos = _center + Vector3.Normalize(offset) * _distance;
            View = Matrix4x4.CreateLookAt(EyePos, _center, Vector3.UnitY);
        }

        public void HandleMouseDown(sapp_mousebutton btn, float x, float y)
        {
            if (btn == sapp_mousebutton.SAPP_MOUSEBUTTON_MIDDLE) { _orbiting   = true; }
            if (btn == sapp_mousebutton.SAPP_MOUSEBUTTON_RIGHT)  { _panningRMB = true; }
            _lastMouseX = x;
            _lastMouseY = y;
        }

        public void HandleMouseUp(sapp_mousebutton btn)
        {
            if (btn == sapp_mousebutton.SAPP_MOUSEBUTTON_MIDDLE) _orbiting   = false;
            if (btn == sapp_mousebutton.SAPP_MOUSEBUTTON_RIGHT)  _panningRMB = false;
        }

        public void HandleMouseMove(float x, float y)
        {
            float dx = x - _lastMouseX;
            float dy = y - _lastMouseY;
            _lastMouseX = x;
            _lastMouseY = y;

            if (_orbiting)
            {
                _yaw   -= dx * 0.005f;
                _pitch += dy * 0.005f;
                _pitch  = Math.Clamp(_pitch, -MathF.PI * 0.499f, MathF.PI * 0.499f);
            }

            if (_panningRMB)
            {
                Vector3 right = Vector3.Normalize(Vector3.Cross(_center - EyePos, Vector3.UnitY));
                Vector3 up    = Vector3.UnitY;
                float scale   = _distance * 0.001f;
                _center += -right * dx * scale + up * dy * scale;
            }
        }

        public void HandleScroll(float delta)
        {
            _distance = MathF.Max(0.01f, _distance - delta * _distance * 0.1f);
        }

        /// <summary>
        /// Sync camera yaw/pitch/center from a view matrix modified by ViewManipulate.
        /// Keeps the current orbit distance unchanged.
        /// </summary>
        public void SetViewFromMatrix(Matrix4x4 view)
        {
            if (!Matrix4x4.Invert(view, out var inv)) return;

            // In the inverted view matrix (row-major System.Numerics layout,
            // built by Matrix4x4.CreateLookAt):
            //   Row 2 (M31/M32/M33) = view's z-column = back vector  (eye − center direction).
            //   Row 3 (M41/M42/M43) = camera eye position in world space.
            //
            // Camera orbit convention (from Update()):
            //   offset = (cos(pitch)·sin(yaw), sin(pitch), cos(pitch)·cos(yaw))
            //   EyePos = center + offset · distance          ← offset IS the back vector
            //
            // Therefore:   back.Y = sin(pitch),   Atan2(back.X, back.Z) = yaw.
            // DO NOT negate to "forward" — that flips pitch sign and rotates yaw by ±π.
            var back = Vector3.Normalize(new Vector3(inv.M31, inv.M32, inv.M33));
            var eye  = new Vector3(inv.M41, inv.M42, inv.M43);

            _pitch  = MathF.Asin(Math.Clamp(back.Y, -1f, 1f));
            _yaw    = MathF.Atan2(back.X, back.Z);
            _center = eye - back * _distance;
        }

        public bool IsCapturing => _orbiting || _panningRMB;
    }
}
