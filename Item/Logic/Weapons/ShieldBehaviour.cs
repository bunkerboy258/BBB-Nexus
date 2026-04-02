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
        private BBBCharacterController _owner;
        private ShieldSO _config;

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

        public void RequestDamage(in DamageRequest request)
        {
            // 1. 标记持盾者本帧免伤（防穿盾）
            _owner?.NotifyShieldBlocked();

            // 2. 对攻击者施加硬直
            if (_config?.BlockedEffect == null) return;
            var attacker = request.ResolveAttackerController();
            attacker?.StatusEffects.Apply(_config.BlockedEffect);
        }
    }
}
