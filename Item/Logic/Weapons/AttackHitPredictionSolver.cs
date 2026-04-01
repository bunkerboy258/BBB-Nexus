using UnityEngine;

namespace BBBNexus
{
    public static class AttackHitPredictionSolver
    {
        public readonly struct SolveInput
        {
            public readonly AttackClipGeometryClipDefinition Clip;
            public readonly Transform AttackerRoot;
            public readonly Bounds TargetBounds;
            public readonly float RemainingAlignmentTime;
            public readonly float TurnSpeed;
            public readonly float MaxStepDistance;
            public readonly int YawSampleCount;
            public readonly int StepSampleCount;

            public SolveInput(
                AttackClipGeometryClipDefinition clip,
                Transform attackerRoot,
                Bounds targetBounds,
                float remainingAlignmentTime,
                float turnSpeed,
                float maxStepDistance,
                int yawSampleCount = 9,
                int stepSampleCount = 5)
            {
                Clip = clip;
                AttackerRoot = attackerRoot;
                TargetBounds = targetBounds;
                RemainingAlignmentTime = remainingAlignmentTime;
                TurnSpeed = turnSpeed;
                MaxStepDistance = maxStepDistance;
                YawSampleCount = yawSampleCount;
                StepSampleCount = stepSampleCount;
            }
        }

        public readonly struct SolveResult
        {
            public readonly bool CanHit;
            public readonly float BestYawOffset;
            public readonly float BestWorldYaw;
            public readonly float BestStepDistance;
            public readonly float BestCost;

            public SolveResult(bool canHit, float bestYawOffset, float bestWorldYaw, float bestStepDistance, float bestCost)
            {
                CanHit = canHit;
                BestYawOffset = bestYawOffset;
                BestWorldYaw = bestWorldYaw;
                BestStepDistance = bestStepDistance;
                BestCost = bestCost;
            }
        }

        private static readonly Vector3[] BoundsProbeOffsets =
        {
            new(0f, 0f, 0f),
            new(-1f, -1f, -1f),
            new(-1f, -1f, 1f),
            new(-1f, 1f, -1f),
            new(-1f, 1f, 1f),
            new(1f, -1f, -1f),
            new(1f, -1f, 1f),
            new(1f, 1f, -1f),
            new(1f, 1f, 1f),
            new(-1f, 0f, 0f),
            new(1f, 0f, 0f),
            new(0f, -1f, 0f),
            new(0f, 1f, 0f),
            new(0f, 0f, -1f),
            new(0f, 0f, 1f),
        };

        public static bool TrySolve(in SolveInput input, out SolveResult result)
        {
            result = default;
            if (input.AttackerRoot == null || input.Clip?.Samples == null || input.Clip.Samples.Count == 0)
            {
                return false;
            }

            float maxYawOffset = Mathf.Max(0f, input.RemainingAlignmentTime) * Mathf.Max(0f, input.TurnSpeed);
            int yawSamples = Mathf.Max(1, input.YawSampleCount);
            int stepSamples = Mathf.Max(1, input.StepSampleCount);
            float maxStepDistance = Mathf.Max(0f, input.MaxStepDistance);
            float baseYaw = input.AttackerRoot.eulerAngles.y;
            bool found = false;
            float bestCost = float.MaxValue;
            float bestYawOffset = 0f;
            float bestStepDistance = 0f;
            float bestWorldYaw = baseYaw;

            for (int yawIndex = 0; yawIndex < yawSamples; yawIndex++)
            {
                float yawOffset = EvaluateSampleOffset(yawIndex, yawSamples, maxYawOffset);
                float worldYaw = baseYaw + yawOffset;

                for (int stepIndex = 0; stepIndex < stepSamples; stepIndex++)
                {
                    float stepDistance = stepSamples <= 1
                        ? 0f
                        : (stepIndex / (float)(stepSamples - 1)) * maxStepDistance;

                    if (!ClipHitsBounds(input.AttackerRoot, input.Clip, input.TargetBounds, worldYaw, stepDistance))
                    {
                        continue;
                    }

                    float cost = ComputeCost(yawOffset, stepDistance);
                    if (!found || cost < bestCost - 0.0001f)
                    {
                        found = true;
                        bestCost = cost;
                        bestYawOffset = yawOffset;
                        bestStepDistance = stepDistance;
                        bestWorldYaw = worldYaw;
                    }
                }
            }

            if (!found)
            {
                return false;
            }

            result = new SolveResult(true, bestYawOffset, bestWorldYaw, bestStepDistance, bestCost);
            return true;
        }

        public static float EstimateHorizontalReach(AttackClipGeometryClipDefinition clip)
        {
            if (clip?.Samples == null)
            {
                return 0f;
            }

            float maxReach = 0f;
            for (int sampleIndex = 0; sampleIndex < clip.Samples.Count; sampleIndex++)
            {
                var sample = clip.Samples[sampleIndex];
                if (sample?.Shapes == null)
                {
                    continue;
                }

                for (int shapeIndex = 0; shapeIndex < sample.Shapes.Count; shapeIndex++)
                {
                    var shape = sample.Shapes[shapeIndex];
                    Vector3 localPosition = (Vector3)shape.LocalPosition;
                    float horizontalDistance = new Vector2(localPosition.x, localPosition.z).magnitude;
                    float horizontalExtent = GetHorizontalExtent(shape);
                    maxReach = Mathf.Max(maxReach, horizontalDistance + horizontalExtent);
                }
            }

            return maxReach;
        }

