using System;
using Newtonsoft.Json;
using NekoGraph;
using UnityEngine;

namespace BBBNexus
{
    /// <summary>
    /// 装备 Pack VFS 工具喵~
    /// 负责在 equipment pack 中写入 / 删除 .equipid 文件
    /// 使用 PackVfs 统一层进行路由
    /// </summary>
    public static class EquipmentPackVfs
    {
        public const string EquipmentPackId = "equipment";
        public const string MainSlotDir = "/mainslot";
        public const string OtherSlotDir = "/otherslot";
        public const string HidePackDir = "/hidepack";
        public const string VirtualPackDir = "/virtualpack";
        public const string MainSlotOccupierId = "__mainslotoccupier_mainhand__";

        /// <summary>
        /// 确保装备 Pack 布局存在喵~
        /// </summary>
        public static void EnsureLayout(BBBCharacterController owner)
        {
            if (owner == null)
            {
                throw new InvalidOperationException("owner cannot be null.");
            }

            var analyser = PackVfs.GetAnalyser(owner, EquipmentPackId);
            analyser.CreateDirectory(EquipmentPackId, MainSlotDir, PackAccessSubjects.SystemMin);
            analyser.CreateDirectory(EquipmentPackId, OtherSlotDir, PackAccessSubjects.SystemMin);
            analyser.CreateDirectory(EquipmentPackId, HidePackDir, PackAccessSubjects.SystemMin);
            analyser.CreateDirectory(EquipmentPackId, VirtualPackDir, PackAccessSubjects.SystemMin);
            WriteEquipIdFile(GetMainSlotOccupierVirtualPath(), MainSlotOccupierId, owner);
        }

        public static void SetMainSlotItem(int index, string itemId, BBBCharacterController owner)
        {
            if (owner == null) throw new InvalidOperationException("owner cannot be null.");
            EnsureLayout(owner);
            WriteEquipIdFile($"{MainSlotDir}/{GetMainSlotFileName(index)}.equipid", itemId, owner);
        }

        public static void ClearMainSlotItem(int index, BBBCharacterController owner)
        {
            if (owner == null) throw new InvalidOperationException("owner cannot be null.");
            EnsureLayout(owner);
            DeletePath($"{MainSlotDir}/{GetMainSlotFileName(index)}.equipid", owner);
        }

        public static void SetOtherSlot(EquipmentSlot slot, string itemId, BBBCharacterController owner)
        {
            if (owner == null) throw new InvalidOperationException("owner cannot be null.");
            EnsureLayout(owner);
            WriteEquipIdFile($"{OtherSlotDir}/{GetOtherSlotFileName(slot)}.equipid", itemId, owner);
        }

        public static void ClearOtherSlot(EquipmentSlot slot, BBBCharacterController owner)
        {
            if (owner == null) throw new InvalidOperationException("owner cannot be null.");
            EnsureLayout(owner);
            DeletePath($"{OtherSlotDir}/{GetOtherSlotFileName(slot)}.equipid", owner);
        }

        public static void SetHideSlot(EquipmentSlot slot, string itemId, BBBCharacterController owner)
        {
            if (owner == null) throw new InvalidOperationException("owner cannot be null.");
            EnsureLayout(owner);
            WriteEquipIdFile($"{HidePackDir}/{GetOtherSlotFileName(slot)}.equipid", itemId, owner);
        }

        public static void ClearHideSlot(EquipmentSlot slot, BBBCharacterController owner)
        {
            if (owner == null) throw new InvalidOperationException("owner cannot be null.");
            EnsureLayout(owner);
            DeletePath($"{HidePackDir}/{GetOtherSlotFileName(slot)}.equipid", owner);
        }

        public static string BuildEquipIdJson(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                throw new ArgumentException("Item id cannot be empty.", nameof(itemId));
            }

            return JsonConvert.SerializeObject(new EquipIdData { Id = itemId }, Formatting.Indented);
        }

        public static bool TryGetOtherSlotItemId(EquipmentSlot slot, out string itemId, BBBCharacterController owner)
        {
            itemId = null;
            if (owner == null) throw new InvalidOperationException("owner cannot be null.");
            
            EnsureLayout(owner);
            return TryReadEquipIdFile($"{OtherSlotDir}/{GetOtherSlotFileName(slot)}.equipid", out itemId, owner);
        }

