using System.Collections.Generic;
using UnityEngine;

namespace BBBNexus
{
    public static class AttackClipGeometryTemplateFactory
    {
        private const float DefaultCoverageFactor = 0.72f;

        private readonly struct SphereAnchor
        {
            public readonly Vector3 Position;
            public readonly float Radius;

            public SphereAnchor(float x, float y, float z, float radius)
            {
                Position = new Vector3(x, y, z);
                Radius = radius;
            }
        }

        public static AttackClipGeometryDefinition CreateForFists(FistsSO fists)
        {
            var definition = new AttackClipGeometryDefinition
            {
                WeaponId = fists.ItemID,
                DisplayName = string.IsNullOrWhiteSpace(fists.DisplayName) ? fists.name : fists.DisplayName,
                Clips = new List<AttackClipGeometryClipDefinition>(),
            };

            int comboCount = fists.ComboSequence != null ? fists.ComboSequence.Length : 0;
            for (int i = 0; i < comboCount; i++)
            {
                definition.Clips.Add(new AttackClipGeometryClipDefinition
                {
                    ComboIndex = i,
                    DisplayName = $"Combo {i + 1}",
                    Samples = BuildDefaultSamplesForClip(i),
                });
            }

            return definition;
        }

        private static List<AttackGeometrySampleDefinition> BuildDefaultSamplesForClip(int comboIndex)
        {
            int patternIndex = comboIndex % 3;
            return patternIndex switch
            {
                0 => BuildForwardJabPattern(),
                1 => BuildCrossBodyPattern(),
                _ => BuildWideSwingPattern(),
            };
        }

        private static List<AttackGeometrySampleDefinition> BuildForwardJabPattern()
        {
            return BuildSphereSweep(
                new SphereAnchor(0.04f, 0.98f, 0.24f, 0.13f),
                new SphereAnchor(0.12f, 1.07f, 0.58f, 0.15f),
                new SphereAnchor(0.10f, 1.10f, 0.88f, 0.17f));
        }

        private static List<AttackGeometrySampleDefinition> BuildCrossBodyPattern()
        {
            return BuildSphereSweep(
                new SphereAnchor(-0.24f, 0.98f, 0.22f, 0.13f),
                new SphereAnchor(-0.04f, 1.10f, 0.60f, 0.15f),
                new SphereAnchor(0.20f, 1.12f, 0.80f, 0.16f));
        }

        private static List<AttackGeometrySampleDefinition> BuildWideSwingPattern()
        {
            return BuildSphereSweep(
                new SphereAnchor(0.30f, 1.00f, 0.18f, 0.13f),
                new SphereAnchor(0.04f, 1.10f, 0.52f, 0.16f),
                new SphereAnchor(-0.30f, 1.06f, 0.78f, 0.18f));
        }

        private static List<AttackGeometrySampleDefinition> BuildSphereSweep(params SphereAnchor[] anchors)
        {
            var samples = new List<AttackGeometrySampleDefinition>();
            if (anchors == null || anchors.Length == 0)
            {
                return samples;
            }

            if (anchors.Length == 1)
            {
                samples.Add(BuildSphereSample(0f, anchors[0].Position, anchors[0].Radius));
                return samples;
            }

            float totalLength = 0f;
            for (int i = 0; i < anchors.Length - 1; i++)
            {
                totalLength += Vector3.Distance(anchors[i].Position, anchors[i + 1].Position);
            }

            if (totalLength <= 0.0001f)
            {
                samples.Add(BuildSphereSample(0f, anchors[0].Position, anchors[0].Radius));
                samples.Add(BuildSphereSample(1f, anchors[^1].Position, anchors[^1].Radius));
                return samples;
            }

            float traversedLength = 0f;
            samples.Add(BuildSphereSample(0f, anchors[0].Position, anchors[0].Radius));

            for (int segmentIndex = 0; segmentIndex < anchors.Length - 1; segmentIndex++)
            {
                SphereAnchor from = anchors[segmentIndex];
                SphereAnchor to = anchors[segmentIndex + 1];
                float segmentLength = Vector3.Distance(from.Position, to.Position);
                if (segmentLength <= 0.0001f)
                {
                    continue;
                }

                float maxSpacing = Mathf.Max(0.01f, (from.Radius + to.Radius) * DefaultCoverageFactor);
                int subdivisions = Mathf.Max(1, Mathf.CeilToInt(segmentLength / maxSpacing));

                for (int step = 1; step <= subdivisions; step++)
                {
                    float localT = step / (float)subdivisions;
                    float progress = (traversedLength + segmentLength * localT) / totalLength;
                    Vector3 position = Vector3.LerpUnclamped(from.Position, to.Position, localT);
                    float radius = Mathf.LerpUnclamped(from.Radius, to.Radius, localT);
                    samples.Add(BuildSphereSample(progress, position, radius));
                }

                traversedLength += segmentLength;
            }

            if (samples.Count > 0)
            {
                samples[^1].SweepProgressNormalized = 1f;
            }

            return samples;
        }

        private static AttackGeometrySampleDefinition BuildSphereSample(float progress, Vector3 position, float radius)
        {
            return new AttackGeometrySampleDefinition
            {
                SweepProgressNormalized = progress,
                Shapes = new List<AttackGeometryShapeDefinition>
                {
                    new AttackGeometryShapeDefinition
                    {
                        ShapeType = AttackGeometryShapeType.Sphere,
                        LocalPosition = new SerializableVector3(position.x, position.y, position.z),
                        LocalEulerAngles = new SerializableVector3(0f, 0f, 0f),
                        Radius = radius,
                        Height = 0f,
                        HalfExtents = new SerializableVector3(0f, 0f, 0f),
                    }
                }
            };
        }
    }
}
