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

        public List<AttackGeometryShapeDefinition> Shapes = new List<AttackGeometryShapeDefinition>();

        [OnDeserialized]
        internal void OnDeserialized(StreamingContext _)
        {
            if (Mathf.Approximately(SweepProgressNormalized, 0f) && !Mathf.Approximately(TimeNormalized, 0f))
            {
                SweepProgressNormalized = TimeNormalized;
            }
        }
    }

    [Serializable]
    public sealed class AttackClipGeometryClipDefinition
    {
        public int ComboIndex;
        public string DisplayName;
        public List<AttackGeometrySampleDefinition> Samples = new List<AttackGeometrySampleDefinition>();
    }

    [Serializable]
    public sealed class AttackClipGeometryDefinition
    {
        public string WeaponId;
        public string DisplayName;
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
