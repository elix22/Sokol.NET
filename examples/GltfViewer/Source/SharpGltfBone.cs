using System;
using System.Numerics;
using SharpGLTF.Schema2;
using SharpGLTF.Transforms;
using SharpGLTF.Animations;

namespace Sokol
{
    public class SharpGltfBone
    {
        // Store curve samplers - NO pre-sampling, evaluate at runtime
        private ICurveSampler<Vector3>? _translationSampler;
        private ICurveSampler<Quaternion>? _rotationSampler;
        private ICurveSampler<Vector3>? _scaleSampler;

        public Matrix4x4 LocalTransform { get; private set; }
        public string Name { get; private set; }
        public int ID { get; private set; }

        public SharpGltfBone(string name, int id, Node node)
        {
            Name = name;
            ID = id;
            LocalTransform = Matrix4x4.Identity;
        }

        // Store samplers - called once during load (fast)
        // FastCurveSampler uses array indexing instead of LINQ - much faster on WebAssembly!
        public void SetSamplers(IAnimationSampler<Vector3>? translationSampler,
                                IAnimationSampler<Quaternion>? rotationSampler,
                                IAnimationSampler<Vector3>? scaleSampler)
        {
            if (translationSampler != null)
            {
                var sampler = translationSampler.CreateCurveSampler();
                // LinearSampler has ToFastSampler() method that optimizes LINQ away
                if (sampler is LinearSampler<Vector3> linear)
                    _translationSampler = linear.ToFastSampler() ?? sampler;
                else
                    _translationSampler = sampler;
            }
            
            if (rotationSampler != null)
            {
                var sampler = rotationSampler.CreateCurveSampler();
                if (sampler is LinearSampler<Quaternion> linear)
                    _rotationSampler = linear.ToFastSampler() ?? sampler;
                else
                    _rotationSampler = sampler;
            }
            
            if (scaleSampler != null)
            {
                var sampler = scaleSampler.CreateCurveSampler();
                if (sampler is LinearSampler<Vector3> linear)
                    _scaleSampler = linear.ToFastSampler() ?? sampler;
                else
                    _scaleSampler = sampler;
            }
        }

        // Runtime update - sample curves directly
        public void Update(float animationTime)
        {
            Vector3 translation = _translationSampler?.GetPoint(animationTime) ?? Vector3.Zero;
            Quaternion rotation = _rotationSampler?.GetPoint(animationTime) ?? Quaternion.Identity;
            Vector3 scale = _scaleSampler?.GetPoint(animationTime) ?? Vector3.One;

            Matrix4x4 translationMatrix = Matrix4x4.CreateTranslation(translation);
            Matrix4x4 rotationMatrix = Matrix4x4.CreateFromQuaternion(Quaternion.Normalize(rotation));
            Matrix4x4 scaleMatrix = Matrix4x4.CreateScale(scale);

            LocalTransform = scaleMatrix * rotationMatrix * translationMatrix;
            
            // Store TRS for later retrieval
            _lastTranslation = translation;
            _lastRotation = rotation;
            _lastScale = scale;
        }

        private Vector3 _lastTranslation = Vector3.Zero;
        private Quaternion _lastRotation = Quaternion.Identity;
        private Vector3 _lastScale = Vector3.One;

        public Matrix4x4 GetLocalTransform() => LocalTransform;
        
        public void GetTRS(out Vector3 translation, out Quaternion rotation, out Vector3 scale)
        {
            translation = _lastTranslation;
            rotation = _lastRotation;
            scale = _lastScale;
        }
        
        /// <summary>
        /// Get animated channels and their values, indicating which channels have actual animation data.
        /// This is important for node animations where non-animated channels should preserve original values.
        /// </summary>
        public void GetAnimatedChannels(out bool hasTranslation, out bool hasRotation, out bool hasScale,
                                        out Vector3 translation, out Quaternion rotation, out Vector3 scale)
        {
            hasTranslation = _translationSampler != null;
            hasRotation = _rotationSampler != null;
            hasScale = _scaleSampler != null;
            
            translation = _lastTranslation;
            rotation = _lastRotation;
            scale = _lastScale;
        }
        
        /// <summary>
        /// Creates a deep clone of this bone with a new ID.
        /// The clone has independent state but shares the same samplers (samplers are read-only).
        /// This allows multiple characters to animate the same bones independently.
        /// </summary>
        public SharpGltfBone Clone(int newId)
        {
            var clone = new SharpGltfBone(Name, newId, null!);
            
            // Share samplers (they're read-only, so safe to share)
            clone._translationSampler = _translationSampler;
            clone._rotationSampler = _rotationSampler;
            clone._scaleSampler = _scaleSampler;
            
            // Clone has independent transform state
            clone.LocalTransform = LocalTransform;
            clone._lastTranslation = _lastTranslation;
            clone._lastRotation = _lastRotation;
            clone._lastScale = _lastScale;
            
            return clone;
        }
    }
}