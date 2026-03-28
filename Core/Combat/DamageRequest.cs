using UnityEngine;

namespace BBBNexus
{
    public struct DamageRequest
    {
        public float Amount;
        /// <summary>世界坐标命中点，用于伤害数字和替身生成位置</summary>
        public Vector3 HitPoint;
        /// <summary>攻击者 GameObject（用于替身追踪、格挡反击）</summary>
        public GameObject Attacker;
        /// <summary>具体武器的 Transform（替身贴靠目标）</summary>
        public Transform WeaponTransform;

        public DamageRequest(float amount)
        {
            Amount          = amount;
            HitPoint        = Vector3.zero;
            Attacker        = null;
            WeaponTransform = null;
        }

        public DamageRequest(float amount, Vector3 hitPoint)
        {
            Amount          = amount;
            HitPoint        = hitPoint;
            Attacker        = null;
            WeaponTransform = null;
        }

        public DamageRequest(float amount, Vector3 hitPoint, GameObject attacker, Transform weaponTransform)
        {
            Amount          = amount;
            HitPoint        = hitPoint;
            Attacker        = attacker;
            WeaponTransform = weaponTransform;
        }
    }

    public interface IDamageable
    {
        void RequestDamage(in DamageRequest request);
    }
}