using System;
using UnityEngine;

namespace BBBNexus
{
    internal static class RangedHitZoneUtility
    {
        internal static DamageHitZoneType ResolveHitZone(Collider collider)
        {
            if (collider == null)
                return DamageHitZoneType.Default;

            var explicitZone = collider.GetComponentInParent<DamageHitZone>();
            if (explicitZone != null && explicitZone.Zone != DamageHitZoneType.Default)
                return explicitZone.Zone;

            Transform current = collider.transform;
            while (current != null)
            {
                DamageHitZoneType inferred = InferFromName(current.name);
                if (inferred != DamageHitZoneType.Default)
                    return inferred;

                current = current.parent;
            }

            return DamageHitZoneType.Default;
        }

        private static DamageHitZoneType InferFromName(string sourceName)
        {
            if (string.IsNullOrWhiteSpace(sourceName))
                return DamageHitZoneType.Default;

            string name = sourceName.Trim().ToLowerInvariant();

            if (ContainsAny(name, "head", "neck"))
                return DamageHitZoneType.Head;

            if (ContainsAny(name, "spine", "chest", "torso", "body", "pelvis", "hips"))
                return DamageHitZoneType.Torso;

            if (ContainsAny(name, "arm", "hand", "shoulder", "elbow", "wrist"))
                return DamageHitZoneType.Arm;

            if (ContainsAny(name, "leg", "foot", "thigh", "calf", "knee", "ankle"))
                return DamageHitZoneType.Leg;

            return DamageHitZoneType.Default;
        }

        private static bool ContainsAny(string value, params string[] parts)
        {
            for (int i = 0; i < parts.Length; i++)
            {
                if (value.IndexOf(parts[i], StringComparison.Ordinal) >= 0)
                    return true;
            }

            return false;
        }
    }
}
