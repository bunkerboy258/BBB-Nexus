using UnityEngine;
using Items.Core;
using Items.Data;
using Characters.Player.Data;

namespace Characters.Player.Processing
{
    public class EquipIntentProcessor
    {
        private PlayerController _player;
        private InventorySystem MainInventory;
        private InventoryItem[] _hotbarSlots = new InventoryItem[5];

        public EquipIntentProcessor(PlayerController player)
        {
            _player = player;
            MainInventory = _player.InventoryController.MainInventory;

            _player.InputReader.OnNumber1Pressed += () => TryEquipSlot(0);
            _player.InputReader.OnNumber2Pressed += () => TryEquipSlot(1);
            _player.InputReader.OnNumber3Pressed += () => TryEquipSlot(2);
            _player.InputReader.OnNumber4Pressed += () => TryEquipSlot(3);
            _player.InputReader.OnNumber5Pressed += () => TryEquipSlot(4);
        }

        public void Update()
        {

        }
        public void AssignItemToSlot(int index, ItemDefinitionSO item)
        {
            if (index >= 0 && index < 5) _hotbarSlots[index] = new InventoryItem(item);
        }

        private void TryEquipSlot(int index)
        {
            Debug.Log($"[Inventory] 尝试装备槽位 {index + 1}");
            var targetItem = _hotbarSlots[index];
            var currentDef = _player.RuntimeData.DesiredItemDefinition;

            // 1. 如果按的是当前这把 -> 卸载 (切换到空手)
            if (targetItem != null && currentDef == targetItem.Definition)
            {
                Debug.Log($"[Inventory] 收起武器: {currentDef.Name}");
                _player.RuntimeData.DesiredItemDefinition = null;
            }
            // 2. 如果按的是新的 -> 切换意图
            else if (targetItem != null)
            {   
                Debug.Log($"[Inventory] 切换意图 -> {targetItem.Definition.Name}");
                _player.RuntimeData.DesiredItemDefinition = targetItem.Definition;
            }
            // 3. 如果槽位是空的 -> 也可以视为卸载
            else
            {
                _player.RuntimeData.DesiredItemDefinition = null;
            }
        }
    }
}
