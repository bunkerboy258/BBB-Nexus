using UnityEngine;

namespace Items.Data
{
    // 继承自 Equippable，因为它是可以拿在手里的
    [CreateAssetMenu(fileName = "NewDevice", menuName = "Items/Devices/Standard Device")]
    public class DeviceItemSO : EquippableItemSO
    {
        [Header("装置参数 (Device Stats)")]
        public float Cooldown = 0.1f; // 冷却时间 (射速)
        public float Range = 100f;    // 有效距离
        public float Power = 10f;     // 输出功率 (伤害)

        // 可以在这里加更多通用参数，或者继续派生子类
    }
}
