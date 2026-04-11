/* REMOVED - old PackVfs layer
using System;
using System.Collections.Generic;
using NekoGraph;
using UnityEngine;

namespace BBBNexus
{
    public sealed class InventorySlotSnapshot
    {
        public string SlotName;
        public ItemData Data;
        public ItemDefinitionSO Definition;

        public bool IsEquippable => Definition is EquippableItemSO;
        public bool IsConsumable => Definition is HealingItemSO;
        public string DisplayName => Definition != null && !string.IsNullOrWhiteSpace(Definition.DisplayName)
            ? Definition.DisplayName
            : Data?.Id ?? string.Empty;
    }

    public sealed class InventorySnapshot
    {
        public List<InventorySlotSnapshot> Slots = new List<InventorySlotSnapshot>();
        public string MainHandItemId;
        public string OffHandItemId;
        public int OccupiedMainSlotIndex = -1;
        public string[] MainSlotItemIds = new string[5];
    }

    public static class PlayerInventoryService
    {
        public static InventorySnapshot BuildSnapshot(BBBCharacterController player)
        {
            if (player == null)
            {
                throw new InvalidOperationException("player cannot be null.");
            }

            ItemPackVfs.EnsureLayout(player);
            EquipmentPackVfs.EnsureLayout(player);

            var snapshot = new InventorySnapshot();
            snapshot.Slots.AddRange(ReadInventorySlots(player));

            EquipmentPackVfs.TryGetOtherSlotItemId(EquipmentSlot.MainHand, out snapshot.MainHandItemId, player);
            EquipmentPackVfs.TryGetOtherSlotItemId(EquipmentSlot.OffHand, out snapshot.OffHandItemId, player);
            EquipmentPackVfs.TryGetOccupiedMainSlotIndex(out snapshot.OccupiedMainSlotIndex, player);

            for (var i = 1; i <= 5; i++)
            {
                if (EquipmentPackVfs.TryGetMainSlotItemId(i, out var itemId, player))
                {
                    snapshot.MainSlotItemIds[i - 1] = itemId;
                }
            }

            return snapshot;
        }

        public static bool TryUseSlot(BBBCharacterController player, string slotName, out string message)
        {
            message = string.Empty;
            if (!TryGetInventorySlot(player, slotName, out var slot))
            {
                message = "未找到该背包槽位。";
                return false;
            }

            if (slot.Definition is HealingItemSO healing)
            {
                return TryUseHealingItem(player, slot, healing, out message);
            }

            if (slot.Definition is EquippableItemSO)
            {
                var targetMainSlot = ResolvePreferredMainSlot(player);
                return TryAssignInventorySlotToMainSlot(player, slotName, targetMainSlot, autoEquip: true, out message);
            }

            message = "该物品当前没有可执行的直接使用逻辑。";
            return false;
        }

        public static bool TryAssignInventorySlotToMainSlot(
            BBBCharacterController player,
            string slotName,
            int mainSlotIndex,
            bool autoEquip,
            out string message)
        {
            message = string.Empty;
            if (player == null)
            {
                message = "角色为空。";
                return false;
            }

            if (mainSlotIndex < 1 || mainSlotIndex > 5)
            {
                message = "主手槽位必须在 1 到 5 之间。";
                return false;
            }

            if (!TryGetInventorySlot(player, slotName, out var slot))
            {
                message = "未找到该背包槽位。";
                return false;
            }

            if (slot.Definition is not EquippableItemSO)
            {
                message = "只有可装备物品才能放进快捷主手槽。";
                return false;
            }

            if (slot.Data == null || slot.Data.Count != 1)
            {
                message = "当前仅支持单件装备物品进入主手槽。";
                return false;
            }

            string displacedItemId = null;
            string displacedInstanceId = null;
            if (EquipmentPackVfs.TryGetMainSlotData(mainSlotIndex, out var targetData, player) &&
                !string.IsNullOrWhiteSpace(targetData?.Id))
            {
                if (string.Equals(targetData.Id, EquipmentPackVfs.MainSlotOccupierId, StringComparison.Ordinal))
                {
                    if (EquipmentPackVfs.TryGetOtherSlotData(EquipmentSlot.MainHand, out var mainhandData, player))
                    {
                        displacedItemId = mainhandData?.Id;
                        displacedInstanceId = mainhandData?.InstanceId;
                    }
                }
                else
                {
                    displacedItemId = targetData.Id;
                    displacedInstanceId = targetData.InstanceId;
                }
            }

            var assignedInstanceId = string.IsNullOrWhiteSpace(slot.Data.InstanceId) ? Guid.NewGuid().ToString() : slot.Data.InstanceId;
            EquipmentPackVfs.SetMainSlotItem(mainSlotIndex, slot.Data.Id, player, assignedInstanceId);

            if (!string.IsNullOrWhiteSpace(displacedItemId))
            {
                ItemPackVfs.SetSlotItem(slotName, displacedItemId, 1, player, displacedInstanceId);
            }
            else
            {
                ItemPackVfs.ClearSlotItem(slotName, player);
            }

            if (autoEquip)
            {
                if (!TryEquipMainSlot(player, mainSlotIndex, out var equipMessage))
                {
                    message = equipMessage;
                    return false;
                }

                if (string.IsNullOrWhiteSpace(message))
                {
                    message = $"已装备到主手槽 {mainSlotIndex}。";
                }

                return true;
            }

            message = $"已放入主手槽 {mainSlotIndex}。";
            return true;
        }

        public static bool TryEquipMainSlot(BBBCharacterController player, int mainSlotIndex, out string message)
        {
            message = string.Empty;
            if (player == null)
            {
                message = "角色为空。";
                return false;
            }

            if (player.RuntimeData != null && player.RuntimeData.Arbitration.BlockInventory)
            {
                message = "当前状态不允许切换装备。";
                return false;
            }

            if (!EquipmentPackVfs.SwapMainHandWithMainSlot(mainSlotIndex, player))
            {
                message = "该快捷槽没有可装备物品。";
                return false;
            }

            if (!EquipmentPackVfs.TryGetOtherSlotData(EquipmentSlot.MainHand, out var mainhandData, player) ||
                string.IsNullOrWhiteSpace(mainhandData?.Id))
            {
                message = "主手槽切换成功，但未找到主手装备数据。";
                return false;
            }

            var instance = EquipmentManager.EquipById(player, mainhandData.Id, EquipmentSlot.MainHand, instanceId: mainhandData.InstanceId);
            if (instance == null)
            {
                message = $"无法装备 '{mainhandData.Id}'。";
                return false;
            }

            player.RuntimeData.CurrentItem = instance;
            message = $"已切换到主手槽 {mainSlotIndex}。";
            return true;
        }

        public static bool TryUnequipMainHand(BBBCharacterController player, out string message)
        {
            message = string.Empty;
            if (player == null)
            {
                message = "角色为空。";
                return false;
            }

            if (!EquipmentPackVfs.ReturnMainHandToOccupiedMainSlot(player))
            {
                message = "当前没有可回收的主手装备。";
                return false;
            }

            EquipmentManager.Unequip(player, EquipmentSlot.MainHand);
            if (player.RuntimeData != null)
            {
                player.RuntimeData.CurrentItem = null;
            }

            message = "主手装备已卸下。";
            return true;
        }

        private static bool TryUseHealingItem(
            BBBCharacterController player,
            InventorySlotSnapshot slot,
            HealingItemSO healing,
            out string message)
        {
            message = string.Empty;
            if (player == null || player.RuntimeData == null)
            {
                message = "角色未初始化。";
                return false;
            }

            if (!healing.AllowUseAtFullHealth && player.RuntimeData.CurrentHealth >= player.RuntimeData.MaxHealth - 0.01f)
            {
                message = string.IsNullOrWhiteSpace(healing.FullHealthMessageBody)
                    ? "当前生命值已满。"
                    : healing.FullHealthMessageBody;
                return false;
            }

            if (!ItemPackVfs.TryConsumeItem(slot.Data.Id, 1, player))
            {
                message = string.IsNullOrWhiteSpace(healing.EmptyMessageBody)
                    ? "该治疗物品已不足。"
                    : healing.EmptyMessageBody;
                return false;
            }

            if (!player.TryHeal(healing.HealAmount))
            {
                ItemPackVfs.TryAddItem(slot.Data.Id, 1, player);
                message = "治疗失败，物品已回退。";
                return false;
            }

            message = $"使用了 {slot.DisplayName}。";
            return true;
        }

        private static int ResolvePreferredMainSlot(BBBCharacterController player)
        {
            if (player == null)
            {
                return 1;
            }

            if (EquipmentPackVfs.TryGetOccupiedMainSlotIndex(out var occupiedIndex, player) &&
                occupiedIndex >= 1 &&
                occupiedIndex <= 5)
            {
                return occupiedIndex;
            }

            for (var i = 1; i <= 5; i++)
            {
                if (!EquipmentPackVfs.TryGetMainSlotItemId(i, out var itemId, player) || string.IsNullOrWhiteSpace(itemId))
                {
                    return i;
                }
            }

            return 1;
        }

        private static bool TryGetInventorySlot(BBBCharacterController player, string slotName, out InventorySlotSnapshot slot)
        {
            slot = null;
            if (player == null || string.IsNullOrWhiteSpace(slotName))
            {
                return false;
            }

            var slots = ReadInventorySlots(player);
            for (var i = 0; i < slots.Count; i++)
            {
                if (string.Equals(slots[i].SlotName, slotName, StringComparison.Ordinal))
                {
                    slot = slots[i];
                    return true;
                }
            }

            return false;
        }

        private static List<InventorySlotSnapshot> ReadInventorySlots(BBBCharacterController player)
        {
            var result = new List<InventorySlotSnapshot>();
            var analyser = PackVfs.GetAnalyser(player, ItemPackVfs.InventoryPackId);
            var children = analyser.GetChildren(ItemPackVfs.InventoryPackId, ItemPackVfs.SlotsDir, PackAccessSubjects.SystemMin);
            for (var i = 0; i < children.Count; i++)
            {
                if (children[i] is not VFSNodeData node ||
                    !node.IsFile ||
                    !string.Equals(node.Extension, ".item", StringComparison.OrdinalIgnoreCase) ||
                    string.IsNullOrWhiteSpace(node.DataJson))
                {
                    continue;
                }

                ItemData data;
                try
                {
                    data = Newtonsoft.Json.JsonConvert.DeserializeObject<ItemData>(node.DataJson);
                }
                catch
                {
                    continue;
                }

                if (data == null || string.IsNullOrWhiteSpace(data.Id) || data.Count <= 0)
                {
                    continue;
                }

                result.Add(new InventorySlotSnapshot
                {
                    SlotName = node.Name,
                    Data = data,
                    Definition = ResolveDefinition(data.Id)
                });
            }

            result.Sort(CompareSlots);
            return result;
        }

        private static int CompareSlots(InventorySlotSnapshot left, InventorySlotSnapshot right)
        {
            var leftNumeric = int.TryParse(left?.SlotName, out var leftIndex);
            var rightNumeric = int.TryParse(right?.SlotName, out var rightIndex);
            if (leftNumeric && rightNumeric)
            {
                return leftIndex.CompareTo(rightIndex);
            }

            if (leftNumeric != rightNumeric)
            {
                return leftNumeric ? -1 : 1;
            }

            return string.Compare(left?.SlotName, right?.SlotName, StringComparison.Ordinal);
        }

        private static ItemDefinitionSO ResolveDefinition(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                return null;
            }

            var definition = MetaLib.GetObject<ItemDefinitionSO>(itemId);
            if (definition != null)
            {
                return definition;
            }

            var resources = Resources.LoadAll<ItemDefinitionSO>(string.Empty);
            for (var i = 0; i < resources.Length; i++)
            {
                if (resources[i] != null && string.Equals(resources[i].ItemID, itemId, StringComparison.Ordinal))
                {
                    return resources[i];
                }
            }

            return null;
        }
    }
}

*/