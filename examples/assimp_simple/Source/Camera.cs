using System;
using System.Numerics;
using static Sokol.SApp;

namespace Sokol
{
    public class CameraDesc
    {
        public float MinDist = 2.0f;
        public float MaxDist = 1000.0f;
        public float MinLat = -85.0f;
        public float MaxLat = 85.0f;
        public float Distance = 5.0f;
        public float Latitude = 0.0f;
        public float Longitude = 0.0f;
        // Here ‘Aspect’ is used as field-of-view in degrees
        public float Aspect = 60.0f;
        public float NearZ = 0.01f;
        public float FarZ = 100.0f;
        public Vector3 Center = Vector3.Zero;
    }

    public class Camera
    {
        public float MinDist;
        public float MaxDist;
        public float MinLat;
        public float MaxLat;
        public float Distance;
        public float Latitude;
        public float Longitude;
        public float Aspect;
        public float NearZ;
        public float FarZ;
        public Vector3 Center;

        public Vector3 EyePos;
        public Matrix4x4 View;
        public Matrix4x4 Proj;
        public Matrix4x4 ViewProj;

        static float CamDef(float val, float def)
        {
            return (val == 0.0f) ? def : val;
        }

        public void Init(CameraDesc desc)
        {
            if (desc == null) throw new ArgumentNullException(nameof(desc));
            MinDist = CamDef(desc.MinDist, 2.0f);
            MaxDist = CamDef(desc.MaxDist, 1000.0f);
            MinLat = CamDef(desc.MinLat, -85.0f);
            MaxLat = CamDef(desc.MaxLat, 85.0f);
            Distance = CamDef(desc.Distance, 5.0f);
            Center = desc.Center;
            Latitude = desc.Latitude;
            Longitude = desc.Longitude;
            Aspect = CamDef(desc.Aspect, 60.0f);
            NearZ = CamDef(desc.NearZ, 0.01f);
            FarZ = CamDef(desc.FarZ, 100.0f);
        }

        public void Orbit(float dx, float dy)
        {
            Longitude -= dx;
            if (Longitude < 0.0f)
            {
                Longitude += 360.0f;
            }
            if (Longitude > 360.0f)
            {
                Longitude -= 360.0f;
            }
            Latitude = Clamp(Latitude + dy, MinLat, MaxLat);
        }

        public void Zoom(float d)
        {
            Distance = Clamp(Distance + d, MinDist, MaxDist);
        }

        static Vector3 Euclidean(float latitude, float longitude)
        {
            float lat = DegreesToRadians(latitude);
            float lng = DegreesToRadians(longitude);
            return new Vector3(
                (float)(Math.Cos(lat) * Math.Sin(lng)),
                (float)Math.Sin(lat),
                (float)(Math.Cos(lat) * Math.Cos(lng))
            );
        }

        public void Update(int fbWidth, int fbHeight)
        {
            if (fbWidth <= 0 || fbHeight <= 0)
                throw new ArgumentException("Invalid framebuffer dimensions");

            float w = fbWidth;
            float h = fbHeight;

            // Calculate the new eye position from spherical coordinates.
            Vector3 offset = Euclidean(Latitude, Longitude) * Distance;
            EyePos = Center + offset;

            // Create view (look-at) matrix.
            View = Matrix4x4.CreateLookAt(EyePos, Center, Vector3.UnitY);

            // Create perspective projection matrix.
            // Aspect is interpreted here as FOV (in degrees), so convert it to radians.
            Proj = Matrix4x4.CreatePerspectiveFieldOfView(DegreesToRadians(Aspect), w / h, NearZ, FarZ);

            // Combine the view and projection matrices.
            // Note: In the original code the multiplication order is proj * view.
            ViewProj = View * Proj ;
        }
            
        float last_touch_x = 0;
        float last_touch_y = 0;
        public unsafe void HandleEvent(sapp_event*  ev)
        {
            if (ev == null)
                throw new ArgumentNullException(nameof(ev));

            switch (ev->type)
            {
                case sapp_event_type.SAPP_EVENTTYPE_KEY_DOWN:
                    if (ev->key_code == sapp_keycode.SAPP_KEYCODE_UP)
                    {
                        Center = Center + Vector3.UnitY * 0.1f;
                    }
                    else if (ev->key_code == sapp_keycode.SAPP_KEYCODE_DOWN)
                    {
                        Center = Center - Vector3.UnitY * 0.1f;
                    }
                    else if (ev->key_code == sapp_keycode.SAPP_KEYCODE_LEFT)
                    {
                        Center = Center - Vector3.UnitX * 0.1f;
                    }
                    else if (ev->key_code == sapp_keycode.SAPP_KEYCODE_RIGHT)
                    {
                        Center = Center + Vector3.UnitX * 0.1f;
                    }
                    
                    break;
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

                case sapp_event_type.SAPP_EVENTTYPE_TOUCHES_BEGAN:
                    {
                        last_touch_x = ev->touches[0].pos_x;
                        last_touch_y = ev->touches[0].pos_y;
                    }
                    break;

                case sapp_event_type.SAPP_EVENTTYPE_TOUCHES_ENDED:
                    {
                        last_touch_x = 0;
                        last_touch_y = 0;
                    }
                    break;

                case sapp_event_type.SAPP_EVENTTYPE_TOUCHES_MOVED:
                    {

                        float dx = ev->touches[0].pos_x - last_touch_x;
                        float dy = ev->touches[0].pos_y - last_touch_y;
                        Orbit(dx * 0.25f, dy * 0.25f);

                        last_touch_x =ev->touches[0].pos_x;
                        last_touch_y = ev->touches[0].pos_y;
                    }

                    break;
                default:
                    break;
            }
        }

        static float DegreesToRadians(float degrees)
        {
            return degrees * ((float)Math.PI / 180f);
        }

        static float Clamp(float value, float min, float max)
        {
            if (value < min)
                return min;
            if (value > max)
                return max;
            return value;
        }
    }

   
}
