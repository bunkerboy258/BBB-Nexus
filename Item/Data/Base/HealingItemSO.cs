using UnityEngine;

namespace BBBNexus
{
    [CreateAssetMenu(fileName = "New HealingItemSO", menuName = "BBBNexus/Items/Healing Item")]
    public class HealingItemSO : ItemDefinitionSO
    {
        [Header("--- 治疗配置 ---")]
        [Tooltip("单次使用回复的生命值。")]
        public float HealAmount = 35f;

        [Tooltip("满血时是否允许消耗。通常血药不允许。")]
        public bool AllowUseAtFullHealth = false;

        [Header("--- 提示文案 ---")]
        public string EmptyMessageTitle = "没有血药";

        [TextArea(2, 4)]
        public string EmptyMessageBody = "已经没有可用的血药了。";

        public string FullHealthMessageTitle = "生命值已满";

        [TextArea(2, 4)]
        public string FullHealthMessageBody = "现在还不需要使用血药。";
    }
}
