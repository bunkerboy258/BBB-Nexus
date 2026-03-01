using UnityEngine;
using Characters.Player.Layers;
using Items.Data;
using Characters.Player.Animation;

namespace Characters.Player.States.UpperBody
{
    public class UpperBodyUnequipState : UpperBodyBaseState
    {
        public UpperBodyUnequipState(PlayerController p) : base(p) { }

        public override void Enter()
        {
            var currentDef = data.CurrentEquipment.Definition;

            // 1. 检查是否是可装备物品，且有卸载动画
            if (currentDef is EquippableItemSO equipDef && equipDef.UnequipAnim.Clip != null)
            {
                // 使用通用方法播放卸载动画
                ChooseOptionsAndPlay(equipDef.UnequipAnim);

                // 2. 绑定卸载事件
                // 在动画播放到 70% (手放回背后) 时，销毁手中的模型
                player.AnimFacade.AddCallback(0.7f, () =>
                {
                    // 强制卸载当前模型 (设为 null)
                    player.EquipmentDriver.UnloadCurrentModel();
                });

                // 3. 结束 -> Idle
                player.AnimFacade.SetOnEndCallback(() => controller.StateMachine.ChangeState(controller.StateRegistry.GetState<UpperBodyIdleState>()));
            }
            else
            {
                // 没有动画或不是装备 -> 瞬间卸载
                player.EquipmentDriver.UnloadCurrentModel();
                controller.StateMachine.ChangeState(controller.StateRegistry.GetState<UpperBodyIdleState>());
            }
        }

        protected override void UpdateStateLogic() { } // 不允许打断
        public override void Exit()
        {
            player.AnimFacade.ClearOnEndCallback();
        }
    }
}
