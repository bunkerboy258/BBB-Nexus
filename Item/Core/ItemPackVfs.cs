/* REMOVED - old PackVfs layer
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using NekoGraph;
using UnityEngine;

namespace BBBNexus
{
    /// <summary>
    /// 物品 Pack VFS 工具喵~
    /// 负责在 inventory pack 中写入 / 读取 .item 文件。
    /// </summary>
    public static class ItemPackVfs
    {
        public const string InventoryPackId = "inventory";
        public const string SlotsDir = "/slots";

        public static void EnsureLayout(BBBCharacterController owner)
        {
            if (owner == null)
            {
                throw new InvalidOperationException("owner cannot be null.");
            }

            var analyser = PackVfs.GetAnalyser(owner, InventoryPackId);
            analyser.CreateDirectory(InventoryPackId, SlotsDir, PackAccessSubjects.SystemMin);
        }

        public static string BuildItemJson(string itemId, int count = 1, string instanceId = null)
        {
            return BuildItemJson(new ItemData
            {
                Id = itemId,
                Count = count,
                InstanceId = instanceId
            });
        }

        public static string BuildItemJson(ItemData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (string.IsNullOrWhiteSpace(data.Id))
            {
                throw new ArgumentException("Item id cannot be empty.", nameof(data));
            }

            if (data.Count <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(data), data.Count, "Item count must be greater than 0.");
            }

            return JsonConvert.SerializeObject(data, Formatting.Indented);
        }

        public static void SetSlotItem(string slotName, string itemId, int count, BBBCharacterController owner, string instanceId = null)
        {
            SetSlotItem(slotName, new ItemData
            {
                Id = itemId,
                Count = count,
                InstanceId = instanceId
            }, owner);
        }

        public static void SetSlotItem(string slotName, ItemData data, BBBCharacterController owner)
        {
            if (owner == null) throw new InvalidOperationException("owner cannot be null.");

            EnsureLayout(owner);

            var analyser = PackVfs.GetAnalyser(owner, InventoryPackId);
            var path = BuildSlotPath(slotName);
            if (!analyser.WriteFile(InventoryPackId, path, BuildItemJson(data), PackAccessSubjects.SystemMin))
            {
                throw new InvalidOperationException($"Failed to write item VFS file: {path}");
            }
        }

        public static void ClearSlotItem(string slotName, BBBCharacterController owner)
        {
            if (owner == null) throw new InvalidOperationException("owner cannot be null.");

            EnsureLayout(owner);

            var analyser = PackVfs.GetAnalyser(owner, InventoryPackId);
            analyser.Delete(InventoryPackId, BuildSlotPath(slotName), PackAccessSubjects.SystemMin);
        }

        public static bool TryGetSlotItem(string slotName, out ItemData data, BBBCharacterController owner)
        {
            data = null;
            if (owner == null) throw new InvalidOperationException("owner cannot be null.");

            EnsureLayout(owner);

            var analyser = PackVfs.GetAnalyser(owner, InventoryPackId);
            var node = analyser.GetNode(InventoryPackId, BuildSlotPath(slotName), PackAccessSubjects.SystemMin) as VFSNodeData;
            if (node == null || string.IsNullOrWhiteSpace(node.DataJson))
            {
                return false;
            }

            data = JsonConvert.DeserializeObject<ItemData>(node.DataJson);
            return data != null &&
                   !string.IsNullOrWhiteSpace(data.Id) &&
                   data.Count > 0;
        }

        public static int GetItemCount(string itemId, BBBCharacterController owner)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                return 0;
            }

            if (owner == null)
            {
                throw new InvalidOperationException("owner cannot be null.");
            }

            EnsureLayout(owner);

            var analyser = PackVfs.GetAnalyser(owner, InventoryPackId);
            var children = analyser.GetChildren(InventoryPackId, SlotsDir, PackAccessSubjects.SystemMin);
            var total = 0;
            for (var i = 0; i < children.Count; i++)
            {
                if (children[i] is not VFSNodeData node ||
                    !node.IsFile ||
                    !string.Equals(node.Extension, ".item", StringComparison.OrdinalIgnoreCase) ||
                    string.IsNullOrWhiteSpace(node.DataJson))
                {
                    continue;
                }

                var data = JsonConvert.DeserializeObject<ItemData>(node.DataJson);
                if (data == null ||
                    string.IsNullOrWhiteSpace(data.Id) ||
                    !string.Equals(data.Id, itemId, StringComparison.Ordinal) ||
                    data.Count <= 0)
                {
                    continue;
                }

                total += data.Count;
            }

            return total;
        }

        public static bool HasItem(string itemId, BBBCharacterController owner, int minCount = 1)
        {
            if (minCount <= 0)
            {
                return true;
            }

            return GetItemCount(itemId, owner) >= minCount;
        }

        public static bool TryConsumeItem(string itemId, int count, BBBCharacterController owner)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                return false;
            }

            if (count <= 0)
            {
                return true;
            }

            if (owner == null)
            {
                throw new InvalidOperationException("owner cannot be null.");
            }

            EnsureLayout(owner);

            if (GetItemCount(itemId, owner) < count)
            {
                return false;
            }

            var analyser = PackVfs.GetAnalyser(owner, InventoryPackId);
            var children = analyser.GetChildren(InventoryPackId, SlotsDir, PackAccessSubjects.SystemMin);
            var remaining = count;
            var updates = new List<(string Path, ItemData Data)>(children.Count);
            var deletes = new List<string>(children.Count);

            for (var i = 0; i < children.Count && remaining > 0; i++)
            {
                if (children[i] is not VFSNodeData node ||
                    !node.IsFile ||
                    !string.Equals(node.Extension, ".item", StringComparison.OrdinalIgnoreCase) ||
                    string.IsNullOrWhiteSpace(node.DataJson))
                {
                    continue;
                }

                var data = JsonConvert.DeserializeObject<ItemData>(node.DataJson);
                if (data == null ||
                    string.IsNullOrWhiteSpace(data.Id) ||
                    !string.Equals(data.Id, itemId, StringComparison.Ordinal) ||
                    data.Count <= 0)
                {
                    continue;
                }

                var path = BuildSlotPath(node.Name);
                var remove = Math.Min(remaining, data.Count);
                data.Count -= remove;
                remaining -= remove;

                if (data.Count > 0)
                {
                    updates.Add((path, data));
                }
                else
                {
                    deletes.Add(path);
                }
            }

            if (remaining > 0)
            {
                return false;
            }

            for (var i = 0; i < updates.Count; i++)
            {
                var update = updates[i];
                if (!analyser.WriteFile(InventoryPackId, update.Path, BuildItemJson(update.Data), PackAccessSubjects.SystemMin))
                {
                    throw new InvalidOperationException($"Failed to update item VFS file: {update.Path}");
                }
            }

            for (var i = 0; i < deletes.Count; i++)
            {
                analyser.Delete(InventoryPackId, deletes[i], PackAccessSubjects.SystemMin);
            }

            return true;
        }

        public static bool TryAddItem(string itemId, int count, BBBCharacterController owner, string instanceId = null)
        {
            if (string.IsNullOrWhiteSpace(itemId) || count <= 0)
            {
                return false;
            }

            if (owner == null)
            {
                throw new InvalidOperationException("owner cannot be null.");
            }

            EnsureLayout(owner);

            var definition = ResolveDefinition(itemId);
            if (definition == null)
            {
                return false;
            }

            var maxStack = Math.Max(1, definition.MaxStack);
            var analyser = PackVfs.GetAnalyser(owner, InventoryPackId);
            var children = analyser.GetChildren(InventoryPackId, SlotsDir, PackAccessSubjects.SystemMin);
            var remaining = count;
            var firstEmptySlot = -1;

            for (var i = 1; i <= children.Count + remaining + 8 && remaining > 0; i++)
            {
                var slotName = i.ToString();
                var path = BuildSlotPath(slotName);
                var node = analyser.GetNode(InventoryPackId, path, PackAccessSubjects.SystemMin) as VFSNodeData;
                if (node == null || string.IsNullOrWhiteSpace(node.DataJson))
                {
                    if (firstEmptySlot < 0)
                    {
                        firstEmptySlot = i;
                    }

                    continue;
                }

                var data = JsonConvert.DeserializeObject<ItemData>(node.DataJson);
                if (data == null ||
                    !string.Equals(data.Id, itemId, StringComparison.Ordinal) ||
                    data.Count <= 0 ||
                    data.Count >= maxStack)
                {
                    continue;
                }

                var add = Math.Min(remaining, maxStack - data.Count);
                data.Count += add;
                remaining -= add;

                if (!analyser.WriteFile(InventoryPackId, path, BuildItemJson(data), PackAccessSubjects.SystemMin))
                {
                    throw new InvalidOperationException($"Failed to update item VFS file: {path}");
                }
            }

            var nextSlot = firstEmptySlot > 0 ? firstEmptySlot : FindNextAvailableSlot(analyser);
            while (remaining > 0)
            {
                var put = Math.Min(remaining, maxStack);
                var data = new ItemData
                {
                    Id = itemId,
                    Count = put,
                    InstanceId = string.IsNullOrWhiteSpace(instanceId) ? null : instanceId
                };

                var path = BuildSlotPath(nextSlot.ToString());
                if (!analyser.WriteFile(InventoryPackId, path, BuildItemJson(data), PackAccessSubjects.SystemMin))
                {
                    throw new InvalidOperationException($"Failed to write item VFS file: {path}");
                }

                remaining -= put;
                nextSlot++;
            }

            return true;
        }

        public static string BuildSlotPath(string slotName)
        {
            if (string.IsNullOrWhiteSpace(slotName))
            {
                throw new ArgumentException("Slot name cannot be empty.", nameof(slotName));
            }

            return $"{SlotsDir}/{NormalizeSegment(slotName)}.item";
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

        private static int FindNextAvailableSlot(GraphAnalyser analyser)
        {
            var slot = 1;
            while (analyser.GetNode(InventoryPackId, BuildSlotPath(slot.ToString()), PackAccessSubjects.SystemMin) != null)
            {
                slot++;
            }

            return slot;
        }

        private static ItemDefinitionSO ResolveDefinition(string itemId)
        {
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