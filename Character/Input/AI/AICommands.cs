using System.Linq;
using UnityEngine;

namespace BBBNexus
{
    public static class AICommands
    {
        [CommandInfo("spawn_bbb", "生成BBB角色", "Entity", new[] { "packID", "x?", "y?", "z?" })]
        public static CommandOutput SpawnBbb(IConsoleController console, int subjectLevel, string[] args, object payload)
        {
            if (args == null || args.Length < 1)
            {
                return CommandOutput.Fail("用法: spawn_bbb <packID> [x y z]");
            }

            var packId = args[0];
            var position = Vector3.zero;
            if (args.Length >= 4)
            {
                if (!float.TryParse(args[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x) ||
                    !float.TryParse(args[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var y) ||
                    !float.TryParse(args[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var z))
                {
                    return CommandOutput.Fail("坐标格式错误，需要浮点数");
                }

                position = new Vector3(x, y, z);
            }

            var runner = GraphRunner.Instance;
            if (runner == null)
            {
                return CommandOutput.Fail("GraphRunner 未初始化");
            }

            if (!MetaLib.HasMeta(packId))
            {
                return CommandOutput.Fail($"MetaLib 中找不到 PackID: '{packId}'");
            }

            var clone = MetaLib.GetPack<BasePackData>(packId);
            if (clone == null)
            {
                return CommandOutput.Fail($"Pack '{packId}' 加载失败");
            }

            if (string.IsNullOrEmpty(clone.RootNodeId))
            {
                return CommandOutput.Fail($"Pack '{packId}' 没有 RootNodeId");
            }

            var before = Object.FindObjectsOfType<BBBCharacterController>(true)
                .Select(controller => controller.GetInstanceID())
                .ToHashSet();

            clone.HasStarted = false;
            clone.ActiveSignals?.Clear();

            var instanceId = runner.LoadPack(clone);
            runner.InjectSignal(instanceId, new SignalContext(clone.RootNodeId, position));
            runner.Tick();

            var spawned = Object.FindObjectsOfType<BBBCharacterController>(true)
                .Where(controller => !before.Contains(controller.GetInstanceID()))
                .ToArray();

            if (spawned.Length == 0)
            {
                return CommandOutput.Fail($"Pack '{packId}' 已运行，但没有生成 BBBCharacterController");
            }

            if (spawned.Length > 1)
            {
                return CommandOutput.Fail($"Pack '{packId}' 生成了多个 BBBCharacterController，无法唯一确定 payload");
            }

            var actor = spawned[0];
            return CommandOutput.Success($"已生成 BBB '{actor.name}' 来自 {packId}", actor);
        }

        [CommandInfo("set_ai_brain", "设置AI脑", "Entity", new[] { "brainType" })]
        public static CommandOutput SetAiBrain(IConsoleController console, int subjectLevel, string[] args, object payload)
        {
            if (args == null || args.Length < 1 || string.IsNullOrWhiteSpace(args[0]))
            {
                return CommandOutput.Fail("用法: set_ai_brain <brainType>");
            }

            if (payload is not BBBCharacterController actor)
            {
                return CommandOutput.Fail("set_ai_brain 需要上游 payload 为 BBBCharacterController");
            }

            if (!AIManager.SetBrain(actor, args[0]))
            {
                return CommandOutput.Fail($"设置 AI brain 失败: {args[0]}");
            }

            return CommandOutput.Success($"已为 {actor.name} 设置 AI brain: {args[0]}", actor);
        }

        [CommandInfo("set_ai_tactical_config", "设置AI战术配置", "Entity", new[] { "configID" })]
        public static CommandOutput SetAiTacticalConfig(IConsoleController console, int subjectLevel, string[] args, object payload)
        {
            if (args == null || args.Length < 1 || string.IsNullOrWhiteSpace(args[0]))
            {
                return CommandOutput.Fail("用法: set_ai_tactical_config <configID>");
            }

            if (payload is not BBBCharacterController actor)
            {
                return CommandOutput.Fail("set_ai_tactical_config 需要上游 payload 为 BBBCharacterController");
            }

            if (!AIManager.SetTacticalConfig(actor, args[0]))
            {
                return CommandOutput.Fail($"设置 AI tactical config 失败: {args[0]}");
            }

            return CommandOutput.Success($"已为 {actor.name} 设置 AI tactical config: {args[0]}", actor);
        }
    }
}
