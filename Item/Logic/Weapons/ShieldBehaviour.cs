using UnityEngine;

namespace BBBNexus
{
    /// <summary>
    /// 盾牌行为组件。
    /// 挂在盾牌 Prefab 根节点上，同节点需有 Collider。
    ///
    /// 工作流：
    ///   敌人 MeleeHitScanner 扫到盾牌 Collider
    ///     → GetComponentInParent&lt;IDamageable&gt;() 找到本组件（优先于持盾者）
    ///     → RequestDamage()
    ///       1. 通知持盾者本帧已被盾拦截（HealthArbiter 将跳过队列）
    ///       2. 对攻击者施加 BlockedEffect 硬直
    /// </summary>
    public class ShieldBehaviour : MonoBehaviour, IHoldableItem, IDamageable
    {
        [Header("--- 格挡挂点 ---")]
        [Tooltip("格挡朝向参考点。优先使用这个挂点的 forward 作为盾牌正面，避免骨骼动画导致根节点朝向不稳定。")]
        [SerializeField] private Transform _blockMuzzle;

        private BBBCharacterController _owner;
        private ShieldSO _config;
        private int _lastProcessedFrame = -1;
        private int _lastProcessedAttackerId;
        private int _lastProcessedWeaponId;

        public EquipmentSlot CurrentEquipSlot { get; set; }

        public void Initialize(ItemInstance instanceData)
        {
            _config = instanceData?.GetSODataAs<ShieldSO>();
            if (_config != null)
                CurrentEquipSlot = _config.EquipSlot;
        }

        public void OnEquipEnter(BBBCharacterController player)
        {
            _owner = player;
        }

        public void OnUpdateLogic() { }

        public void OnForceUnequip()
        {
            _owner = null;
        }

        public bool CanBlock(in DamageRequest request)
        {
            if (_owner == null)
                return false;

            var attackerTransform = request.ResolveAttackerTransform();
            if (attackerTransform == null)
                return false;

            Transform facingSource = _blockMuzzle != null ? _blockMuzzle : transform;
            Vector3 blockForward = _config != null && _config.UseNegativeForwardAsBlockFront
                ? -facingSource.forward
                : facingSource.forward;
            blockForward.y = 0f;
            if (blockForward.sqrMagnitude < 0.0001f)
                return false;
            blockForward.Normalize();

            Vector3 toAttacker = attackerTransform.position - _owner.transform.position;
            toAttacker.y = 0f;
            if (toAttacker.sqrMagnitude < 0.0001f)
                return false;
            toAttacker.Normalize();

            float arcDegrees = _config != null ? Mathf.Clamp(_config.BlockArcDegrees, 0f, 180f) : 150f;
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
            _owner?.NotifyShieldBlocked();

            if (_config?.BlockedEffect != null)
            {
                var attacker = request.ResolveAttackerController();
                attacker?.StatusEffects.Apply(_config.BlockedEffect);
            }

            return true;
        }

        public void RequestDamage(in DamageRequest request)
        {
            if (TryBlock(in request))
                return;

            _owner?.RequestDamage(in request);
        }

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