        public static bool TryGetMainSlotItemId(int index, out string itemId, BBBCharacterController owner)
        {
            itemId = null;
            if (owner == null) throw new InvalidOperationException("owner cannot be null.");
            
            EnsureLayout(owner);
            return TryReadEquipIdFile($"{MainSlotDir}/{GetMainSlotFileName(index)}.equipid", out itemId, owner);
        }

        public static bool TryGetHideSlotItemId(EquipmentSlot slot, out string itemId, BBBCharacterController owner)
        {
            itemId = null;
            if (owner == null) throw new InvalidOperationException("owner cannot be null.");
            
            EnsureLayout(owner);
            return TryReadEquipIdFile($"{HidePackDir}/{GetOtherSlotFileName(slot)}.equipid", out itemId, owner);
        }

        public static void SetVirtualSlot(string ownerItemId, EquipmentSlot slot, string itemId, BBBCharacterController owner)
        {
            if (owner == null) throw new InvalidOperationException("owner cannot be null.");
            EnsureLayout(owner);
            WriteEquipIdFile(BuildVirtualSlotPath(ownerItemId, slot), itemId, owner);
        }

        public static void ClearVirtualSlot(string ownerItemId, EquipmentSlot slot, BBBCharacterController owner)
        {
            if (owner == null) throw new InvalidOperationException("owner cannot be null.");
            DeletePath(BuildVirtualSlotPath(ownerItemId, slot), owner);
        }

        public static bool TryGetVirtualSlotItemId(string ownerItemId, EquipmentSlot slot, out string itemId, BBBCharacterController owner)
        {
            itemId = null;
            if (owner == null) throw new InvalidOperationException("owner cannot be null.");
            
            return TryReadEquipIdFile(BuildVirtualSlotPath(ownerItemId, slot), out itemId, owner);
        }

        public static bool TryTakeVirtualSlotItemId(string ownerItemId, EquipmentSlot slot, out string itemId, BBBCharacterController owner)
        {
            itemId = null;
            if (owner == null) throw new InvalidOperationException("owner cannot be null.");
            
            if (!TryGetVirtualSlotItemId(ownerItemId, slot, out itemId, owner))
            {
                return false;
            }

            ClearVirtualSlot(ownerItemId, slot, owner);
            return true;
        }

        public static bool TryGetOccupiedMainSlotIndex(out int index, BBBCharacterController owner)
        {
            index = -1;
            if (owner == null) throw new InvalidOperationException("owner cannot be null.");
            
            EnsureLayout(owner);

            for (int i = 1; i <= 5; i++)
            {
                if (TryGetMainSlotItemId(i, out var itemId, owner) &&
                    string.Equals(itemId, MainSlotOccupierId, StringComparison.Ordinal))
                {
                    index = i;
                    return true;
                }
            }

            return false;
        }

