using System;
using System.Numerics;
using static Sokol.SApp;

namespace GameEditor.Framework.Renderer
{
    /// <summary>
    /// Orbit/pan/zoom viewport camera adapted from CGltfViewer.
    /// <para>
    /// <see cref="Is2D"/> makes it the camera of a 2D view: orthographic, looking at the XY plane from +Z, with X to
    /// the right and Y up. Its place is <see cref="Center2D"/> and <see cref="OrthoSize"/>, kept apart from the orbit
    /// values, so leaving the 2D view gives the 3D one back as it was.
    /// </para>
    /// </summary>
    public class ViewportCamera
    {
        public Matrix4x4 View { get; private set; }
        public Matrix4x4 Proj { get; private set; }
        public Vector3 EyePos { get; private set; }
        public Matrix4x4 ViewProj => View * Proj;

        /// <summary>Perspective near/far planes — needed by CSM cascade fitting.</summary>
        public float NearZ => _nearZ;
        public float FarZ  => _farZ;

        private Vector3 _center = Vector3.Zero;
        private float _distance = 5f;
        private float _yaw = 0f;
        private float _pitch = 0.3f;
        private float _fov = 60f;
        private float _nearZ = 0.1f;
        private float _farZ = 1000f;

        // 2D view
        private float _orthoSize = 5f;
        private int _viewW = 1, _viewH = 1;
        private const float Eye2D = 500f;      // the eye's z: the view shows z from -500 up to just below 500

        /// <summary>The camera of a 2D view (see the class).</summary>
        public bool Is2D { get; set; }
        /// <summary>2D view: the world point in the middle of the view.</summary>
        public Vector2 Center2D { get; set; }
        /// <summary>2D view: half the view's height, in world units.</summary>
        public float OrthoSize { get => _orthoSize; set => _orthoSize = Math.Clamp(value, 0.001f, 100000f); }
        /// <summary>2D view: view units per unit of the pointer positions the Handle… calls get (1 / the display's scale).</summary>
        public float PointerScale { get; set; } = 1f;
        /// <summary>2D view: world units per view unit.</summary>
        public float UnitsPerPixel => 2f * _orthoSize / _viewH;

        // Input state
        private bool _orbiting;
        private bool _panningRMB; // right button pan
        private float _lastMouseX;
        private float _lastMouseY;

        public Vector3 Center { get => _center; set => _center = value; }
        public float Distance { get => _distance; set => _distance = MathF.Max(0.01f, value); }
        public float Yaw   { get => _yaw;   set => _yaw   = value; }
        public float Pitch { get => _pitch; set => _pitch  = value; }

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
            _viewW = width; _viewH = height;

            if (Is2D)
            {
                Proj   = Matrix4x4.CreateOrthographic(2f * _orthoSize * aspect, 2f * _orthoSize, _nearZ, _farZ);
                EyePos = new Vector3(Center2D, Eye2D);
                View   = Matrix4x4.CreateLookAt(EyePos, new Vector3(Center2D, 0f), Vector3.UnitY);
                return;
            }

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

            if (Is2D)
            {
                // Either button moves the view with the pointer: the world point under it stays under it.
                if (_orbiting || _panningRMB) Pan2D(dx * PointerScale, dy * PointerScale);
                return;
            }

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
            if (Is2D) { Zoom2D(delta, _viewW * 0.5f, _viewH * 0.5f); return; }
            _distance = MathF.Max(0.01f, _distance - delta * _distance * 0.1f);
        }

        // ── 2D view ──────────────────────────────────────────────────────────────────────
        // A view point is in view units from the view's top-left corner, y down.

        /// <summary>2D view: the world point (of the XY plane) shown at a view point.</summary>
        public Vector2 ViewToWorld2D(float viewX, float viewY)
        {
            float upp = UnitsPerPixel;
            return new Vector2(Center2D.X + (viewX - _viewW * 0.5f) * upp, Center2D.Y - (viewY - _viewH * 0.5f) * upp);
        }

        /// <summary>2D view: the view point a world point is shown at.</summary>
        public Vector2 WorldToView2D(Vector2 world)
        {
            float upp = UnitsPerPixel;
            return new Vector2(_viewW * 0.5f + (world.X - Center2D.X) / upp, _viewH * 0.5f - (world.Y - Center2D.Y) / upp);
        }

        /// <summary>2D view: moves the picture by this many view units (what was under the pointer follows it).</summary>
        public void Pan2D(float dxView, float dyView)
        {
            float upp = UnitsPerPixel;
            Center2D = new Vector2(Center2D.X - dxView * upp, Center2D.Y + dyView * upp);
        }

        /// <summary>
        /// 2D view: zooms by <paramref name="notches"/> of the wheel (positive = closer, a tenth each) about a view
        /// point: the world point shown there is shown there afterwards.
        /// </summary>
        public void Zoom2D(float notches, float viewX, float viewY)
        {
            Vector2 under = ViewToWorld2D(viewX, viewY);
            OrthoSize = _orthoSize * MathF.Pow(0.9f, Math.Clamp(notches, -20f, 20f));
            Vector2 now = ViewToWorld2D(viewX, viewY);
            Center2D += under - now;
        }

        /// <summary>2D view: the centre and size that show the rectangle from <paramref name="min"/> to <paramref name="max"/> whole, with a margin.</summary>
        public (Vector2 Center, float OrthoSize) Fit2D(Vector2 min, Vector2 max)
        {
            float aspect = (float)_viewW / _viewH;
            float half = MathF.Max((max.Y - min.Y) * 0.5f, (max.X - min.X) * 0.5f / aspect);
            return ((min + max) * 0.5f, MathF.Max(half * 1.25f, 0.05f));
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
