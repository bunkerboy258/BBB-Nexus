using Animancer;
using Characters.Player.Layers;
using Items.Data;
using UnityEngine;

namespace Characters.Player.States.UpperBody
{
    public class UpperBodyEquipState : UpperBodyBaseState
    {
        public UpperBodyEquipState(PlayerController p, UpperBodyController c) : base(p, c) { }

        public override void Enter()
        {
            var targetItem = data.DesiredItemDefinition;

            if (targetItem == null) // 防御性代码
            {
                controller.ChangeState(controller.IdleState);
                return;
            }
            if(targetItem is EquippableItemSO equipDef&& equipDef.EquipAnim.Clip!=null)
            {
                var state=layer.Play(equipDef.EquipAnim);

                var events = state.Events(this);
                events.Add(0.7f, () =>
                {
                    // 2. 在动画 70% 时生成模型 (模拟手从背后拿东西出来)
                    player.EquipmentDriver.SyncModelToDesired();
                });

                state.Events(this).OnEnd = () => controller.ChangeState(controller.IdleState);
            }


        }

        protected override void UpdateStateLogic() { }
        public override void Exit() { }
    }
}
