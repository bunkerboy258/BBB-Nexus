using UnityEngine;

namespace BBBNexus
{
    /// <summary>
    /// Static entry point for equipping and resolving runtime item instances.
    /// Keeps BBB's existing EquipmentDriver as the execution backend.
    /// </summary>
    public static class EquipmentManager
    {
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

            var instance = CreateInstance(itemSo, amount);
            if (instance == null)
            {
                return null;
            }

            player.EquipmentDriver.EquipItemToSlot(instance, slot);

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
    }
}
