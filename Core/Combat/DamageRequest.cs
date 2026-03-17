using UnityEngine;

namespace BBBHe.Core.Combat
{
    public struct DamageRequest
    {
        public float Amount;
        public DamageRequest(float amount)
        {
            Amount = amount;
        }
    }

    public interface IDamageable
    {
        void RequestDamage(in DamageRequest request);
    }
}