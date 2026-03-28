using UnityEngine;
using System.Collections.Generic;

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

        private Collider _hitCollider;
        private MeleeHitScanner _scanner;

        private void Awake()
        {
            _hitCollider = GetComponent<Collider>();
            _scanner = new MeleeHitScanner(_hitCollider);
            enabled = false;
        }

        public void SetOwner(BBBCharacterController owner)
        {
            _scanner?.SetOwner(owner);
        }

        /// <summary>
        /// 开启碰撞检测，清除本次已命中记录
        /// </summary>
        public void Activate()
        {
            Activate(null, true);
        }

        public void Activate(HashSet<IDamageable> sharedHitSet, bool clearHitSet)
        {
            _scanner?.BeginWindow(sharedHitSet, clearHitSet);
            enabled = true;
            PerformScan();
        }

        /// <summary>
        /// 关闭碰撞检测
        /// </summary>
        public void Deactivate()
        {
            _scanner?.EndWindow();
            enabled = false;
        }

        private void FixedUpdate()
        {
            if (!enabled) return;
            PerformScan();
        }

        private void PerformScan()
        {
            _scanner?.Scan(Damage, static (other, damageable, request) =>
            {
                Debug.Log($"[FistHitbox] 命中！目标：{other.name}, 伤害：{request.Amount}, IDamageable 类型：{damageable.GetType().Name}");
            });
        }
    }
}
