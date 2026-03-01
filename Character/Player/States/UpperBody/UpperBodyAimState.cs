using Characters.Player.Data;
using Characters.Player.Layers;
using Items.Data;
using Items.Logic;
using Characters.Player.Animation;
using UnityEngine;

namespace Characters.Player.States.UpperBody
{
    /// <summary>
    /// 上半身瞄准状态。
    /// 职责：
    /// 1. 播放瞄准动画 (Aim Pose)。
    /// 2. 开启 IK (手部 + 身体注视)。
    /// 3. 监听开火输入 -> 调用 DeviceLogic。
    /// </summary>
    public class UpperBodyAimState : UpperBodyBaseState
    {
        public UpperBodyAimState(PlayerController p) : base(p) { }

        public override void Enter()
        {
            var equip = data.CurrentEquipment;

            // 1. 播放瞄准动画 (使用通用方法)
            if (equip.Definition is RangedWeaponSO equipDef && equipDef.AimAnim != null)
            {
                ChooseOptionsAndPlay(equipDef.AimAnim);
            }

            player.InputReader.OnLeftMouseDown += HandleFireInput;
        }

        protected override void UpdateStateLogic()
        {
            // 1. 退出检测
            // 如果松开右键 -> 切回 Idle
            if (!data.IsAiming)
            {
                controller.StateMachine.ChangeState(controller.StateRegistry.GetState<UpperBodyIdleState>());
                return;
            }
        }

        public override void PhysicsUpdate() { }
        private void HandleFireInput()
        {
            var equip = data.CurrentEquipment;

            // 检查当前装备是否有逻辑控制器 (DeviceLogic)
            if (equip.HasDevice)
            {
                // 调用装置的开火接口
                equip.DeviceLogic.OnTriggerDown();
            }
        }

        public override void Exit()
        {
            // 解绑开火输入，防止内存泄漏
            player.InputReader.OnLeftMouseDown -= HandleFireInput;

            // 确保松开扳机 (防止按下状态卡住)
            if (data.CurrentEquipment.HasDevice)
            {
                data.CurrentEquipment.DeviceLogic.OnTriggerUp();
            }
        }
    }
}