        private static float EvaluateSampleOffset(int index, int sampleCount, float maxYawOffset)
        {
            if (sampleCount <= 1 || maxYawOffset <= 0f)
            {
                return 0f;
            }

            float normalized = index / (float)(sampleCount - 1);
            return Mathf.LerpUnclamped(-maxYawOffset, maxYawOffset, normalized);
        }

        private static float ComputeCost(float yawOffset, float stepDistance)
        {
            return Mathf.Abs(yawOffset) * 0.02f + stepDistance;
        }

        private static bool ClipHitsBounds(
            Transform attackerRoot,
            AttackClipGeometryClipDefinition clip,
            Bounds targetBounds,
            float worldYaw,
            float stepDistance)
        {
            Quaternion yawRotation = Quaternion.Euler(0f, worldYaw, 0f);
            Vector3 rootPosition = attackerRoot.position + yawRotation * (Vector3.forward * stepDistance);

            for (int sampleIndex = 0; sampleIndex < clip.Samples.Count; sampleIndex++)
            {
                var sample = clip.Samples[sampleIndex];
                if (sample?.Shapes == null)
                {
                    continue;
                }

                for (int shapeIndex = 0; shapeIndex < sample.Shapes.Count; shapeIndex++)
                {
                    var shape = sample.Shapes[shapeIndex];
                    if (ShapeHitsBounds(shape, rootPosition, yawRotation, targetBounds))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool ShapeHitsBounds(
            AttackGeometryShapeDefinition shape,
            Vector3 rootPosition,
            Quaternion yawRotation,
            Bounds targetBounds)
        {
            Vector3 worldCenter = rootPosition + yawRotation * (Vector3)shape.LocalPosition;
            Quaternion worldRotation = yawRotation * shape.LocalRotation;

            switch (shape.ShapeType)
            {
                case AttackGeometryShapeType.Sphere:
                    return SphereHitsBounds(worldCenter, Mathf.Max(0.001f, shape.Radius), targetBounds);

                case AttackGeometryShapeType.Capsule:
                    return CapsuleHitsBounds(worldCenter, worldRotation, Mathf.Max(0.001f, shape.Radius), Mathf.Max(0f, shape.Height), targetBounds);

                case AttackGeometryShapeType.Box:
                default:
                    return BoxHitsBounds(worldCenter, worldRotation, (Vector3)shape.HalfExtents, targetBounds);
            }
        }

        private static bool SphereHitsBounds(Vector3 center, float radius, Bounds bounds)
        {
            Vector3 closest = bounds.ClosestPoint(center);
            return (closest - center).sqrMagnitude <= radius * radius;
        }

        private static bool CapsuleHitsBounds(Vector3 center, Quaternion rotation, float radius, float height, Bounds bounds)
        {
            float lineHalfLength = Mathf.Max(0f, height * 0.5f - radius);
            Vector3 axis = rotation * Vector3.up;
            Vector3 start = center - axis * lineHalfLength;
            Vector3 end = center + axis * lineHalfLength;

            const int segmentSamples = 5;
            for (int i = 0; i < segmentSamples; i++)
            {
                float t = i / (float)(segmentSamples - 1);
                Vector3 point = Vector3.LerpUnclamped(start, end, t);
                if (SphereHitsBounds(point, radius, bounds))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool BoxHitsBounds(Vector3 center, Quaternion rotation, Vector3 halfExtents, Bounds bounds)
        {
            Vector3 clampedHalfExtents = new Vector3(
                Mathf.Max(0.001f, halfExtents.x),
                Mathf.Max(0.001f, halfExtents.y),
                Mathf.Max(0.001f, halfExtents.z));

            if (PointInsideOrientedBox(center, center, rotation, clampedHalfExtents))
            {
                return true;
            }

            if (bounds.Contains(center))
            {
                return true;
            }

            Vector3 extents = bounds.extents;
            for (int i = 0; i < BoundsProbeOffsets.Length; i++)
            {
                Vector3 probe = bounds.center + Vector3.Scale(extents, BoundsProbeOffsets[i]);
                if (PointInsideOrientedBox(probe, center, rotation, clampedHalfExtents))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool PointInsideOrientedBox(Vector3 point, Vector3 boxCenter, Quaternion boxRotation, Vector3 halfExtents)
        {
            Vector3 local = Quaternion.Inverse(boxRotation) * (point - boxCenter);
            return Mathf.Abs(local.x) <= halfExtents.x
                   && Mathf.Abs(local.y) <= halfExtents.y
                   && Mathf.Abs(local.z) <= halfExtents.z;
        }

        private static float GetHorizontalExtent(AttackGeometryShapeDefinition shape)
        {
            switch (shape.ShapeType)
            {
                case AttackGeometryShapeType.Sphere:
                    return Mathf.Max(0.001f, shape.Radius);

                case AttackGeometryShapeType.Capsule:
                    return Mathf.Max(0.001f, shape.Radius);

                case AttackGeometryShapeType.Box:
                default:
                    Vector3 halfExtents = (Vector3)shape.HalfExtents;
                    return new Vector2(Mathf.Abs(halfExtents.x), Mathf.Abs(halfExtents.z)).magnitude;
            }
        }
    }
}
