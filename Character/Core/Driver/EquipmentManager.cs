using UnityEngine;

namespace BBBNexus
{
    /*/// <summary>
    /// Static entry point for equipping and resolving runtime item instances.
    /// Keeps BBB's existing EquipmentDriver as the execution backend.
    ///
    /// 已废弃：VirtualOtherSlot 逻辑已迁移到 PlayerInventoryController，
    /// 请直接使用 PlayerInventoryController 和 EquipmentDriver 进行装备操作。
    /// </summary>
    [System.Obsolete("EquipmentManager 已废弃。请使用 PlayerInventoryController + EquipmentService + EquipmentDriver 替代。", false)]
    public static class EquipmentManager
    {
        private readonly struct VirtualLinkPlan
        {
            public VirtualLinkPlan(EquipmentSlot targetSlot, string itemId, string instanceId)
            {
                TargetSlot = targetSlot;
                ItemId = itemId;
                InstanceId = instanceId;
            }

            public EquipmentSlot TargetSlot { get; }
            public string ItemId { get; }
            public string InstanceId { get; }
        }

        public static EquippableItemSO ResolveItemSO(string globalId)
        {
            if (string.IsNullOrWhiteSpace(globalId))
            {
                Debug.LogError("[EquipmentManager] Global item id is empty.");
                return null;
            }

            var item = MetaLib.GetObject<EquippableItemSO>(globalId);
            if (item == null)
            {
                Debug.LogError($"[EquipmentManager] Failed to resolve EquippableItemSO from MetaLib id '{globalId}'.");
            }

            return item;
        }

        public static ItemInstance CreateInstance(EquippableItemSO itemSo, int amount = 1, string instanceId = null)
        {
            if (itemSo == null)
            {
                Debug.LogError("[EquipmentManager] Cannot create item instance from null item SO.");
                return null;
            }

            return new ItemInstance(itemSo, instanceId, amount);
        }

        public static ItemInstance EquipById(BBBCharacterController player, string globalId, EquipmentSlot slot, int amount = 1, string instanceId = null)
        {
            var itemSo = ResolveItemSO(globalId);
            if (itemSo == null)
            {
                return null;
            }

            return Equip(player, itemSo, slot, amount, instanceId);
        }

        public static ItemInstance Equip(BBBCharacterController player, EquippableItemSO itemSo, EquipmentSlot slot, int amount = 1, string instanceId = null)
        {
            if (player == null)
            {
                Debug.LogError("[EquipmentManager] Player is null.");
                return null;
            }

            if (player.EquipmentDriver == null || player.RuntimeData == null)
            {
                Debug.LogError("[EquipmentManager] Player equipment systems are not initialized.");
                return null;
            }

            if (itemSo == null)
            {
                Debug.LogError("[EquipmentManager] Item SO is null.");
                return null;
            }

            if (slot == EquipmentSlot.None)
            {
                Debug.LogError("[EquipmentManager] Cannot equip item to EquipmentSlot.None.");
                return null;
            }

            VirtualLinkPlan? mainhandLinkPlan = null;
            if (slot == EquipmentSlot.MainHand)
            {
                ReleaseMainhandLinkedOtherSlot(player);
                mainhandLinkPlan = PrepareMainhandLinkedOtherSlot(player, itemSo);
            }

            var instance = CreateInstance(itemSo, amount, instanceId);
            if (instance == null)
            {
                return null;
            }

            player.EquipmentDriver.EquipItemToSlot(instance, slot);

            if (mainhandLinkPlan.HasValue)
            {
                var plan = mainhandLinkPlan.Value;
                EquipById(player, plan.ItemId, plan.TargetSlot, instanceId: plan.InstanceId);
            }

            if (slot == EquipmentSlot.MainHand)
            {
                player.RuntimeData.CurrentItem = instance;
            }

            return instance;
        }

        public static void Unequip(BBBCharacterController player, EquipmentSlot slot)
        {
            if (player == null || player.EquipmentDriver == null || player.RuntimeData == null)
            {
                return;
            }

            switch (slot)
            {
                case EquipmentSlot.MainHand:
                    ReleaseMainhandLinkedOtherSlot(player);
                    var removedMainhand = player.RuntimeData.MainhandItem;
                    player.EquipmentDriver.UnequipMainhand();
                    if (player.RuntimeData.CurrentItem == removedMainhand)
                    {
                        player.RuntimeData.CurrentItem = null;
                    }
                    break;
                case EquipmentSlot.OffHand:
                    player.EquipmentDriver.UnequipOffhand();
                    break;
                case EquipmentSlot.None:
                default:
                    player.EquipmentDriver.UnequipCurrentItem();
                    player.RuntimeData.CurrentItem = null;
                    break;
            }
        }

        private static VirtualLinkPlan? PrepareMainhandLinkedOtherSlot(BBBCharacterController player, EquippableItemSO mainhandSo)
        {
            if (!TryGetVirtualOtherSlotLink(mainhandSo, out var targetSlot, out var linkedItemId))
            {
                return null;
            }

            if (EquipmentPackVfs.TryGetOtherSlotData(targetSlot, out var occupiedData, player) &&
                !string.IsNullOrWhiteSpace(occupiedData?.Id) &&
                !string.Equals(occupiedData.Id, linkedItemId, System.StringComparison.Ordinal))
            {
                EquipmentPackVfs.SetHideSlot(targetSlot, occupiedData.Id, player, occupiedData.InstanceId);
                EquipmentPackVfs.ClearOtherSlot(targetSlot, player);
            }

            if (EquipmentPackVfs.TryTakeVirtualSlotData(mainhandSo.name, targetSlot, out var virtualData, player) &&
                !string.IsNullOrWhiteSpace(virtualData?.Id))
            {
                linkedItemId = virtualData.Id;
                EquipmentPackVfs.SetOtherSlot(targetSlot, linkedItemId, player, virtualData.InstanceId);
                return new VirtualLinkPlan(targetSlot, linkedItemId, virtualData.InstanceId);
            }

            EquipmentPackVfs.SetOtherSlot(targetSlot, linkedItemId, player);
            return new VirtualLinkPlan(targetSlot, linkedItemId, null);
        }

        private static void ReleaseMainhandLinkedOtherSlot(BBBCharacterController player)
        {
            var currentMainhandSo = player?.EquipmentDriver?.MainhandItemData;
            if (!TryGetVirtualOtherSlotLink(currentMainhandSo, out var targetSlot, out _))
            {
                return;
            }

            if (EquipmentPackVfs.TryGetOtherSlotData(targetSlot, out var equippedLinkedData, player) &&
                !string.IsNullOrWhiteSpace(equippedLinkedData?.Id))
            {
                EquipmentPackVfs.SetVirtualSlot(currentMainhandSo.name, targetSlot, equippedLinkedData.Id, player, equippedLinkedData.InstanceId);
                EquipmentPackVfs.ClearOtherSlot(targetSlot, player);
            }

            switch (targetSlot)
            {
                case EquipmentSlot.OffHand:
                    player.EquipmentDriver.UnequipOffhand();
                    break;
                case EquipmentSlot.MainHand:
                    player.EquipmentDriver.UnequipMainhand();
                    break;
                default:
                    player.EquipmentDriver.UnequipCurrentItem();
                    break;
            }

            if (EquipmentPackVfs.TryGetHideSlotData(targetSlot, out var hiddenData, player) &&
                !string.IsNullOrWhiteSpace(hiddenData?.Id))
            {
                EquipmentPackVfs.SetOtherSlot(targetSlot, hiddenData.Id, player, hiddenData.InstanceId);
                EquipmentPackVfs.ClearHideSlot(targetSlot, player);
                EquipById(player, hiddenData.Id, targetSlot, instanceId: hiddenData.InstanceId);
            }
        }

        private static bool TryGetVirtualOtherSlotLink(
            EquippableItemSO itemSo,
            out EquipmentSlot targetSlot,
            out EquippableItemSO linkedItem)
        {
            targetSlot = EquipmentSlot.None;
            linkedItem = null;

            if (itemSo == null || !itemSo.VirtualOtherSlot.Enabled)
            {
                return false;
            }

            targetSlot = itemSo.VirtualOtherSlot.TargetSlot;
            linkedItem = itemSo.VirtualOtherSlot.LinkedItem;
            if (targetSlot == EquipmentSlot.None || targetSlot == EquipmentSlot.MainHand)
            {
                Debug.LogWarning($"[EquipmentManager] Invalid virtual otherslot target on '{itemSo.name}'.");
                return false;
            }

            if (linkedItem == null)
            {
                Debug.LogWarning($"[EquipmentManager] Missing virtual otherslot item on '{itemSo.name}'.");
                return false;
            }

            return true;
        }
    }*/
}
