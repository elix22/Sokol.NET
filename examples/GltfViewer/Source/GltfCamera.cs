using System.Numerics;

namespace Sokol;

/// <summary>
/// A free-look camera that maintains explicit position and rotation,
/// suitable for glTF camera definitions. Unlike the orbit camera,
/// this camera can move and rotate freely without being constrained
/// to orbit around a center point.
/// </summary>
public class GltfCamera
{
    // Camera properties
    public Vector3 Position { get; set; }
    public Quaternion Rotation { get; set; }
    
    public float Fov { get; set; } = 60.0f;
    public float NearZ { get; set; } = 0.01f;
    public float FarZ { get; set; } = 1000.0f;
    public float AspectRatio { get; set; } = 1.777f;
    
    // Movement speed
    public float MoveSpeed { get; set; } = 5.0f;
    public float RotateSpeed { get; set; } = 0.15f;
    
    // Derived vectors (cached)
    private Vector3 _forward;
    private Vector3 _right;
    private Vector3 _up;
    
    public Vector3 Forward => _forward;
    public Vector3 Right => _right;
    public Vector3 Up => _up;
    
    public GltfCamera()
    {
        Position = Vector3.Zero;
        Rotation = Quaternion.Identity;
        UpdateVectors();
    }
    
    public void Init(Vector3 position, Quaternion rotation, float fov, float nearZ, float farZ)
    {
        Position = position;
        Rotation = rotation;
        Fov = fov;
        NearZ = nearZ;
        FarZ = farZ;
        UpdateVectors();
    }
    
    /// <summary>
    /// Update cached direction vectors from rotation quaternion
    /// </summary>
    private void UpdateVectors()
    {
        // Transform basis vectors by rotation
        // glTF cameras look down -Z axis
        _forward = Vector3.Transform(-Vector3.UnitZ, Rotation);
        _right = Vector3.Transform(Vector3.UnitX, Rotation);
        _up = Vector3.Transform(Vector3.UnitY, Rotation);
    }
    
    /// <summary>
    /// Move camera forward/backward along its facing direction
    /// </summary>
    public void MoveForward(float delta)
    {
        Position += _forward * MoveSpeed * delta;
    }
    
    /// <summary>
    /// Move camera left/right perpendicular to facing direction
    /// </summary>
    public void MoveRight(float delta)
    {
        Position += _right * MoveSpeed * delta;
    }
    
    /// <summary>
    /// Move camera up/down along world Y axis
    /// </summary>
    public void MoveUp(float delta)
    {
        Position += Vector3.UnitY * MoveSpeed * delta;
    }
    
    /// <summary>
    /// Rotate camera by yaw (horizontal) and pitch (vertical) angles
    /// </summary>
    public void Rotate(float deltaYaw, float deltaPitch)
    {
        // Convert to radians
        deltaYaw *= RotateSpeed * MathF.PI / 180.0f;
        deltaPitch *= RotateSpeed * MathF.PI / 180.0f;
        
        // Create rotation quaternions
        Quaternion yawRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, deltaYaw);
        Quaternion pitchRotation = Quaternion.CreateFromAxisAngle(_right, deltaPitch);
        
        // Apply rotations
        Rotation = Quaternion.Normalize(yawRotation * Rotation * pitchRotation);
        
        // Update cached vectors
        UpdateVectors();
    }
    
    /// <summary>
    /// Get the view matrix for rendering
    /// </summary>
    public Matrix4x4 GetViewMatrix()
    {
        Vector3 target = Position + _forward;
        return Matrix4x4.CreateLookAt(Position, target, _up);
    }
    
    /// <summary>
    /// Get the projection matrix for rendering
    /// </summary>
    public Matrix4x4 GetProjectionMatrix()
    {
        return Matrix4x4.CreatePerspectiveFieldOfView(
            Fov * MathF.PI / 180.0f,  // Convert degrees to radians
            AspectRatio,
            NearZ,
            FarZ
        );
    }
}
