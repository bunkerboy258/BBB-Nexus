using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using UnityEngine;

namespace BBBNexus
{
    public enum AttackGeometryShapeType
    {
        Sphere = 0,
        Capsule = 1,
        Box = 2,
    }

    [Serializable]
    public struct AttackGeometryShapeDefinition
    {
        public AttackGeometryShapeType ShapeType;
        public SerializableVector3 LocalPosition;
        public SerializableVector3 LocalEulerAngles;
        public float Radius;
        public float Height;
        public SerializableVector3 HalfExtents;

        [JsonIgnore]
        public Quaternion LocalRotation => Quaternion.Euler((Vector3)LocalEulerAngles);
    }

    [Serializable]
    public sealed class AttackGeometrySampleDefinition
    {
        [Range(0f, 1f)]
        public float SweepProgressNormalized;

        [Obsolete("Use SweepProgressNormalized instead. Kept only for old JSON migration.")]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public float TimeNormalized;

        // 与 SweepProgressNormalized 对齐的角色根运动采样。
        // Position 表示该采样点的根节点局部累计位置，RotationY 表示累计朝向（仅 Y 轴）。
        public SerializableVector3 RootLocalPosition;
        public float RootLocalRotationY;

        public List<AttackGeometryShapeDefinition> Shapes = new List<AttackGeometryShapeDefinition>();

        [OnDeserialized]
        internal void OnDeserialized(StreamingContext _)
        {
            if (Mathf.Approximately(SweepProgressNormalized, 0f) && !Mathf.Approximately(TimeNormalized, 0f))
            {
                SweepProgressNormalized = TimeNormalized;
            }
        }

        [JsonIgnore]
        public Vector2 RootLocalPositionXZ => new Vector2(RootLocalPosition.x, RootLocalPosition.z);
    }

    [Serializable]
    public sealed class AttackRootMotionSampleDefinition
    {
        [Range(0f, 1f)]
        public float ClipProgressNormalized;
        public SerializableVector3 RootLocalPosition;
        public float RootLocalRotationY;
    }

    [Serializable]
    public sealed class AttackClipGeometryClipDefinition
    {
        public int ComboIndex;
        public string DisplayName;
        public bool UseRootTransformForSweep = true;
        public List<AttackGeometrySampleDefinition> Samples = new List<AttackGeometrySampleDefinition>();
        public List<AttackRootMotionSampleDefinition> RootMotionSamples = new List<AttackRootMotionSampleDefinition>();

        public bool TrySampleRootMotion(float sweepProgressNormalized, out Vector3 rootLocalPosition, out float rootLocalRotationY)
        {
            rootLocalPosition = Vector3.zero;
            rootLocalRotationY = 0f;

            if (Samples == null || Samples.Count == 0)
            {
                return false;
            }

            if (Samples.Count == 1)
            {
                rootLocalPosition = Samples[0].RootLocalPosition;
                rootLocalRotationY = Samples[0].RootLocalRotationY;
                return true;
            }

            float t = Mathf.Clamp01(sweepProgressNormalized);
            AttackGeometrySampleDefinition previous = Samples[0];

            if (t <= previous.SweepProgressNormalized)
            {
                rootLocalPosition = previous.RootLocalPosition;
                rootLocalRotationY = previous.RootLocalRotationY;
                return true;
            }

            for (int i = 1; i < Samples.Count; i++)
            {
                AttackGeometrySampleDefinition next = Samples[i];
                if (t > next.SweepProgressNormalized)
                {
                    previous = next;
                    continue;
                }

                float range = next.SweepProgressNormalized - previous.SweepProgressNormalized;
                float lerpT = range > 0.0001f
                    ? Mathf.Clamp01((t - previous.SweepProgressNormalized) / range)
                    : 0f;

                rootLocalPosition = Vector3.LerpUnclamped(previous.RootLocalPosition, next.RootLocalPosition, lerpT);
                rootLocalRotationY = Mathf.LerpAngle(previous.RootLocalRotationY, next.RootLocalRotationY, lerpT);
                return true;
            }

            AttackGeometrySampleDefinition last = Samples[Samples.Count - 1];
            rootLocalPosition = last.RootLocalPosition;
            rootLocalRotationY = last.RootLocalRotationY;
            return true;
        }

        public bool TrySampleClipRootMotion(float clipProgressNormalized, out Vector3 rootLocalPosition, out float rootLocalRotationY)
        {
            rootLocalPosition = Vector3.zero;
            rootLocalRotationY = 0f;

            if (RootMotionSamples == null || RootMotionSamples.Count == 0)
            {
                return false;
            }

            if (RootMotionSamples.Count == 1)
            {
                rootLocalPosition = RootMotionSamples[0].RootLocalPosition;
                rootLocalRotationY = RootMotionSamples[0].RootLocalRotationY;
                return true;
            }

            float t = Mathf.Clamp01(clipProgressNormalized);
            AttackRootMotionSampleDefinition previous = RootMotionSamples[0];

            if (t <= previous.ClipProgressNormalized)
            {
                rootLocalPosition = previous.RootLocalPosition;
                rootLocalRotationY = previous.RootLocalRotationY;
                return true;
            }

            for (int i = 1; i < RootMotionSamples.Count; i++)
            {
                AttackRootMotionSampleDefinition next = RootMotionSamples[i];
                if (t > next.ClipProgressNormalized)
                {
                    previous = next;
                    continue;
                }

                float range = next.ClipProgressNormalized - previous.ClipProgressNormalized;
                float lerpT = range > 0.0001f
                    ? Mathf.Clamp01((t - previous.ClipProgressNormalized) / range)
                    : 0f;

                rootLocalPosition = Vector3.LerpUnclamped(previous.RootLocalPosition, next.RootLocalPosition, lerpT);
                rootLocalRotationY = Mathf.LerpAngle(previous.RootLocalRotationY, next.RootLocalRotationY, lerpT);
                return true;
            }

            AttackRootMotionSampleDefinition last = RootMotionSamples[RootMotionSamples.Count - 1];
            rootLocalPosition = last.RootLocalPosition;
            rootLocalRotationY = last.RootLocalRotationY;
            return true;
        }
    }

    [CreateAssetMenu(fileName = "New Attack Geometry", menuName = "BBBNexus/Weapons/Attack Geometry")]
    public sealed class AttackClipGeometryDefinition : ScriptableObject
    {
        [Tooltip("关联的武器 ID（可选，用于快速查找）")]
        public string WeaponId;

        [Tooltip("显示名称")]
        public string DisplayName;

        [Tooltip("攻击片段几何体定义列表")]
        public List<AttackClipGeometryClipDefinition> Clips = new List<AttackClipGeometryClipDefinition>();

        public AttackClipGeometryClipDefinition GetClip(int comboIndex)
        {
            if (Clips == null)
            {
                return null;
            }

            for (int i = 0; i < Clips.Count; i++)
            {
                var clip = Clips[i];
                if (clip != null && clip.ComboIndex == comboIndex)
                {
                    return clip;
                }
            }

            return null;
        }
    }
}
