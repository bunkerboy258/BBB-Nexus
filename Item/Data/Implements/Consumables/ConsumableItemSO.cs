using UnityEngine;

namespace Items.Data
{
    [CreateAssetMenu(fileName = "NewConsumable", menuName = "Items/Consumable")]
    public class ConsumableItemSO : ItemDefinitionSO
    {
        [Header("消耗效果")]
        public float HealthRestore = 0f;
        public float StaminaRestore = 0f;

        public void Use(GameObject user)
        {
            // 在这里实现通用的使用逻辑，或者委托给 Ability 系统
            Debug.Log($"{user.name} 使用了 {Name}");
        }
    }
}
