using UnityEngine;
using Items.Core;
using Items.Data;
using Characters.Player.Data;
using System.Linq;//FirstOrDefault()

namespace Characters.Player.Core
{
    public class PlayerInventoryController
    {
        private PlayerController _player;
        public InventorySystem MainInventory { get; private set; }
        private InventoryItem[] _hotbarSlots = new InventoryItem[5];
        private int _currentSlotIndex = -1;

        public PlayerInventoryController(PlayerController player)
        {
            _player = player;
            MainInventory = new InventorySystem(20);
        }

        public void Initialize()
        {
            // ========== 核心修改：绑定 InputReader 数字键回调 ==========
            if (_player.InputReader != null)
            {
                // 数字1 → 快捷栏0，触发 TryEquipSlot(0)
                _player.InputReader.OnNumber1Pressed += () => TryEquipSlot(0);
                // 数字2 → 快捷栏1
                _player.InputReader.OnNumber2Pressed += () => TryEquipSlot(1);
                // 数字3 → 快捷栏2
                _player.InputReader.OnNumber3Pressed += () => TryEquipSlot(2);
                // 数字4 → 快捷栏3
                _player.InputReader.OnNumber4Pressed += () => TryEquipSlot(3);
                // 数字5 → 快捷栏4
                _player.InputReader.OnNumber5Pressed += () => TryEquipSlot(4);
            }

            // 绑定装备变更回调
            _player.OnEquipmentChanged += OnEquipmentChanged;
        }

        // ========== 替换析构函数：手动 Dispose 方法（解绑所有回调） ==========
        /// <summary>
        /// 手动调用销毁，解绑所有回调（由 PlayerController 的 OnDestroy 调用）
        /// </summary>
        public void Dispose()
        {
            if (_player == null) return;

            // 解绑装备变更回调
            _player.OnEquipmentChanged -= OnEquipmentChanged;

            // 解绑 InputReader 数字键回调（防止内存泄漏）
            if (_player.InputReader != null)
            {
                _player.InputReader.OnNumber1Pressed -= () => TryEquipSlot(0);
                _player.InputReader.OnNumber2Pressed -= () => TryEquipSlot(1);
                _player.InputReader.OnNumber3Pressed -= () => TryEquipSlot(2);
                _player.InputReader.OnNumber4Pressed -= () => TryEquipSlot(3);
                _player.InputReader.OnNumber5Pressed -= () => TryEquipSlot(4);
            }

            // 清空引用，加速 GC
            _player = null;
        }

        // --- AssignItemToSlot 逻辑（保持不变，补充完整方便使用） ---
        public void AssignItemToSlot(int slotIndex, ItemDefinitionSO itemDef)
        {
            if (slotIndex < 0 || slotIndex >= 5) return;

            // 确保物品在背包中（调试用：不在则自动添加）
            if (!MainInventory.HasItem(itemDef))
            {
                MainInventory.AddItem(itemDef, 1);
            }

            // 找到背包中对应的物品实例
            var itemInBag = MainInventory.GetAllItems().FirstOrDefault(i => i.Definition == itemDef);

            // 放入快捷栏
            _hotbarSlots[slotIndex] = itemInBag;

            Debug.Log($"快捷栏 [{slotIndex + 1}] 已绑定: {itemDef.Name}");
        }

        // --- 修改后的装备逻辑（核心） ---
        private void TryEquipSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= 5) return;

            // 1. 重复按下当前槽位 → 卸载
            if (_currentSlotIndex == slotIndex)
            {
                Unequip();
                return;
            }

            InventoryItem targetItem = _hotbarSlots[slotIndex];

            // 2. 如果槽位有可装备物品 → 设置装备意图
            if (targetItem != null && targetItem.IsValid && targetItem.Definition is EquippableItemSO)
            {
                Debug.Log($"[Inventory] 意图切换 -> {targetItem.Definition.Name}");
                // 设置意图（核心逻辑：只修改数据，不直接调用管理器）
                _player.RuntimeData.DesiredItemDefinition = targetItem.Definition;
            }
            else
            {
                Debug.Log($"槽位 {slotIndex + 1} 无效（为空或不可装备）");
            }
        }

        private void Unequip()
        {
            Debug.Log("[Inventory] 意图卸载");
            // 设置卸载意图
            _player.RuntimeData.DesiredItemDefinition = null;
        }

        // --- 同步逻辑（保持不变） ---
        private void OnEquipmentChanged()
        {
            var currentDef = _player.RuntimeData.CurrentEquipment.Definition;

            if (currentDef == null)
            {
                _currentSlotIndex = -1;
                return;
            }

            // 遍历快捷栏，匹配当前装备的槽位索引
            for (int i = 0; i < 5; i++)
            {
                if (_hotbarSlots[i] != null && _hotbarSlots[i].Definition == currentDef)
                {
                    _currentSlotIndex = i;
                    return;
                }
            }

            // 装备不在快捷栏中，索引设为 -1
            _currentSlotIndex = -1;
        }
    }
}