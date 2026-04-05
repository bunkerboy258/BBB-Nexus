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

        [Header("--- 破除特效 ---")]
        [Tooltip("完美弹反期间替换的材质（使用 BBBNexus/ShieldBreakEffect Shader）。\n为空则不切换材质。")]
        [SerializeField] private Material _breakEffectMaterial;

        [Tooltip("需要切换材质的 Renderer 列表。留空则自动查找自身所有 Renderer（不含子物体攻击判定盒）。")]
        [SerializeField] private Renderer[] _shieldRenderers;

        private ShieldSO _shieldConfig;
        private BBBCharacterController _shieldOwner;

        private int _lastProcessedFrame = -1;
        private int _lastProcessedAttackerId;
        private int _lastProcessedWeaponId;

        // 材质切换状态
        private Material[][] _originalMaterials;   // 每个 Renderer 的原始材质数组
        private bool         _breakEffectActive;

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
            CacheRenderers();
        }

        public override void OnForceUnequip()
        {
            base.OnForceUnequip();
            RestoreOriginalMaterials();
            _shieldOwner = null;
        }

        public override void OnUpdateLogic()
        {
            base.OnUpdateLogic();
            if (_shieldOwner == null || _breakEffectMaterial == null) return;

            bool shouldBreak = _shieldOwner.RuntimeData.Arbitration.BlockShield;
            if (shouldBreak == _breakEffectActive) return;

            _breakEffectActive = shouldBreak;
            if (shouldBreak)
                ApplyBreakMaterials();
            else
                RestoreOriginalMaterials();
        }

        // ─────────────────────────────────────────────────────
        // IDamageable — 格挡逻辑
        // ─────────────────────────────────────────────────────

        public bool CanBlock(in DamageRequest request)
        {
            if (_shieldOwner == null)
                return false;

            if (_shieldOwner.RuntimeData.Arbitration.BlockShield)
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

        public bool RequestDamage(in DamageRequest request)
        {
            if (TryBlock(in request))
                return false;

            return _shieldOwner != null && _shieldOwner.RequestDamage(in request);
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

        // ─────────────────────────────────────────────────────
        // 材质切换辅助
        // ─────────────────────────────────────────────────────

        private void CacheRenderers()
        {
            if (_shieldRenderers == null || _shieldRenderers.Length == 0)
                _shieldRenderers = GetComponentsInChildren<Renderer>();

            _originalMaterials = new Material[_shieldRenderers.Length][];
            for (int i = 0; i < _shieldRenderers.Length; i++)
                _originalMaterials[i] = _shieldRenderers[i].sharedMaterials;

            _breakEffectActive = false;
        }

        private void ApplyBreakMaterials()
        {
            if (_shieldRenderers == null) return;
            var mats = new Material[] { _breakEffectMaterial };
            foreach (var r in _shieldRenderers)
                if (r != null) r.materials = mats;
        }

        private void RestoreOriginalMaterials()
        {
            if (_shieldRenderers == null || _originalMaterials == null) return;
            for (int i = 0; i < _shieldRenderers.Length; i++)
                if (_shieldRenderers[i] != null && i < _originalMaterials.Length)
                    _shieldRenderers[i].materials = _originalMaterials[i];

            _breakEffectActive = false;
        }
    }
}
