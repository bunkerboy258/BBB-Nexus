using System.Linq;
using UnityEngine;

namespace BBBNexus
{
    /*public static class EquipmentCommands
    {
        [CommandInfo("get_player", "获取玩家", "Entity", new[] { "targetName?" })]
        public static CommandOutput GetPlayer(IConsoleController console, int subjectLevel, string[] args, object payload)
        {
            var targetName = args != null && args.Length >= 1 ? args[0] : null;
            var player = ResolvePlayerTarget(targetName, out var error);
            if (player == null)
            {
                return CommandOutput.Fail(error);
            }

            return CommandOutput.Success($"已获取玩家 {player.name}", player);
        }

        [CommandInfo("equip_item", "装备物品", "Entity", new[] { "itemID", "slot?", "targetName?" })]
        public static CommandOutput EquipItem(IConsoleController console, int subjectLevel, string[] args, object payload)
        {
            if (args == null || args.Length < 1 || string.IsNullOrWhiteSpace(args[0]))
            {
                return CommandOutput.Fail("用法: equip_item <itemID> [mainhand|offhand] [targetName]");
            }

            var itemId = args[0].Trim();
            var slot = ParseSlot(args.Length >= 2 ? args[1] : null);
            if (slot == EquipmentSlot.None)
            {
                return CommandOutput.Fail("装备槽位无效。可用值: mainhand, offhand");
            }

            var targetName = args.Length >= 3 ? args[2] : null;
            var player = ResolveTarget(payload, targetName, out var resolveError);
            if (player == null)
            {
                return CommandOutput.Fail(resolveError);
            }

            var instance = EquipmentManager.EquipById(player, itemId, slot);
            if (instance == null)
            {
                return CommandOutput.Fail($"装备失败: {itemId}");
            }

            return CommandOutput.Success(
                $"已为 {player.name} 装备 {itemId} 到 {slot}",
                instance.InstanceID);
        }

        [CommandInfo("unequip_item", "卸下物品", "Entity", new[] { "slot?", "targetName?" })]
        public static CommandOutput UnequipItem(IConsoleController console, int subjectLevel, string[] args, object payload)
        {
            var slot = ParseSlot(args != null && args.Length >= 1 ? args[0] : null, allowEmptyAsBoth: true);
            var targetName = args != null && args.Length >= 2 ? args[1] : null;
            var player = ResolveTarget(payload, targetName, out var resolveError);
            if (player == null)
            {
                return CommandOutput.Fail(resolveError);
            }

            EquipmentManager.Unequip(player, slot);
            return CommandOutput.Success($"已为 {player.name} 卸下 {SlotToText(slot)}");
        }

        private static BBBCharacterController ResolveTarget(object payload, string targetName, out string error)
        {
            if (payload is BBBCharacterController payloadController)
            {
                error = null;
                return payloadController;
            }

            return ResolveAnyTarget(targetName, out error);
        }

        private static BBBCharacterController ResolvePlayerTarget(string targetName, out string error)
        {
            var candidates = Object.FindObjectsOfType<BBBCharacterController>()
                .Where(controller => controller != null && controller.InputSourceRef is PlayerInputReader)
                .ToArray();

            return ResolveFromCandidates(candidates, targetName, "玩家 BBBCharacterController", out error);
        }

        private static BBBCharacterController ResolveAnyTarget(string targetName, out string error)
        {
            var candidates = Object.FindObjectsOfType<BBBCharacterController>()
                .Where(controller => controller != null)
                .ToArray();

            return ResolveFromCandidates(candidates, targetName, "BBBCharacterController", out error);
        }

        private static BBBCharacterController ResolveFromCandidates(BBBCharacterController[] candidates, string targetName, string label, out string error)
        {
            if (!string.IsNullOrWhiteSpace(targetName))
            {
                var matched = candidates
                    .Where(controller => string.Equals(controller.name, targetName, System.StringComparison.OrdinalIgnoreCase))
                    .ToArray();

                if (matched.Length > 1)
                {
                    error = $"找到多个同名 {label}: {targetName}";
                    return null;
                }

                if (matched.Length == 1)
                {
                    error = null;
                    return matched[0];
                }

                error = $"未找到名为 {targetName} 的 {label}";
                return null;
            }

            if (candidates.Length == 1)
            {
                error = null;
                return candidates[0];
            }

            if (candidates.Length == 0)
            {
                error = $"场景中没有 {label}";
                return null;
            }

            error = $"场景中有多个 {label}，请显式传 targetName: " +
                    string.Join(", ", candidates.Select(controller => controller.name));
            return null;
        }

        private static EquipmentSlot ParseSlot(string rawSlot, bool allowEmptyAsBoth = false)
        {
            if (string.IsNullOrWhiteSpace(rawSlot))
            {
                return allowEmptyAsBoth ? EquipmentSlot.None : EquipmentSlot.MainHand;
            }

            switch (rawSlot.Trim().ToLowerInvariant())
            {
                case "main":
                case "mainhand":
                case "right":
                    return EquipmentSlot.MainHand;
                case "off":
                case "offhand":
                case "left":
                    return EquipmentSlot.OffHand;
                case "all":
                case "both":
                case "none":
                    return allowEmptyAsBoth ? EquipmentSlot.None : EquipmentSlot.None;
                default:
                    return EquipmentSlot.None;
            }
        }

        private static string SlotToText(EquipmentSlot slot)
        {
            switch (slot)
            {
                case EquipmentSlot.MainHand:
                    return "MainHand";
                case EquipmentSlot.OffHand:
                    return "OffHand";
                default:
                    return "BothHands";
            }
        }
    }*/
}
