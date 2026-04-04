using UnityEngine;

namespace BBBNexus
{
    /// <summary>
    /// 盾牌行为组件。继承 WeaponBehaviour 获得完整的近战/远程/姿态能力，
    /// 同时实现 IDamageable 叠加格挡逻辑。
    ///
    /// Prefab 结构约定：
    ///   Shield (Root)  ← ShieldBehaviour + 格挡 Collider (non-trigger)
    ///   └── AttackHitbox (Child)  ← FistHitbox + 攻击 Collider（由 FistHitbox 自动刷成 trigger）
    ///
    /// 格挡工作流：
    ///   敌人 MeleeHitScanner 扫到根节点 Collider
    ///     → GetComponentInParent&lt;IDamageable&gt;() 找到本组件
    ///     → RequestDamage() → TryBlock()
    ///       1. 通知持盾者本帧已被盾拦截（HealthArbiter 将跳过队列）
    ///       2. 对攻击者施加 BlockedEffect 硬直
    /// </summary>
    public class ShieldBehaviour : WeaponBehaviour, IDamageable
    {
        [Header("--- 格挡挂点 ---")]
        [Tooltip("格挡朝向参考点。优先使用这个挂点的 forward 作为盾牌正面，避免骨骼动画导致根节点朝向不稳定。")]
        [SerializeField] private Transform _blockMuzzle;

        private ShieldSO _shieldConfig;
        private BBBCharacterController _shieldOwner;

        private int _lastProcessedFrame = -1;
        private int _lastProcessedAttackerId;
        private int _lastProcessedWeaponId;

        // ─────────────────────────────────────────────────────
        // IHoldableItem overrides
        // ─────────────────────────────────────────────────────

        public override void Initialize(ItemInstance instanceData)
        {
            base.Initialize(instanceData);
            _shieldConfig = instanceData?.GetSODataAs<ShieldSO>();
        }

        public override void OnEquipEnter(BBBCharacterController player)
        {
            base.OnEquipEnter(player);
            _shieldOwner = player;
        }

        public override void OnForceUnequip()
        {
            base.OnForceUnequip();
            _shieldOwner = null;
        }

        // ─────────────────────────────────────────────────────
        // IDamageable — 格挡逻辑
        // ─────────────────────────────────────────────────────

        public bool CanBlock(in DamageRequest request)
        {
            if (_shieldOwner == null)
                return false;

            var attackerTransform = request.ResolveAttackerTransform();
            if (attackerTransform == null)
                return false;

            Transform facingSource = _blockMuzzle != null ? _blockMuzzle : transform;
            Vector3 blockForward = _shieldConfig != null && _shieldConfig.UseNegativeForwardAsBlockFront
                ? -facingSource.forward
                : facingSource.forward;
            blockForward.y = 0f;
            if (blockForward.sqrMagnitude < 0.0001f)
                return false;
            blockForward.Normalize();

            Vector3 toAttacker = attackerTransform.position - _shieldOwner.transform.position;
            toAttacker.y = 0f;
            if (toAttacker.sqrMagnitude < 0.0001f)
                return false;
            toAttacker.Normalize();

            float arcDegrees = _shieldConfig != null ? Mathf.Clamp(_shieldConfig.BlockArcDegrees, 0f, 180f) : 150f;
            float minDot = Mathf.Cos(arcDegrees * 0.5f * Mathf.Deg2Rad);
            return Vector3.Dot(blockForward, toAttacker) >= minDot;
        }

        public bool TryBlock(in DamageRequest request)
        {
            if (!CanBlock(in request))
                return false;

            if (IsDuplicateProcessedRequest(in request))
                return true;

            RememberProcessedRequest(in request);
            _shieldOwner?.NotifyShieldBlocked();

            if (_shieldConfig?.BlockedEffect != null)
            {
                var attacker = request.ResolveAttackerController();
                attacker?.StatusEffects.Apply(_shieldConfig.BlockedEffect);
            }

            return true;
        }

        public void RequestDamage(in DamageRequest request)
        {
            if (TryBlock(in request))
                return;

            _shieldOwner?.RequestDamage(in request);
        }

        // ─────────────────────────────────────────────────────
        // 去重辅助
        // ─────────────────────────────────────────────────────

        private bool IsDuplicateProcessedRequest(in DamageRequest request)
        {
            if (_lastProcessedFrame != Time.frameCount)
                return false;

            int attackerId = request.Attacker != null ? request.Attacker.GetInstanceID() : 0;
            int weaponId = request.WeaponTransform != null ? request.WeaponTransform.GetInstanceID() : 0;
            return _lastProcessedAttackerId == attackerId && _lastProcessedWeaponId == weaponId;
        }

        private void RememberProcessedRequest(in DamageRequest request)
        {
            _lastProcessedFrame = Time.frameCount;
            _lastProcessedAttackerId = request.Attacker != null ? request.Attacker.GetInstanceID() : 0;
            _lastProcessedWeaponId = request.WeaponTransform != null ? request.WeaponTransform.GetInstanceID() : 0;
        }
    }
}
