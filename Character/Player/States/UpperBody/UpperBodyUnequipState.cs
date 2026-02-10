using UnityEngine;
using Animancer;
using Characters.Player.Layers;
using Items.Data; // 引用

namespace Characters.Player.States.UpperBody
{
    public class UpperBodyUnequipState : UpperBodyBaseState
    {
        public UpperBodyUnequipState(PlayerController p, UpperBodyController c) : base(p, c) { }

        public override void Enter()
        {
            var currentDef = data.CurrentEquipment.Definition;

            // 1. 检查是否是可装备物品，且有卸载动画
            if (currentDef is EquippableItemSO equipDef && equipDef.UnequipAnim.Clip != null)
            {
                var state = layer.Play(equipDef.UnequipAnim);

                // 2. 绑定卸载事件
                // 在动画播放到 70% (手放回背后) 时，销毁手中的模型
                var events= state.Events(this);
                events.Add(0.7f, () =>
                {
                    // 强制卸载当前模型 (设为 null)
                    // 即使 Desired 是新武器，这里也先变成空手
                    player.EquipmentDriver.UnloadCurrentModel();
                });

                // 3. 结束 -> Idle
                // IdleState 会再次检测意图。如果 Desired != null，它会再次触发 EquipState
                state.Events(this).OnEnd = () => controller.ChangeState(controller.IdleState);
            }
            else
            {
                // 没有动画或不是装备 -> 瞬间卸载
                player.EquipmentDriver.UnloadCurrentModel();
                controller.ChangeState(controller.IdleState);
            }
        }

        protected override void UpdateStateLogic() { } // 不允许打断
        public override void Exit() { }
    }
}
