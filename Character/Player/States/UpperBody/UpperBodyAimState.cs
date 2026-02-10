using Animancer;
using Characters.Player.Data;
using Characters.Player.Layers;
using Items.Data;
using Items.Logic; // 引用 DeviceController
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
        public UpperBodyAimState(PlayerController p, UpperBodyController c) : base(p, c) { }

        public override void Enter()
        {
            var equip = data.CurrentEquipment;

            // 1. 播放瞄准动画
            // 优先用物品自带的 AimPose，如果没有则尝试用通用的，或者维持 Idle
            if (equip.Definition is RangedWeaponSO equipDef&&equipDef.AimAnim!=null)
            {
                layer.Play(equipDef.AimAnim, 0.15f);
            }
            // else: 也可以播一个通用的 "GenericAimPose" 如果你有的话

            player.InputReader.OnLeftMouseDown+= HandleFireInput;
        }

        protected override void UpdateStateLogic()
        {
            // 1. 退出检测
            // 如果松开右键 -> 切回 Idle
            if (!data.IsAiming)
            {
                controller.ChangeState(controller.IdleState);
                return;
            }

        }

        private void HandleFireInput()
        {
            var equip = data.CurrentEquipment;

            // 检查当前装备是否有逻辑控制器 (DeviceLogic)
            if (equip.HasDevice)
            {
                // 调用装置的开火接口
                equip.DeviceLogic.OnTriggerDown();

                // 可以在这里加一个 "PlayShootAnim" (比如 Layer 1 Additive 的后坐力动画)
                // player.UpperBodyController.PlayAdditiveRecoil();
            }
        }

        public override void Exit()
        {

            // 确保松开扳机 (防止按下状态卡住)
            if (data.CurrentEquipment.HasDevice)
            {
                data.CurrentEquipment.DeviceLogic.OnTriggerUp();
            }
        }
    }
}
