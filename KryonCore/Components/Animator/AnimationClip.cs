using OpenTK.Mathematics;
using System;
using System.Collections.Generic;

namespace KrayonCore.Animation
{
    public struct KeyPosition
    {
        public Vector3 Position;
        public float TimeStamp;
    }

    public struct KeyRotation
    {
        public Quaternion Rotation;
        public float TimeStamp;
    }

    public struct KeyScale
    {
        public Vector3 Scale;
        public float TimeStamp;
    }

    public class BoneAnimation
    {
        public string BoneName { get; set; }
        public int BoneId { get; set; } = -1;

        public List<KeyPosition> Positions { get; set; } = new();
        public List<KeyRotation> Rotations { get; set; } = new();
        public List<KeyScale> Scales { get; set; } = new();

        public Vector3 InterpolatePosition(float animationTime)
        {
            if (Positions.Count == 0) return Vector3.Zero;
            if (Positions.Count == 1) return Positions[0].Position;

            // Si el tiempo supera el último keyframe, devolver el último valor
            if (animationTime >= Positions[Positions.Count - 1].TimeStamp)
                return Positions[Positions.Count - 1].Position;

            int index = GetPositionIndex(animationTime);
            int nextIndex = index + 1;

            float deltaTime = Positions[nextIndex].TimeStamp - Positions[index].TimeStamp;
            if (deltaTime <= 0f) return Positions[index].Position;
            float factor = Math.Clamp((animationTime - Positions[index].TimeStamp) / deltaTime, 0f, 1f);

            return Vector3.Lerp(Positions[index].Position, Positions[nextIndex].Position, factor);
        }

        public Quaternion InterpolateRotation(float animationTime)
        {
            if (Rotations.Count == 0) return Quaternion.Identity;
            if (Rotations.Count == 1) return Rotations[0].Rotation;

            if (animationTime >= Rotations[Rotations.Count - 1].TimeStamp)
                return Rotations[Rotations.Count - 1].Rotation;

            int index = GetRotationIndex(animationTime);
            int nextIndex = index + 1;

            float deltaTime = Rotations[nextIndex].TimeStamp - Rotations[index].TimeStamp;
            if (deltaTime <= 0f) return Rotations[index].Rotation;
            float factor = Math.Clamp((animationTime - Rotations[index].TimeStamp) / deltaTime, 0f, 1f);

            return Quaternion.Slerp(Rotations[index].Rotation, Rotations[nextIndex].Rotation, factor);
        }

        public Vector3 InterpolateScale(float animationTime)
        {
            if (Scales.Count == 0) return Vector3.One;
            if (Scales.Count == 1) return Scales[0].Scale;

            if (animationTime >= Scales[Scales.Count - 1].TimeStamp)
                return Scales[Scales.Count - 1].Scale;

            int index = GetScaleIndex(animationTime);
            int nextIndex = index + 1;

            float deltaTime = Scales[nextIndex].TimeStamp - Scales[index].TimeStamp;
            if (deltaTime <= 0f) return Scales[index].Scale;
            float factor = Math.Clamp((animationTime - Scales[index].TimeStamp) / deltaTime, 0f, 1f);

            return Vector3.Lerp(Scales[index].Scale, Scales[nextIndex].Scale, factor);
        }

        private int GetPositionIndex(float animationTime)
        {
            for (int i = 0; i < Positions.Count - 1; i++)
            {
                if (animationTime < Positions[i + 1].TimeStamp)
                    return i;
            }
            return Math.Max(0, Positions.Count - 2);
        }

        private int GetRotationIndex(float animationTime)
        {
            for (int i = 0; i < Rotations.Count - 1; i++)
            {
                if (animationTime < Rotations[i + 1].TimeStamp)
                    return i;
            }
            return Math.Max(0, Rotations.Count - 2);
        }

        private int GetScaleIndex(float animationTime)
        {
            for (int i = 0; i < Scales.Count - 1; i++)
            {
                if (animationTime < Scales[i + 1].TimeStamp)
                    return i;
            }
            return Math.Max(0, Scales.Count - 2);
        }
    }

    public class AnimationClip
    {
        public string Name { get; set; }
        public float Duration { get; set; }
        public float TicksPerSecond { get; set; }
        public List<BoneAnimation> BoneAnimations { get; set; } = new();
        public NodeData RootNode { get; set; }

        private Dictionary<string, BoneAnimation> _boneAnimationMap;

        public BoneAnimation FindBoneAnimation(string boneName)
        {
            if (_boneAnimationMap == null)
            {
                _boneAnimationMap = new Dictionary<string, BoneAnimation>();
                foreach (var ba in BoneAnimations)
                    _boneAnimationMap[ba.BoneName] = ba;
            }

            _boneAnimationMap.TryGetValue(boneName, out var result);
            return result;
        }
    }
}