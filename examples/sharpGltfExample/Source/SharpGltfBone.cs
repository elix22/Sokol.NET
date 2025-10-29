using System;
using System.Collections.Generic;
using System.Numerics;
using SharpGLTF.Schema2;
using SharpGLTF.Transforms;
using SharpGLTF.Animations;

namespace Sokol
{
    public class SharpGltfBone
    {
        // Store curve samplers instead of pre-sampled keyframes (like assimp_animation)
        private ICurveSampler<Vector3>? _translationCurveSampler;
        private ICurveSampler<Quaternion>? _rotationCurveSampler;
        private ICurveSampler<Vector3>? _scaleCurveSampler;

        public Matrix4x4 LocalTransform { get; private set; }
        public string Name { get; private set; }
        public int ID { get; private set; }

        public SharpGltfBone(string name, int id, Node node)
        {
            Name = name;
            ID = id;
            LocalTransform = Matrix4x4.Identity;
        }

        public void SetTranslationSampler(IAnimationSampler<Vector3> sampler)
        {
            _translationCurveSampler = sampler.CreateCurveSampler();
        }

        public void SetRotationSampler(IAnimationSampler<Quaternion> sampler)
        {
            _rotationCurveSampler = sampler.CreateCurveSampler();
        }

        public void SetScaleSampler(IAnimationSampler<Vector3> sampler)
        {
            _scaleCurveSampler = sampler.CreateCurveSampler();
        }

        // Update bone transformation at runtime by sampling the curves (like assimp_animation)
        public void Update(float animationTime)
        {
            // Sample from curve samplers at runtime instead of using pre-sampled keyframes
            Vector3 translation = _translationCurveSampler?.GetPoint(animationTime) ?? Vector3.Zero;
            Quaternion rotation = _rotationCurveSampler?.GetPoint(animationTime) ?? Quaternion.Identity;
            Vector3 scale = _scaleCurveSampler?.GetPoint(animationTime) ?? Vector3.One;

            // Build the local transform matrix
            Matrix4x4 translationMatrix = Matrix4x4.CreateTranslation(translation);
            Matrix4x4 rotationMatrix = Matrix4x4.CreateFromQuaternion(Quaternion.Normalize(rotation));
            Matrix4x4 scaleMatrix = Matrix4x4.CreateScale(scale);

            LocalTransform = scaleMatrix * rotationMatrix * translationMatrix;
        }

        public Matrix4x4 GetLocalTransform() => LocalTransform;
    }
}
