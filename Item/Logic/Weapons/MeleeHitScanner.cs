using System.Collections.Generic;
using UnityEngine;

namespace BBBNexus
{
    /// <summary>
    /// 通用近战命中扫描器：
    /// - 主动查询重叠命中，不依赖 OnTriggerEnter/OnCollision
    /// - 使用 HashSet 去重，保证一次攻击窗口内每个目标只受击一次
    /// - 支持多 Collider，并根据实际 Collider 类型选择最合适的 Physics 查询
    /// </summary>
    public sealed class MeleeHitScanner
    {
        private const int DefaultCapacity = 16;

        private readonly List<Collider> _sourceColliders;
        private readonly Collider[] _overlaps;
        private readonly HashSet<IDamageable> _hitThisWindow = new HashSet<IDamageable>();
        private HashSet<IDamageable> _activeHitSet;

        private BBBCharacterController _owner;

        public MeleeHitScanner(List<Collider> sourceColliders, int capacity = DefaultCapacity)
        {
            _sourceColliders = sourceColliders ?? new List<Collider>();
            _overlaps = new Collider[Mathf.Max(1, capacity)];
        }

        public MeleeHitScanner(Collider sourceCollider, int capacity = DefaultCapacity)
            : this(new List<Collider> { sourceCollider }, capacity)
        {
        }

        public void SetOwner(BBBCharacterController owner)
        {
            _owner = owner;
        }

        public void BeginWindow()
        {
            BeginWindow(null, true);
        }

        public void BeginWindow(HashSet<IDamageable> sharedHitSet, bool clearHitSet)
        {
            _activeHitSet = sharedHitSet ?? _hitThisWindow;
            if (clearHitSet)
                _activeHitSet.Clear();
        }

        public void EndWindow()
        {
            _activeHitSet?.Clear();
            _activeHitSet = null;
        }

        public void Scan(float damage, System.Action<Collider, IDamageable, DamageRequest> onHit = null)
        {
            if (_sourceColliders == null || _sourceColliders.Count == 0) return;

            for (int i = 0; i < _sourceColliders.Count; i++)
            {
                Collider collider = _sourceColliders[i];
                if (collider == null || !collider.enabled) continue;

                int count = OverlapCollider(collider);
                for (int j = 0; j < count; j++)
                {
                    TryDamage(_overlaps[j], damage, onHit, collider);
                    _overlaps[j] = null;
                }
            }
        }

        private int OverlapCollider(Collider collider)
        {
            switch (collider)
            {
                case SphereCollider sphere:
                {
                    Vector3 worldCenter = sphere.transform.TransformPoint(sphere.center);
                    float maxScale = Mathf.Max(
                        Mathf.Abs(sphere.transform.lossyScale.x),
                        Mathf.Max(Mathf.Abs(sphere.transform.lossyScale.y),
                            Mathf.Abs(sphere.transform.lossyScale.z)));
                    float worldRadius = sphere.radius * maxScale;
                    return Physics.OverlapSphereNonAlloc(
                        worldCenter, worldRadius, _overlaps, ~0, QueryTriggerInteraction.Collide);
                }

                case CapsuleCollider capsule:
                {
                    GetCapsuleWorldPoints(capsule, out Vector3 point0, out Vector3 point1, out float worldRadius);
                    return Physics.OverlapCapsuleNonAlloc(
                        point0, point1, worldRadius, _overlaps, ~0, QueryTriggerInteraction.Collide);
                }

                case BoxCollider box:
                {
                    Vector3 worldCenter = box.transform.TransformPoint(box.center);
                    Vector3 halfExtents = Vector3.Scale(box.size * 0.5f, box.transform.lossyScale);
                    halfExtents.x = Mathf.Abs(halfExtents.x);
                    halfExtents.y = Mathf.Abs(halfExtents.y);
                    halfExtents.z = Mathf.Abs(halfExtents.z);
                    return Physics.OverlapBoxNonAlloc(
                        worldCenter, halfExtents, _overlaps, box.transform.rotation, ~0, QueryTriggerInteraction.Collide);
                }

                default:
                {
                    Bounds bounds = collider.bounds;
                    return Physics.OverlapBoxNonAlloc(
                        bounds.center, bounds.extents, _overlaps,
                        collider.transform.rotation, ~0, QueryTriggerInteraction.Collide);
                }
            }
        }

        private static void GetCapsuleWorldPoints(CapsuleCollider capsule, out Vector3 point0, out Vector3 point1, out float worldRadius)
        {
            Transform t = capsule.transform;
            Vector3 lossyScale = t.lossyScale;
            Vector3 center = t.TransformPoint(capsule.center);

            int direction = capsule.direction;
            float axisScale;
            float radiusScale;
            switch (direction)
            {
                case 0: // X
                    axisScale = Mathf.Abs(lossyScale.x);
                    radiusScale = Mathf.Max(Mathf.Abs(lossyScale.y), Mathf.Abs(lossyScale.z));
                    break;
                case 1: // Y
                    axisScale = Mathf.Abs(lossyScale.y);
                    radiusScale = Mathf.Max(Mathf.Abs(lossyScale.x), Mathf.Abs(lossyScale.z));
                    break;
                default: // Z
                    axisScale = Mathf.Abs(lossyScale.z);
                    radiusScale = Mathf.Max(Mathf.Abs(lossyScale.x), Mathf.Abs(lossyScale.y));
                    break;
            }

            worldRadius = capsule.radius * radiusScale;
            float halfHeight = Mathf.Max(0f, capsule.height * 0.5f * axisScale - worldRadius);

            Vector3 localAxis;
            switch (direction)
            {
                case 0: localAxis = Vector3.right; break;
                case 1: localAxis = Vector3.up; break;
                default: localAxis = Vector3.forward; break;
            }

            Vector3 worldAxis = t.rotation * localAxis;
            point0 = center - worldAxis * halfHeight;
            point1 = center + worldAxis * halfHeight;
        }

        private void TryDamage(Collider other, float damage, System.Action<Collider, IDamageable, DamageRequest> onHit, Collider sourceCollider)
        {
            if (other == null) return;
            if (_owner != null && other.transform.IsChildOf(_owner.transform)) return;

            // 排除自身的检测 Collider
            if (_sourceColliders.Contains(other)) return;

            var damageable = other.GetComponentInParent<IDamageable>();
            if (damageable == null) return;
            var hitSet = _activeHitSet ?? _hitThisWindow;
            if (hitSet.Contains(damageable)) return;

            var hitPoint = other.ClosestPoint(sourceCollider.bounds.center);
            var request = new DamageRequest(
                damage,
                hitPoint,
                _owner != null ? _owner.gameObject : null,
                sourceCollider.transform);
            bool applied = damageable.RequestDamage(in request);
            if (applied)
                onHit?.Invoke(other, damageable, request);
            hitSet.Add(damageable);
        }

        public bool TryGetQueryBox(out Vector3 center, out Vector3 halfExtents, out Quaternion rotation)
        {
            if (_sourceColliders == null || _sourceColliders.Count == 0)
            {
                center = default;
                halfExtents = default;
                rotation = Quaternion.identity;
                return false;
            }

            // 合并所有 Collider 的 AABB
            Bounds merged = _sourceColliders[0].bounds;
            for (int i = 1; i < _sourceColliders.Count; i++)
            {
                if (_sourceColliders[i] != null)
                    merged.Encapsulate(_sourceColliders[i].bounds);
            }

            center = merged.center;
            halfExtents = merged.extents;
            rotation = _sourceColliders.Count == 1 ? _sourceColliders[0].transform.rotation : Quaternion.identity;
            return true;
        }
    }
}
