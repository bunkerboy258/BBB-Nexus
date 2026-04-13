using UnityEngine;

namespace BBBNexus
{
    public enum DamageDeliveryKind
    {
        Unspecified = 0,
        Melee = 1,
        Ranged = 2,
    }

    public struct DamageRequest
    {
        public float Amount;
        /// <summary>世界坐标命中点，用于伤害数字和替身生成位置</summary>
        public Vector3 HitPoint;
        /// <summary>攻击者 GameObject（用于替身追踪、格挡反击）</summary>
        public GameObject Attacker;
        /// <summary>具体武器的 Transform（替身贴靠目标）</summary>
        public Transform WeaponTransform;
        /// <summary>伤害来源类型。用于盾牌、友伤等统一判定。</summary>
        public DamageDeliveryKind DeliveryKind;
        /// <summary>是否使用盾牌方向角判定。近战=true，远程=false。</summary>
        public bool UsesShieldBlockArc;
        /// <summary>命中盾牌时是否对攻击者施加反震/负面效果。</summary>
        public bool TriggersShieldBlockedEffect;

        public bool IsMelee => DeliveryKind == DamageDeliveryKind.Melee;
        public bool IsRanged => DeliveryKind == DamageDeliveryKind.Ranged;

        public DamageRequest(float amount)
        {
            Amount          = amount;
            HitPoint        = Vector3.zero;
            Attacker        = null;
            WeaponTransform = null;
            DeliveryKind = DamageDeliveryKind.Unspecified;
            UsesShieldBlockArc = true;
            TriggersShieldBlockedEffect = true;
        }

        public DamageRequest(float amount, Vector3 hitPoint)
        {
            Amount          = amount;
            HitPoint        = hitPoint;
            Attacker        = null;
            WeaponTransform = null;
            DeliveryKind = DamageDeliveryKind.Unspecified;
            UsesShieldBlockArc = true;
            TriggersShieldBlockedEffect = true;
        }

        public DamageRequest(float amount, Vector3 hitPoint, GameObject attacker, Transform weaponTransform)
            : this(amount, hitPoint, attacker, weaponTransform, DamageDeliveryKind.Unspecified, true, true)
        {
        }

        public DamageRequest(
            float amount,
            Vector3 hitPoint,
            GameObject attacker,
            Transform weaponTransform,
            DamageDeliveryKind deliveryKind,
            bool usesShieldBlockArc,
            bool triggersShieldBlockedEffect)
        {
            Amount          = amount;
            HitPoint        = hitPoint;
            Attacker        = attacker;
            WeaponTransform = weaponTransform;
            DeliveryKind = deliveryKind;
            UsesShieldBlockArc = usesShieldBlockArc;
            TriggersShieldBlockedEffect = triggersShieldBlockedEffect;
        }

        /// <summary>
        /// 尝试解析这次伤害对应的攻击者控制器。
        /// 优先使用显式注入的 Attacker，其次回退到 WeaponTransform 所在层级。
        /// </summary>
        public BBBCharacterController ResolveAttackerController()
        {
            if (Attacker != null)
            {
                var controller = Attacker.GetComponent<BBBCharacterController>();
                if (controller != null)
                    return controller;

                controller = Attacker.GetComponentInParent<BBBCharacterController>();
                if (controller != null)
                    return controller;
            }

            if (WeaponTransform != null)
                return WeaponTransform.GetComponentInParent<BBBCharacterController>();

            return null;
        }

        /// <summary>
        /// 命中方向/朝向判定优先使用攻击者控制器，其次回退到显式攻击者或武器挂点。
        /// </summary>
        public Transform ResolveAttackerTransform()
        {
            var controller = ResolveAttackerController();
            if (controller != null)
                return controller.transform;

            if (Attacker != null)
                return Attacker.transform;

            return WeaponTransform;
        }
    }

    public interface IDamageable
    {
        bool RequestDamage(in DamageRequest request);
    }
}
