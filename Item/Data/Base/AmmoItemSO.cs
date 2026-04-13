using UnityEngine;

namespace BBBNexus
{
    [CreateAssetMenu(fileName = "New AmmoItemSO", menuName = "BBBNexus/Items/Ammo Item")]
    public class AmmoItemSO : ItemDefinitionSO
    {
        [Header("--- 弹药语义 ---")]
        [Tooltip("口径/分类备注。真正用于判定的仍然是 ItemID。")]
        public string AmmoTag;
    }
}
