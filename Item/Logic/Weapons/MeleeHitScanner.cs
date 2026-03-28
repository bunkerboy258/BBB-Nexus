using System.Collections.Generic;
using UnityEngine;

namespace BBBNexus
{
    /// <summary>
    /// 通用近战命中扫描器：
    /// - 主动查询重叠命中，不依赖 OnTriggerEnter/OnCollision
    /// - 使用 HashSet 去重，保证一次攻击窗口内每个目标只受击一次
    /// - 以挂载物体上的 Collider 形状作为扫描体积，便于拳头/武器共用
    /// </summary>
    public sealed class MeleeHitScanner
    {
        private const int DefaultCapacity = 16;

        private readonly Collider _sourceCollider;
        private readonly Collider[] _overlaps;
        private readonly HashSet<IDamageable> _hitThisWindow = new HashSet<IDamageable>();
        private HashSet<IDamageable> _activeHitSet;

        private BBBCharacterController _owner;

        public MeleeHitScanner(Collider sourceCollider, int capacity = DefaultCapacity)
        {
            _sourceCollider = sourceCollider;
            _overlaps = new Collider[Mathf.Max(1, capacity)];
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
            if (_sourceCollider == null) return;

            var bounds = _sourceCollider.bounds;
            int count = Physics.OverlapBoxNonAlloc(
                bounds.center,
                bounds.extents,
                _overlaps,
                _sourceCollider.transform.rotation,
                ~0,
                QueryTriggerInteraction.Collide);

            for (int i = 0; i < count; i++)
            {
                TryDamage(_overlaps[i], damage, onHit);
                _overlaps[i] = null;
            }
        }

        private void TryDamage(Collider other, float damage, System.Action<Collider, IDamageable, DamageRequest> onHit)
        {
            if (other == null) return;
            if (_owner != null && other.transform.IsChildOf(_owner.transform)) return;

            var damageable = other.GetComponentInParent<IDamageable>();
            if (damageable == null) return;
            var hitSet = _activeHitSet ?? _hitThisWindow;
            if (hitSet.Contains(damageable)) return;

            var hitPoint = other.ClosestPoint(_sourceCollider.bounds.center);
            var request = new DamageRequest(
                damage,
                hitPoint,
                _owner != null ? _owner.gameObject : null,
                _sourceCollider.transform);
            onHit?.Invoke(other, damageable, request);
            hitSet.Add(damageable);
            damageable.RequestDamage(in request);
        }
    }
}
