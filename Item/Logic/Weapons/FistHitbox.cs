using UnityEngine;

namespace BBBNexus
{
    /// <summary>
    /// 拳头碰撞体：挂在 Fists Prefab 上，由 FistsBehaviour 控制启用时机。
    /// 每次激活只对同一目标造成一次伤害，避免单段攻击重复命中。
    /// 需要在同 GameObject 或子物体上配置 Collider（Is Trigger = true）。
    /// </summary>
    public class FistHitbox : MonoBehaviour
    {
        [Header("伤害配置")]
        public float Damage = 10f;

        private BBBCharacterController _owner;

        // 本次激活期间已命中的目标，防止同一挥拳重复伤害
        private readonly System.Collections.Generic.HashSet<IDamageable> _hitThisSwing
            = new System.Collections.Generic.HashSet<IDamageable>();

        private void Awake()
        {
            // 默认关闭，由 FistsBehaviour 在攻击帧开启
            enabled = false;
        }

        public void SetOwner(BBBCharacterController owner)
        {
            _owner = owner;
        }

        /// <summary>
        /// 开启碰撞检测，清除本次已命中记录
        /// </summary>
        public void Activate()
        {
            _hitThisSwing.Clear();
            enabled = true;
        }

        /// <summary>
        /// 关闭碰撞检测
        /// </summary>
        public void Deactivate()
        {
            enabled = false;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!enabled) return;

            // 不打自己
            if (_owner != null && other.transform.IsChildOf(_owner.transform)) return;

            var damageable = other.GetComponentInParent<IDamageable>();
            if (damageable == null) return;
            if (_hitThisSwing.Contains(damageable)) return;

            _hitThisSwing.Add(damageable);

            var req = new DamageRequest(Damage);
            damageable.RequestDamage(in req);
        }
    }
}
