using Animancer;
using Characters.Player.Layers;
using Items.Data;
using UnityEngine;
using Characters.Player.Animation;

namespace Characters.Player.States.UpperBody
{
    public class UpperBodyIdleState : UpperBodyBaseState
    {
        public UpperBodyIdleState(PlayerController p) : base(p) { }

        public override void Enter()
        {
            // 1. 播放当前持有物品的 Idle 动画
            PlayCorrectIdle();
        }

        public override void Exit()
        {
        }

        protected override void UpdateStateLogic()
        {
            // 🔥 [核心决策] 检测意图是否改变 🔥
            if (data.DesiredItemDefinition != data.CurrentEquipment.Definition)
            {
                // 意图 (Desired) 与现状 (Current) 不符 -> 触发切换流程

                // Case 1: 手里有东西 -> 先卸载 (Unequip)
                if (data.CurrentEquipment.HasItem)
                {
                    controller.StateMachine.ChangeState(controller.StateRegistry.GetState<UpperBodyUnequipState>());
                    return;
                }

                // Case 2: 手里没东西 -> 直接装备 (Equip)
                if (data.DesiredItemDefinition != null)
                {
                    controller.StateMachine.ChangeState(controller.StateRegistry.GetState<UpperBodyEquipState>());
                    return;
                }
            }

            if(data.IsAiming && data.CurrentEquipment.Definition is RangedWeaponSO)
            {
                controller.StateMachine.ChangeState(controller.StateRegistry.GetState<UpperBodyAimState>());
                return;
            }
        }

        private void PlayCorrectIdle()
        {
            var def = data.CurrentEquipment.Definition;
            // [核心] 类型转换：只有 EquippableItemSO 才有 EquipIdleAnim
            if (def is EquippableItemSO equipDef && equipDef.EquipIdleAnim.Clip != null)
            {
                ChooseOptionsAndPlay(equipDef.EquipIdleAnim);
            }
            else
            {
                // 空手或不支持动画的物品 -> 淡出 Layer 1，显示全身基础动作
                player.AnimFacade.SetLayerWeight(1, 0f, 0.25f);
            }
        }
    }
}

