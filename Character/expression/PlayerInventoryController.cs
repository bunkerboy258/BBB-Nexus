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

        private int _currentSlotIndex = -1;

        // 配置槽位和实例槽位的 key 映射
        private readonly string[] _configSlotKeys = new[] { "config:weapon1", "config:weapon2", "config:weapon3", "config:weapon4", "config:weapon5" };
        private const string InstanceMainhandKey = "instance:mainhand";
        private const string InstanceOffhandKey = "instance:offhand";

        public PlayerInventoryController(BBBCharacterController player)
        {
            _player = player;
            _data = player.RuntimeData;
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

        // 尝试切换指定数字槽位对应的主手装备。
        // 新模式：复制配置槽位的载荷到实例槽位，不是 swap
        private void TryEquipSlot(int slotIndex)
        {
            if (_player == null) return;
            if (slotIndex < 0 || slotIndex >= 5) return;

            var configKey = _configSlotKeys[slotIndex];

            // 检查配置槽位是否启用
            if (!IsConfigSlotEnabled(configKey))
            {
                ConsumeHotbarKey(slotIndex);
                return;
            }

            // 检查实例槽位是否启用
            if (!IsInstanceSlotEnabled(InstanceMainhandKey))
            {
                ConsumeHotbarKey(slotIndex);
                return;
            }

            var service = _player.EquipmentService;
            if (service == null)
            {
                Debug.LogError("[PlayerInventoryController] EquipmentService 未配置");
                ConsumeHotbarKey(slotIndex);
                return;
            }

            // 同一槽位再按 = 卸下
            if (_currentSlotIndex == slotIndex)
            {
                Unequip();
                ConsumeHotbarKey(slotIndex);
                return;
            }

            // 从配置槽位获取装备 SO
            var itemSO = service.GetEquippedSO(configKey);
            if (itemSO == null)
            {
                // 配置槽位为空，不切换
                ConsumeHotbarKey(slotIndex);
                return;
            }

            // 切换前卸除旧武器的 VirtualOtherSlot 联动
            HandleVirtualOtherSlotOnUnequip();

            // 复制到实例槽位（主手）- 配置槽位保留原装备
            if (service.TrySetEquipSO(InstanceMainhandKey, itemSO))
            {
                // 实例化主手装备
                var mainhandInstance = CreateAndEquipInstance(itemSO, EquipmentSlot.MainHand);
                if (mainhandInstance != null)
                {
                    _player.RuntimeData.CurrentItem = mainhandInstance;
                    _currentSlotIndex = slotIndex;

                    // 处理 VirtualOtherSlot 联动
                    HandleVirtualOtherSlotOnEquip(itemSO);
                }
            }

            ConsumeHotbarKey(slotIndex);
        }

        /// <summary>
        /// 处理 VirtualOtherSlot 联动装备（开局恢复时也会调用）
        /// </summary>
        public void HandleVirtualOtherSlotOnEquip(EquippableItemSO mainhandSO)
        {
            if (!mainhandSO.VirtualOtherSlot.Enabled) return;

            var linkedItem = mainhandSO.VirtualOtherSlot.LinkedItem;
            if (linkedItem == null) return;

            var targetSlot = mainhandSO.VirtualOtherSlot.TargetSlot;
            if (targetSlot != EquipmentSlot.OffHand) return; // 目前只支持副手联动

            var service = _player.EquipmentService;
            if (service == null) return;

            // 检查副手槽位是否启用
            if (!IsInstanceSlotEnabled(InstanceOffhandKey)) return;

            // 检查副手是否已有装备，如果有则先记录（用于后续恢复）
            var currentOffhandSO = service.GetEquippedSO(InstanceOffhandKey);
            if (currentOffhandSO != null)
            {
                // 将当前副手存到隐藏槽位
                service.TrySetEquipSO("hide:offhand", currentOffhandSO);
            }

            // 装备联动的副手武器
            if (service.TrySetEquipSO(InstanceOffhandKey, linkedItem))
            {
                CreateAndEquipInstance(linkedItem, EquipmentSlot.OffHand);
            }
        }

        /// <summary>
        /// 创建并装备实例
        /// </summary>
        private ItemInstance CreateAndEquipInstance(EquippableItemSO itemSO, EquipmentSlot slot)
        {
            // 创建实例
            var instance = new ItemInstance(itemSO, null, 1);

            // 通过 EquipmentDriver 实例化
            if (slot == EquipmentSlot.MainHand)
            {
                _player.EquipmentDriver.EquipItemToSlot(instance, EquipmentSlot.MainHand);
            }
            else if (slot == EquipmentSlot.OffHand)
            {
                _player.EquipmentDriver.EquipItemToSlot(instance, EquipmentSlot.OffHand);
            }

            return instance;
        }

        // 检查槽位是否启用
        private bool IsConfigSlotEnabled(string key)
        {
            return _player?.Config?.SlotRegistry?.IsConfigSlotEnabled(key) ?? true;
        }

        private bool IsInstanceSlotEnabled(string key)
        {
            return _player?.Config?.SlotRegistry?.IsInstanceSlotEnabled(key) ?? true;
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

            // 检查实例槽位是否启用
            if (!IsInstanceSlotEnabled(InstanceMainhandKey)) return;

            var service = _player.EquipmentService;

            // 处理 VirtualOtherSlot 联动卸下
            HandleVirtualOtherSlotOnUnequip();

            if (service != null)
            {
                service.TryRemoveEquipped(InstanceMainhandKey);
            }

            // 通过 EquipmentDriver 卸下
            _player.EquipmentDriver.UnequipMainhand();
            _player.RuntimeData.CurrentItem = null;
            _currentSlotIndex = -1;
        }

        /// <summary>
        /// 处理 VirtualOtherSlot 联动卸下
        /// </summary>
        private void HandleVirtualOtherSlotOnUnequip()
        {
            var service = _player.EquipmentService;
            if (service == null) return;

            // 从当前装备实例获取 SO（避免 MetaLib 查询）
            var currentItem = _player.RuntimeData.CurrentItem;
            if (currentItem?.BaseData == null) return;

            var mainhandSO = currentItem.BaseData as EquippableItemSO;

            // 检查是否有 VirtualOtherSlot 联动
            if (mainhandSO == null || !mainhandSO.VirtualOtherSlot.Enabled) return;

            var targetSlot = mainhandSO.VirtualOtherSlot.TargetSlot;
            if (targetSlot != EquipmentSlot.OffHand) return;

            // 卸下副手
            if (IsInstanceSlotEnabled(InstanceOffhandKey))
            {
                service.TryRemoveEquipped(InstanceOffhandKey);
                _player.EquipmentDriver.UnequipOffhand();

                // 尝试恢复隐藏槽位的装备
                var hiddenSO = service.GetEquippedSO("hide:offhand");
                if (hiddenSO != null)
                {
                    service.TrySetEquipSO(InstanceOffhandKey, hiddenSO);
                    CreateAndEquipInstance(hiddenSO, EquipmentSlot.OffHand);
                    service.TryRemoveEquipped("hide:offhand");
                }
            }
        }

        private void OnEquipmentChanged()
        {
            // 只更新索引，不触发装备操作（避免递归）
            // 装备操作应由调用方（UI/热键）直接完成
            if (_player == null) return;

            var service = _player.EquipmentService;
            if (service == null)
            {
                _currentSlotIndex = -1;
                return;
            }

            // 获取当前主手实例槽位的装备SO
            var mainhandSO = service.GetEquippedSO(InstanceMainhandKey);
            if (mainhandSO == null)
            {
                _currentSlotIndex = -1;
                return;
            }

            // 更新当前槽位索引（用于热键逻辑）
            for (int i = 0; i < _configSlotKeys.Length; i++)
            {
                var configSO = service.GetEquippedSO(_configSlotKeys[i]);
                if (mainhandSO == configSO)
                {
                    _currentSlotIndex = i;
                    return;
                }
            }

            _currentSlotIndex = -1;
        }
    }
}
