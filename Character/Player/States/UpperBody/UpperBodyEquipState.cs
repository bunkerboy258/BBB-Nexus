using Characters.Player.Layers;
using Items.Data;
using UnityEngine;
using Characters.Player.Animation;

namespace Characters.Player.States.UpperBody
{
    public class UpperBodyEquipState : UpperBodyBaseState
    {
        public UpperBodyEquipState(PlayerController p) : base(p) { }

        public override void Enter()
        {
            var targetItem = data.DesiredItemDefinition;

            if (targetItem == null)
            {
                controller.StateMachine.ChangeState(controller.StateRegistry.GetState<UpperBodyIdleState>());
                return;
            }

            if (targetItem is EquippableItemSO equipDef && equipDef.EquipAnim.Clip != null)
            {
                // 使用通用方法播放动画
                ChooseOptionsAndPlay(equipDef.EquipAnim);

                // 添加自定义事件：在 70% 时同步模型
                player.AnimFacade.AddCallback(0.7f, () =>
                {
                    player.EquipmentDriver.SyncModelToDesired();
                });

                // 设置结束回调
                player.AnimFacade.SetOnEndCallback(() => controller.StateMachine.ChangeState(controller.StateRegistry.GetState<UpperBodyIdleState>()));
            }
        }

        public override void PhysicsUpdate() { }

        protected override void UpdateStateLogic() { }

        public override void Exit()
        {
            // 离开状态清理回调，符合适配器使用规范
            player.AnimFacade.ClearOnEndCallback();
        }
    }
}