        public static bool SwapMainHandWithMainSlot(int index, BBBCharacterController owner)
        {
            if (owner == null) throw new InvalidOperationException("owner cannot be null.");
            
            EnsureLayout(owner);

            if (!TryGetMainSlotItemId(index, out var targetItemId, owner) ||
                string.IsNullOrWhiteSpace(targetItemId) ||
                string.Equals(targetItemId, MainSlotOccupierId, StringComparison.Ordinal))
            {
                return false;
            }

            TryGetOtherSlotItemId(EquipmentSlot.MainHand, out var currentMainhandItemId, owner);
            var hasOccupiedIndex = TryGetOccupiedMainSlotIndex(out var occupiedIndex, owner);

            if (!string.IsNullOrWhiteSpace(currentMainhandItemId) && !hasOccupiedIndex)
            {
                Debug.LogWarning("[EquipmentPackVfs] MainHand has item but no mainslot occupier was found. Swap aborted.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(currentMainhandItemId) && hasOccupiedIndex && occupiedIndex != index)
            {
                ClearMainSlotItem(occupiedIndex, owner);
            }

            SetMainSlotItem(index, MainSlotOccupierId, owner);
            SetOtherSlot(EquipmentSlot.MainHand, targetItemId, owner);

            if (!string.IsNullOrWhiteSpace(currentMainhandItemId) && occupiedIndex > 0 && occupiedIndex != index)
            {
                SetMainSlotItem(occupiedIndex, currentMainhandItemId, owner);
            }

            return true;
        }

        public static bool ReturnMainHandToOccupiedMainSlot(BBBCharacterController owner)
        {
            if (owner == null) throw new InvalidOperationException("owner cannot be null.");
            
            EnsureLayout(owner);

            if (!TryGetOtherSlotItemId(EquipmentSlot.MainHand, out var currentMainhandItemId, owner) ||
                string.IsNullOrWhiteSpace(currentMainhandItemId))
            {
                return false;
            }

            if (!TryGetOccupiedMainSlotIndex(out var occupiedIndex, owner))
            {
                return false;
            }

            SetMainSlotItem(occupiedIndex, currentMainhandItemId, owner);
            ClearOtherSlot(EquipmentSlot.MainHand, owner);
            return true;
        }

        public static void SwapEquipIdPaths(string leftPath, string rightPath, string emptyFallbackForLeft, BBBCharacterController owner)
        {
            if (owner == null) throw new InvalidOperationException("owner cannot be null.");
            
            EnsureLayout(owner);

            TryReadEquipIdFile(leftPath, out var leftItemId, owner);
            TryReadEquipIdFile(rightPath, out var rightItemId, owner);

            WriteOrDeleteEquipIdFile(leftPath, string.IsNullOrWhiteSpace(rightItemId) ? emptyFallbackForLeft : rightItemId, owner);
            WriteOrDeleteEquipIdFile(rightPath, leftItemId, owner);
        }

        // =========================================================
        // 内部辅助方法喵~
        // =========================================================

        private static void WriteEquipIdFile(string path, string itemId, BBBCharacterController owner)
        {
            var analyser = PackVfs.GetAnalyser(owner, EquipmentPackId);
            if (!analyser.WriteFile(EquipmentPackId, path, BuildEquipIdJson(itemId), PackAccessSubjects.SystemMin))
            {
                throw new InvalidOperationException($"Failed to write equipment VFS file: {path}");
            }
        }

        private static void WriteOrDeleteEquipIdFile(string path, string itemId, BBBCharacterController owner)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                DeletePath(path, owner);
                return;
            }

            WriteEquipIdFile(path, itemId, owner);
        }

        private static void DeletePath(string path, BBBCharacterController owner)
        {
            var analyser = PackVfs.GetAnalyser(owner, EquipmentPackId);
            analyser.Delete(EquipmentPackId, path, PackAccessSubjects.SystemMin);
        }

        private static bool TryReadEquipIdFile(string path, out string itemId, BBBCharacterController owner)
        {
            itemId = null;
            var analyser = PackVfs.GetAnalyser(owner, EquipmentPackId);
            var node = analyser.GetNode(EquipmentPackId, path, PackAccessSubjects.SystemMin) as VFSNodeData;
            if (node == null || string.IsNullOrWhiteSpace(node.DataJson))
            {
                return false;
            }

            var data = JsonConvert.DeserializeObject<EquipIdData>(node.DataJson);
            if (data == null || string.IsNullOrWhiteSpace(data.Id))
            {
                return false;
            }

            itemId = data.Id;
            return true;
        }

        private static string GetOtherSlotFileName(EquipmentSlot slot)
        {
            return slot switch
            {
                EquipmentSlot.MainHand => "mainhand",
                EquipmentSlot.OffHand => "offhand",
                _ => throw new ArgumentOutOfRangeException(nameof(slot), slot, "Only MainHand and OffHand are supported.")
            };
        }

        private static string GetMainSlotFileName(int index)
        {
            if (index <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "Main slot index must start at 1.");
            }

            return index.ToString();
        }

        private static string BuildVirtualSlotPath(string ownerItemId, EquipmentSlot slot)
        {
            if (string.IsNullOrWhiteSpace(ownerItemId))
            {
                throw new ArgumentException("Owner item id cannot be empty.", nameof(ownerItemId));
            }

            return $"{VirtualPackDir}/{GetOtherSlotFileName(slot)}/{NormalizeSegment(ownerItemId)}.equipid";
        }

        private static string GetMainSlotOccupierVirtualPath()
        {
            return $"{VirtualPackDir}/mainslotoccupier/mainhand.equipid";
        }

        private static string NormalizeSegment(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Path segment cannot be empty.", nameof(value));
            }

            return value.Trim()
                .Replace('/', '_')
                .Replace('\\', '_')
                .Replace(':', '_');
        }
    }
}
