using UnityEngine;

namespace BBBNexus
{
    public enum DamageHitZoneType
    {
        Default = 0,
        Head = 1,
        Torso = 2,
        Arm = 3,
        Leg = 4,
    }

    /// <summary>
    /// 挂在受击碰撞体上，为远程命中提供稳定的部位语义。
    /// </summary>
    public sealed class DamageHitZone : MonoBehaviour
    {
        [SerializeField] private DamageHitZoneType _zone = DamageHitZoneType.Default;

        public DamageHitZoneType Zone => _zone;
    }
}
