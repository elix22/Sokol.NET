using System;
using System.Numerics;
using static Sokol.SApp;

namespace Sokol
{
    public struct CameraDesc
    {
        public float Aspect;
        public float NearZ;
        public float FarZ;
        public Vector3 Center;
        public float Distance;
        public float Latitude;
        public float Longitude;
    }

    public class Camera
    {
        public Matrix4x4 View { get; private set; }
        public Matrix4x4 Proj { get; private set; }
        public Vector3 EyePos { get; private set; }
        public Matrix4x4 ViewProj => View * Proj;

        private CameraDesc _desc;
        private float _latitude;
        private float _longitude;

        // Public properties for camera modification
        public Vector3 Center { get => _desc.Center; set => _desc.Center = value; }
        public float Distance { get => _desc.Distance; set => _desc.Distance = value; }
        public float Latitude { get => _latitude; set => _latitude = value; }
        public float Longitude { get => _longitude; set => _longitude = value; }
        public float Aspect => _desc.Aspect;

        public void Init(CameraDesc desc)
        {
            _desc = desc;
            _latitude = desc.Latitude;
            _longitude = desc.Longitude;
        }

        public void Update(int width, int height)
        {
            float aspect = (float)width / (float)height;
            Proj = Matrix4x4.CreatePerspectiveFieldOfView(
                _desc.Aspect * (float)Math.PI / 180.0f,
                aspect,
                _desc.NearZ,
                _desc.FarZ
            );

            // Calculate eye position from spherical coordinates
            float latRad = _latitude * (float)Math.PI / 180.0f;
            float lonRad = _longitude * (float)Math.PI / 180.0f;

            EyePos = new Vector3(
                _desc.Center.X + _desc.Distance * (float)Math.Cos(latRad) * (float)Math.Sin(lonRad),
                _desc.Center.Y + _desc.Distance * (float)Math.Sin(latRad),
                _desc.Center.Z + _desc.Distance * (float)Math.Cos(latRad) * (float)Math.Cos(lonRad)
            );

            View = Matrix4x4.CreateLookAt(EyePos, _desc.Center, Vector3.UnitY);
        }

        public void Orbit(float dx, float dy)
        {
            _longitude -= dx;
            if (_longitude < 0.0f)
                _longitude += 360.0f;
            if (_longitude > 360.0f)
                _longitude -= 360.0f;
            _latitude = Math.Clamp(_latitude + dy, -85.0f, 85.0f);
        }

        public void Zoom(float d)
        {
            _desc.Distance = Math.Clamp(_desc.Distance + d, 0.5f, 1000.0f);
        }

        public unsafe void HandleEvent(sapp_event* ev)
        {
            if (ev == null) return;

            switch (ev->type)
            {
                case sapp_event_type.SAPP_EVENTTYPE_MOUSE_DOWN:
                    if (ev->mouse_button == sapp_mousebutton.SAPP_MOUSEBUTTON_LEFT)
                    {
                        sapp_lock_mouse(true);
                    }
                    break;

                case sapp_event_type.SAPP_EVENTTYPE_MOUSE_UP:
                    if (ev->mouse_button == sapp_mousebutton.SAPP_MOUSEBUTTON_LEFT)
                    {
                        sapp_lock_mouse(false);
                    }
                    break;

                case sapp_event_type.SAPP_EVENTTYPE_MOUSE_SCROLL:
                    Zoom(ev->scroll_y * 0.5f);
                    break;

                case sapp_event_type.SAPP_EVENTTYPE_MOUSE_MOVE:
                    if (sapp_mouse_locked())
                    {
                        Orbit(ev->mouse_dx * 0.25f, ev->mouse_dy * 0.25f);
                    }
                    break;
            }
        }
    }
}
