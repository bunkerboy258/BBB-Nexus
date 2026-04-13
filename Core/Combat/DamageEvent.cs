using UnityEngine;

namespace BBBNexus
{
    public interface IHealthBarTarget
    {
        Transform HealthBarTransform { get; }
        float CurrentHealthForBar { get; }
        float MaxHealthForBar { get; }
    }

    /// <summary>
    /// HealthArbiter 结算伤害后通过 PostSystem 广播的事件包喵~
    /// 事件名："OnDamaged"
    /// </summary>
    public class DamageEvent
    {
        /// <summary>受伤目标</summary>
        public IHealthBarTarget Target;
        /// <summary>本次伤害量</summary>
        public float Amount;
        /// <summary>结算后剩余血量</summary>
        public float RemainingHealth;
        /// <summary>命中世界坐标（来自 DamageRequest.HitPoint）</summary>
        public Vector3 HitPoint;
        /// <summary>受伤后是否死亡</summary>
        public bool IsFatal;
    }
}
