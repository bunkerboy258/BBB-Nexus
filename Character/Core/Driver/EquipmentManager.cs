using UnityEngine;

namespace BBBNexus
{
    /// <summary>
    /// Static entry point for equipping and resolving runtime item instances.
    /// Keeps BBB's existing EquipmentDriver as the execution backend.
    /// </summary>
    public static class EquipmentManager
    {
        private readonly struct VirtualLinkPlan
        {
            public VirtualLinkPlan(EquipmentSlot targetSlot, string itemId)
            {
                TargetSlot = targetSlot;
                ItemId = itemId;
            }

            public EquipmentSlot TargetSlot { get; }
            public string ItemId { get; }
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

        public static ItemInstance CreateInstance(EquippableItemSO itemSo, int amount = 1)
        {
            if (itemSo == null)
            {
                Debug.LogError("[EquipmentManager] Cannot create item instance from null item SO.");
                return null;
            }

            return new ItemInstance(itemSo, amount);
        }

        public static ItemInstance EquipById(BBBCharacterController player, string globalId, EquipmentSlot slot, int amount = 1)
        {
            var itemSo = ResolveItemSO(globalId);
            if (itemSo == null)
            {
                return null;
            }

            return Equip(player, itemSo, slot, amount);
        }

        public static ItemInstance Equip(BBBCharacterController player, EquippableItemSO itemSo, EquipmentSlot slot, int amount = 1)
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

            var instance = CreateInstance(itemSo, amount);
            if (instance == null)
            {
                return null;
            }

            player.EquipmentDriver.EquipItemToSlot(instance, slot);

            if (mainhandLinkPlan.HasValue)
            {
                var plan = mainhandLinkPlan.Value;
                EquipById(player, plan.ItemId, plan.TargetSlot);
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

            if (EquipmentPackVfs.TryGetOtherSlotItemId(targetSlot, out var occupiedItemId, player) &&
                !string.IsNullOrWhiteSpace(occupiedItemId) &&
                !string.Equals(occupiedItemId, linkedItemId, System.StringComparison.Ordinal))
            {
                EquipmentPackVfs.SetHideSlot(targetSlot, occupiedItemId, player);
                EquipmentPackVfs.ClearOtherSlot(targetSlot, player);
            }

            if (EquipmentPackVfs.TryTakeVirtualSlotItemId(mainhandSo.name, targetSlot, out var virtualItemId, player) &&
                !string.IsNullOrWhiteSpace(virtualItemId))
            {
                linkedItemId = virtualItemId;
            }

            EquipmentPackVfs.SetOtherSlot(targetSlot, linkedItemId, player);
            return new VirtualLinkPlan(targetSlot, linkedItemId);
        }

        private static void ReleaseMainhandLinkedOtherSlot(BBBCharacterController player)
        {
            var currentMainhandSo = player?.EquipmentDriver?.MainhandItemData;
            if (!TryGetVirtualOtherSlotLink(currentMainhandSo, out var targetSlot, out _))
            {
                return;
            }

            if (EquipmentPackVfs.TryGetOtherSlotItemId(targetSlot, out var equippedLinkedItemId, player) &&
                !string.IsNullOrWhiteSpace(equippedLinkedItemId))
            {
                EquipmentPackVfs.SetVirtualSlot(currentMainhandSo.name, targetSlot, equippedLinkedItemId, player);
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

            if (EquipmentPackVfs.TryGetHideSlotItemId(targetSlot, out var hiddenItemId, player) &&
                !string.IsNullOrWhiteSpace(hiddenItemId))
            {
                EquipmentPackVfs.SetOtherSlot(targetSlot, hiddenItemId, player);
                EquipmentPackVfs.ClearHideSlot(targetSlot, player);
                EquipById(player, hiddenItemId, targetSlot);
            }
        }

        private static bool TryGetVirtualOtherSlotLink(
            EquippableItemSO itemSo,
            out EquipmentSlot targetSlot,
            out string linkedItemId)
        {
            targetSlot = EquipmentSlot.None;
            linkedItemId = null;

            if (itemSo == null || !itemSo.VirtualOtherSlot.Enabled)
            {
                return false;
            }

            targetSlot = itemSo.VirtualOtherSlot.TargetSlot;
            linkedItemId = itemSo.VirtualOtherSlot.ItemId;
            if (targetSlot == EquipmentSlot.None || targetSlot == EquipmentSlot.MainHand)
            {
                Debug.LogWarning($"[EquipmentManager] Invalid virtual otherslot target on '{itemSo.name}'.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(linkedItemId))
            {
                Debug.LogWarning($"[EquipmentManager] Missing virtual otherslot item id on '{itemSo.name}'.");
                return false;
            }

            return true;
        }
    }
}
