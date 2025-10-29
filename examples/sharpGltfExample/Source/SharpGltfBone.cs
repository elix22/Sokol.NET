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
        public void SetSamplers(IAnimationSampler<Vector3>? translationSampler,
                                IAnimationSampler<Quaternion>? rotationSampler,
                                IAnimationSampler<Vector3>? scaleSampler)
        {
            if (translationSampler != null)
                _translationSampler = translationSampler.CreateCurveSampler();
            if (rotationSampler != null)
                _rotationSampler = rotationSampler.CreateCurveSampler();
            if (scaleSampler != null)
                _scaleSampler = scaleSampler.CreateCurveSampler();
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
        }

        public Matrix4x4 GetLocalTransform() => LocalTransform;
    }
}
