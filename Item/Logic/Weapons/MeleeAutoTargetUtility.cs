using System.Collections.Generic;
using UnityEngine;

namespace BBBNexus
{
    public static class MeleeAutoTargetUtility
    {
        private const int MaxOverlapCount = 32;
        private static readonly Collider[] Overlaps = new Collider[MaxOverlapCount];

        public readonly struct Candidate
        {
            public readonly Transform TargetTransform;
            public readonly float Angle;
            public readonly float Distance;
            public readonly float TargetYaw;

            public Candidate(Transform targetTransform, float angle, float distance, float targetYaw)
            {
                TargetTransform = targetTransform;
                Angle = angle;
                Distance = distance;
                TargetYaw = targetYaw;
            }
        }

        public static bool TryFindBestCandidate(
            BBBCharacterController owner,
            float searchRange,
            float maxAllowedAngle,
            out Candidate candidate)
        {
            candidate = default;
            if (owner == null)
            {
                return false;
            }

            Vector3 origin = owner.transform.position;
            int count = Physics.OverlapSphereNonAlloc(origin, searchRange, Overlaps, ~0, QueryTriggerInteraction.Collide);
            if (count <= 0)
            {
                return false;
            }

            var seenRoots = new HashSet<Transform>();
            bool found = false;
            float bestAngle = float.MaxValue;
            float bestDistance = float.MaxValue;
            Candidate best = default;

            for (int i = 0; i < count; i++)
            {
                Collider other = Overlaps[i];
                Overlaps[i] = null;
                if (other == null)
                {
                    continue;
                }

                if (other.transform.IsChildOf(owner.transform))
                {
                    continue;
                }

                var damageable = other.GetComponentInParent<IDamageable>();
                if (damageable == null)
                {
                    continue;
                }

                Transform root = other.transform.root;
                if (root == null || !seenRoots.Add(root))
                {
                    continue;
                }

                Vector3 targetPoint = other.bounds.center;
                Vector3 toTarget = targetPoint - origin;
                toTarget.y = 0f;
                float distance = toTarget.magnitude;
                if (distance <= 0.001f || distance > searchRange)
                {
                    continue;
                }

                Vector3 direction = toTarget / distance;
                float signedAngle = Vector3.SignedAngle(owner.transform.forward, direction, Vector3.up);
                float absAngle = Mathf.Abs(signedAngle);
                if (absAngle > maxAllowedAngle)
                {
                    continue;
                }

                float targetYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
                if (!found || absAngle < bestAngle - 0.01f || (Mathf.Abs(absAngle - bestAngle) <= 0.01f && distance < bestDistance))
                {
                    found = true;
                    bestAngle = absAngle;
                    bestDistance = distance;
                    best = new Candidate(root, absAngle, distance, targetYaw);
                }
            }

            candidate = best;
            return found;
        }
    }
}
