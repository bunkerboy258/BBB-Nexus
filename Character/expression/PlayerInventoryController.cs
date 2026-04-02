using System;
using System.Linq;
using UnityEngine;

namespace BBBNexus
{
    // 物品栏管理器
    public class PlayerInventoryController
    {
        private BBBCharacterController _player;
        private PlayerRuntimeData _data; 

        public InventorySystem MainInventory { get; private set; }
        public InventorySystem HotbarInventory { get; private set; }

        private int _currentSlotIndex = -1;

        public PlayerInventoryController(BBBCharacterController player)
        {
            _player = player;
            _data = player.RuntimeData; 
            MainInventory = new InventorySystem(20);
            HotbarInventory = new InventorySystem(5);
        }

        public void Initialize()
        {
            if (_player != null)
            {
                _player.OnEquipmentChanged += OnEquipmentChanged;
            }
        }

        public void Dispose()
        {
            if (_player == null) return;
            _player.OnEquipmentChanged -= OnEquipmentChanged;
            _player = null;
        }

        public void Update()
        {
            if (_data == null) return;
            // BlockInventory 只阻止切换新装备，不强制卸载当前装备
            if (_data.Arbitration.BlockInventory) return;

            if (_data.WantToEquipSlotIndex != -1)
            {
                TryEquipSlot(_data.WantToEquipSlotIndex);

                _data.WantToEquipSlotIndex = -1;
            }
        }

        public void AssignItemToSlot(int slotIndex, ItemInstance itemInstance)
        {
            if (slotIndex < 0 || slotIndex >= 5) return;
            if (itemInstance == null) return;

            var oldItem = HotbarInventory.SetAt(slotIndex, itemInstance);
            if (oldItem != null)
            {
                MainInventory.TryAdd(oldItem);
            }
            //Debug.Log($"[Inventory] 快捷栏[{slotIndex + 1}] 绑定: {itemInstance.BaseData.DisplayName}");
        }

        public bool MoveToHotbar(InventorySystem source, int sourceSlot, int hotbarSlot)
        {
            if (source == null || sourceSlot < 0 || hotbarSlot < 0 || hotbarSlot >= 5) return false;

            var itemToMove = source.RemoveAt(sourceSlot);
            if (itemToMove == null) return false;

            var oldItem = HotbarInventory.SetAt(hotbarSlot, itemToMove);
            if (oldItem != null)
            {
                source.SetAt(sourceSlot, oldItem);
            }
            return true;
        }

        public bool MoveToInventory(InventorySystem source, int sourceSlot, int inventorySlot)
        {
            if (source == null || sourceSlot < 0 || inventorySlot < 0 || inventorySlot >= 20) return false;

            var itemToMove = source.RemoveAt(sourceSlot);
            if (itemToMove == null) return false;

            var oldItem = MainInventory.SetAt(inventorySlot, itemToMove);
            if (oldItem != null)
            {
                source.SetAt(sourceSlot, oldItem);
            }
            return true;
        }

        // 尝试切换指定数字槽位对应的主手装备。
        // 数字槽位语义默认只作用于 MainHand；
        // OffHand 以及更细粒度槽位应由背包/装备界面显式处理。
        private void TryEquipSlot(int slotIndex)
        {
            if (_player == null) return;
            if (slotIndex < 0 || slotIndex >= 5) return;

            if (_currentSlotIndex == slotIndex)
            {
                Unequip();

                // 消费对应的数字键输入 防止同帧重复触发
                ConsumeHotbarKey(slotIndex);
                return;
            }

            if (EquipmentPackVfs.SwapMainHandWithMainSlot(slotIndex + 1, _player))
            {
                if (!EquipmentPackVfs.TryGetOtherSlotData(EquipmentSlot.MainHand, out var mainhandData, _player) ||
                    string.IsNullOrWhiteSpace(mainhandData?.Id))
                {
                    ConsumeHotbarKey(slotIndex);
                    return;
                }

                var instance = EquipmentManager.EquipById(_player, mainhandData.Id, EquipmentSlot.MainHand, instanceId: mainhandData.InstanceId);
                if (instance != null)
                {
                    _player.RuntimeData.CurrentItem = instance;
                    _currentSlotIndex = slotIndex;
                }

                ConsumeHotbarKey(slotIndex);
                return;
            }

            ConsumeHotbarKey(slotIndex);
        }

        private void ConsumeHotbarKey(int slotIndex)
        {
            if (_player == null || _player.InputPipeline == null) return;
            switch (slotIndex)
            {
                case 0: _player.InputPipeline.ConsumeNumber1Pressed(); break;
                case 1: _player.InputPipeline.ConsumeNumber2Pressed(); break;
                case 2: _player.InputPipeline.ConsumeNumber3Pressed(); break;
                case 3: _player.InputPipeline.ConsumeNumber4Pressed(); break;
                case 4: _player.InputPipeline.ConsumeNumber5Pressed(); break;
            }
        }

        private void Unequip()
        {
            if (_player == null) return;
            //Debug.Log("[Inventory] 意图卸载");
            EquipmentPackVfs.ReturnMainHandToOccupiedMainSlot(_player);
            EquipmentManager.Unequip(_player, EquipmentSlot.MainHand);
            _player.RuntimeData.CurrentItem = null;
            _currentSlotIndex = -1;
        }

        private void OnEquipmentChanged()
        {
            if (_player == null) return;

            var current = _player.RuntimeData.CurrentItem;
            if (current == null)
            {
                _currentSlotIndex = -1;
                return;
            }

            if (EquipmentPackVfs.TryGetOccupiedMainSlotIndex(out var occupiedIndex, _player))
            {
                _currentSlotIndex = occupiedIndex - 1;
                return;
            }
            _currentSlotIndex = -1;
        }
    }
}
