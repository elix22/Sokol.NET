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
        private bool _mouseMovementEnabled = false;  // Toggle for mouse movement mode
        private float _mouseSensitivity = 0.15f;     // Mouse sensitivity for look around
        
        // Single coordinate system: yaw/pitch for rotation
        private float _yaw = 0.0f;      // Rotation around Y axis (left/right)
        private float _pitch = 0.0f;    // Rotation around X axis (up/down)
        private bool _useLocalRotation = false;  // Track if using local axis rotation (first-person vs orbit)
        
        // Store state when switching modes to restore smoothly
        private Vector3 _savedCenter;
        private float _savedDistance;
        
        // Keyboard state for WASD movement
        private bool _keyW = false;
        private bool _keyA = false;
        private bool _keyS = false;
        private bool _keyD = false;
        private bool _keyQ = false;  // Up
        private bool _keyE = false;  // Down
        private bool _keyUp = false;    // Arrow up
        private bool _keyDown = false;  // Arrow down
        private bool _keyShift = false;  // Speed boost
        
        // Touch state for mobile
        private float _lastTouch1X = 0;  // 1-finger: camera orbit
        private float _lastTouch1Y = 0;
        private bool _touch1IsActive = false;
        private float _lastTouch2X = 0;  // 2-finger: model rotation
        private float _lastTouch2Y = 0;
        private bool _touch2IsActive = false;
        
        public float MoveSpeed { get; set; } = 1.0f;  // Units per second

        // Public properties for camera modification
        public Vector3 Center { get => _desc.Center; set => _desc.Center = value; }
        public float Distance { get => _desc.Distance; set => _desc.Distance = value; }
        public float Aspect => _desc.Aspect;
        public bool MouseMovementEnabled { get => _mouseMovementEnabled; set => _mouseMovementEnabled = value; }
        public float MouseSensitivity { get => _mouseSensitivity; set => _mouseSensitivity = value; }
        
        // Properties for compatibility (convert to/from yaw/pitch)
        public float Latitude 
        { 
            get => _pitch * 180.0f / (float)Math.PI; 
            set => _pitch = value * (float)Math.PI / 180.0f; 
        }
        public float Longitude 
        { 
            get => -_yaw * 180.0f / (float)Math.PI; 
            set => _yaw = -value * (float)Math.PI / 180.0f; 
        }

        public void Init(CameraDesc desc)
        {
            _desc = desc;
            
            // Initialize yaw and pitch from latitude/longitude in desc
            _yaw = -desc.Longitude * (float)Math.PI / 180.0f;
            _pitch = desc.Latitude * (float)Math.PI / 180.0f;
        }

        static int counter = 0;
        public void Update(int width, int height, float deltaTime = 0.0f)
        {
            float aspect = (float)width / (float)height;
            Proj = Matrix4x4.CreatePerspectiveFieldOfView(
                _desc.Aspect * (float)Math.PI / 180.0f,
                aspect,
                _desc.NearZ,
                _desc.FarZ
            );

            if (_useLocalRotation)
            {
                // First-person camera: rotate around local axis
                // Calculate forward direction from yaw and pitch
                Vector3 forward = new Vector3(
                    (float)(Math.Cos(_pitch) * Math.Sin(_yaw)),
                    (float)Math.Sin(_pitch),
                    (float)(Math.Cos(_pitch) * Math.Cos(_yaw))
                );
                forward = Vector3.Normalize(forward);

                // Calculate right and up vectors
                Vector3 right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));
                Vector3 up = Vector3.Cross(right, forward);

                // Handle WASD camera movement
                if (deltaTime > 0.0f)
                {
                    float speedMultiplier = _keyShift ? 2.0f : 1.0f;
                    float moveAmount = MoveSpeed * deltaTime * speedMultiplier;
                    Vector3 moveDir = Vector3.Zero;

                    // WASD for forward/back/left/right movement
                    if (_keyW) moveDir += forward;
                    if (_keyS) moveDir -= forward;
                    if (_keyD) moveDir += right;
                    if (_keyA) moveDir -= right;

                    // Q/E for up/down movement
                    if (_keyQ) moveDir += Vector3.UnitY;
                    if (_keyE) moveDir -= Vector3.UnitY;

                    // Arrow keys for up/down movement (alternative to Q/E)
                    if (_keyUp) moveDir += Vector3.UnitY;
                    if (_keyDown) moveDir -= Vector3.UnitY;

                    // Normalize and apply movement
                    if (moveDir.LengthSquared() > 0)
                    {
                        moveDir = Vector3.Normalize(moveDir);
                        Vector3 movement = moveDir * moveAmount;
                        _desc.Center += movement;
                    }
                }

                // Eye position is at the center (first-person)
                EyePos = _desc.Center;

                // Look at position is center + forward direction
                Vector3 lookAt = _desc.Center + forward;
                View = Matrix4x4.CreateLookAt(EyePos, lookAt, up);
            }
            else
            {
                // Orbit camera: rotate around center point
                // Handle WASD camera movement
                if (deltaTime > 0.0f)
                {
                    float speedMultiplier = _keyShift ? 2.0f : 1.0f;
                    float moveAmount = MoveSpeed * deltaTime * speedMultiplier;

                    // Get camera forward, right, and up vectors
                    Vector3 forward = Vector3.Normalize(_desc.Center - EyePos);
                    Vector3 right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));
                    Vector3 up = Vector3.UnitY;

                    Vector3 moveDir = Vector3.Zero;

                    // WASD for forward/back/left/right movement
                    if (_keyW) moveDir += forward;
                    if (_keyS) moveDir -= forward;
                    if (_keyD) moveDir += right;
                    if (_keyA) moveDir -= right;

                    // Q/E for up/down movement
                    if (_keyQ) moveDir += up;
                    if (_keyE) moveDir -= up;

                    // Arrow keys for up/down movement (alternative to Q/E)
                    if (_keyUp) moveDir += up;
                    if (_keyDown) moveDir -= up;

                    // Normalize and apply movement
                    if (moveDir.LengthSquared() > 0)
                    {
                        moveDir = Vector3.Normalize(moveDir);
                        Vector3 movement = moveDir * moveAmount;

                        // Move both camera position and look-at center to maintain view direction
                        _desc.Center += movement;
                    }
                }

                // Calculate camera position from yaw/pitch in orbit mode
                // We position the camera at Distance away from Center, looking at it
                Vector3 offset = new Vector3(
                    (float)(Math.Cos(_pitch) * Math.Sin(_yaw)),
                    (float)Math.Sin(_pitch),
                    (float)(Math.Cos(_pitch) * Math.Cos(_yaw))
                );
                offset = Vector3.Normalize(offset);

                // Eye position orbits around center at Distance
                EyePos = _desc.Center + offset * _desc.Distance;

                View = Matrix4x4.CreateLookAt(EyePos, _desc.Center, Vector3.UnitY);
            }
        }

        public void Orbit(float dx, float dy)
        {
            // Same as RotateWorld - use yaw/pitch directly
            _yaw += dx * (float)Math.PI / 180.0f;
            _pitch = Math.Clamp(_pitch + dy * (float)Math.PI / 180.0f, -1.5f, 1.5f);  // Clamp pitch to ~±85 degrees
        }
        
        public void RotateWorld(float dx, float dy)
        {
            // Rotate around world axis (first-person style)
            // Yaw rotates around world Y-axis
            _yaw -= dx * (float)Math.PI / 180.0f;
            // Pitch rotates around world X-axis (clamped to prevent flipping)
            _pitch = Math.Clamp(_pitch + dy * (float)Math.PI / 180.0f, -1.5f, 1.5f);  // Clamp pitch to ~±85 degrees
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
                        // Left click: Enable first-person rotation around world axis
                        // Save orbit state
                        _savedCenter = _desc.Center;
                        _savedDistance = _desc.Distance;
                        
                        // Calculate forward direction for first-person mode
                        Vector3 forward = Vector3.Normalize(_desc.Center - EyePos);
                        _yaw = (float)Math.Atan2(forward.X, forward.Z);
                        _pitch = (float)Math.Asin(forward.Y);
                        
                        // Move center to current eye position for first-person mode
                        _desc.Center = EyePos;
                        _useLocalRotation = true;
                        sapp_lock_mouse(true);
                    }
                    break;

                case sapp_event_type.SAPP_EVENTTYPE_MOUSE_UP:
                    if (ev->mouse_button == sapp_mousebutton.SAPP_MOUSEBUTTON_LEFT)
                    {
                        // When returning to orbit mode from first-person:
                        // Keep the camera looking in the same direction
                        // Calculate where the Center should be based on current forward direction
                        
                        // Current yaw/pitch represents the FORWARD direction (first-person)
                        Vector3 forward = new Vector3(
                            (float)(Math.Cos(_pitch) * Math.Sin(_yaw)),
                            (float)Math.Sin(_pitch),
                            (float)(Math.Cos(_pitch) * Math.Cos(_yaw))
                        );
                        forward = Vector3.Normalize(forward);
                        
                        // In orbit mode, we want to look AT the center FROM the current position
                        // So: Center = EyePos + forward * Distance
                        _desc.Center = EyePos + forward * _savedDistance;
                        _desc.Distance = _savedDistance;
                        
                        // Now yaw/pitch needs to represent the offset direction (opposite of forward)
                        // offset = -forward, so we need to convert forward to offset angles
                        Vector3 offset = -forward;
                        _yaw = (float)Math.Atan2(offset.X, offset.Z);
                        _pitch = (float)Math.Asin(offset.Y);
                        
                        sapp_lock_mouse(false);
                        _useLocalRotation = _mouseMovementEnabled;
                    }
                    break;

                case sapp_event_type.SAPP_EVENTTYPE_MOUSE_SCROLL:
                    Zoom(ev->scroll_y * 0.5f);
                    break;

                case sapp_event_type.SAPP_EVENTTYPE_MOUSE_MOVE:
                    if (sapp_mouse_locked())
                    {
                        if (_useLocalRotation)
                        {
                            // Rotate around world axis (first-person style)
                            RotateWorld(ev->mouse_dx * 0.25f, -ev->mouse_dy * 0.25f);
                        }
                        else
                        {
                            // Orbit around center point
                            Orbit(ev->mouse_dx * 0.25f, ev->mouse_dy * 0.25f);
                        }
                    }
                    else if (_mouseMovementEnabled)
                    {
                        // Free mouse movement mode
                        if (_useLocalRotation)
                        {
                            RotateWorld(ev->mouse_dx * _mouseSensitivity, ev->mouse_dy * _mouseSensitivity);
                        }
                        else
                        {
                            Orbit(ev->mouse_dx * _mouseSensitivity, ev->mouse_dy * _mouseSensitivity);
                        }
                    }
                    break;
                    
                case sapp_event_type.SAPP_EVENTTYPE_KEY_DOWN:
                case sapp_event_type.SAPP_EVENTTYPE_KEY_UP:
                    HandleKeyboardInput(ev);
                    break;
                    
                case sapp_event_type.SAPP_EVENTTYPE_TOUCHES_BEGAN:
                    if (ev->num_touches == 1)
                    {
                        // 1 finger: camera orbit
                        _lastTouch1X = ev->touches[0].pos_x;
                        _lastTouch1Y = ev->touches[0].pos_y;
                        _touch1IsActive = true;
                        _touch2IsActive = false;
                    }
                    else if (ev->num_touches >= 2)
                    {
                        // 2 fingers: model rotation
                        _lastTouch2X = ev->touches[0].pos_x;
                        _lastTouch2Y = ev->touches[0].pos_y;
                        _touch1IsActive = false;
                        _touch2IsActive = true;
                    }
                    break;

                case sapp_event_type.SAPP_EVENTTYPE_TOUCHES_ENDED:
                    _touch1IsActive = false;
                    _touch2IsActive = false;
                    break;

                case sapp_event_type.SAPP_EVENTTYPE_TOUCHES_MOVED:
                    if (ev->num_touches == 1 && _touch1IsActive)
                    {
                        // 1-finger: camera orbit
                        float currentX = ev->touches[0].pos_x;
                        float currentY = ev->touches[0].pos_y;
                        
                        float dx = currentX - _lastTouch1X;
                        float dy = currentY - _lastTouch1Y;
                        
                        // Discontinuity detection
                        float deltaMagnitude = (float)Math.Sqrt(dx * dx + dy * dy);
                        if (deltaMagnitude > 50.0f)
                        {
                            _lastTouch1X = currentX;
                            _lastTouch1Y = currentY;
                            break;
                        }

                        // Orbit camera
                        Orbit(dx * 0.25f, dy * 0.25f);

                        _lastTouch1X = currentX;
                        _lastTouch1Y = currentY;
                    }
                    else if (ev->num_touches >= 2 && _touch2IsActive)
                    {
                        // 2-finger: model rotation - handled in Event.cs
                        // Don't update position here - Event.cs will call GetTwoFingerTouchDelta
                    }
                    break;
            }
        }
        
        private unsafe void HandleKeyboardInput(sapp_event* ev)
        {
            bool isDown = ev->type == sapp_event_type.SAPP_EVENTTYPE_KEY_DOWN;
            
            switch (ev->key_code)
            {
                case sapp_keycode.SAPP_KEYCODE_W:
                    _keyW = isDown;
                    break;
                case sapp_keycode.SAPP_KEYCODE_A:
                    _keyA = isDown;
                    break;
                case sapp_keycode.SAPP_KEYCODE_S:
                    _keyS = isDown;
                    break;
                case sapp_keycode.SAPP_KEYCODE_D:
                    _keyD = isDown;
                    break;
                case sapp_keycode.SAPP_KEYCODE_Q:
                    _keyQ = isDown;
                    break;
                case sapp_keycode.SAPP_KEYCODE_E:
                    _keyE = isDown;
                    break;
                case sapp_keycode.SAPP_KEYCODE_UP:
                    _keyUp = isDown;
                    break;
                case sapp_keycode.SAPP_KEYCODE_DOWN:
                    _keyDown = isDown;
                    break;
                case sapp_keycode.SAPP_KEYCODE_LEFT_SHIFT:
                case sapp_keycode.SAPP_KEYCODE_RIGHT_SHIFT:
                    _keyShift = isDown;
                    break;
            }
        }

        // Check if 2-finger touch is active (for external model rotation handling)
        public bool IsTwoFingerTouchActive() => _touch2IsActive;
        
        // Get 2-finger touch delta and update position (for external model rotation handling)
        public (float dx, float dy) GetTwoFingerTouchDelta(float currentX, float currentY)
        {
            float dx = currentX - _lastTouch2X;
            float dy = currentY - _lastTouch2Y;
            
            // Check for discontinuity
            float deltaMagnitude = (float)Math.Sqrt(dx * dx + dy * dy);
            if (deltaMagnitude > 50.0f)
            {
                _lastTouch2X = currentX;
                _lastTouch2Y = currentY;
                return (0, 0);  // No rotation on discontinuity
            }
            
            _lastTouch2X = currentX;
            _lastTouch2Y = currentY;
            return (dx, dy);
        }
    }
}
