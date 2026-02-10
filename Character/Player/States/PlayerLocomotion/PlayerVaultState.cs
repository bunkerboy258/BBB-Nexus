using UnityEngine;
using Animancer;
using Characters.Player.Data;

namespace Characters.Player.States
{
    public class PlayerVaultState : PlayerBaseState
    {
        private AnimancerState _state;
        private float _stateDuration;
        private float _startYaw;

        public PlayerVaultState(PlayerController player) : base(player) { }

        public override void Enter()
        {
            Debug.Log("Entered Vault State");
            _stateDuration = 0f;
            _startYaw = player.transform.eulerAngles.y;

            // 1. 禁用上半身层
            data.IsVaulting=true;

            // 2. 播放翻越动画
            var clipData = config.VaultFenceAnim;
            if (clipData == null || clipData.Clip.Clip == null)
            {
                player.StateMachine.ChangeState(player.IdleState);
                return;
            }

            _state = player.Animancer.Layers[0].Play(clipData.Clip);

            // 3. 结束回调
            _state.Events(this).OnEnd = () =>
            {
                // 如果翻越结束时玩家还推着摇杆 -> 进 Loop
                if (data.MoveInput.sqrMagnitude > 0.01f)
                    player.StateMachine.ChangeState(player.MoveLoopState);
                else
                    player.StateMachine.ChangeState(player.IdleState);
            };
        }

        public override void LogicUpdate()
        {
            // 翻越过程不可打断
        }

        public override void PhysicsUpdate()
        {
            if (_state == null) return;

            _stateDuration += Time.deltaTime * _state.Speed;

            // 🔥 [关键] 使用 MotionDriver 驱动 🔥
            // 翻越动画必须提前烘焙好 SpeedCurve
            // 这里我们不需要旋转 (startYaw)，通常翻越是直线的
            // 如果你的翻越动画带转身，需要烘焙 RotationCurve

            player.MotionDriver.UpdateMotion(
                config.VaultFenceAnim,
                _stateDuration,
                _startYaw
            );

            // ⚠️ 注意：如果你的翻越动画有明显的 Y 轴位移 (跳起)
            // 你的 MotionDriver 需要支持 Y 轴烘焙 (HeightCurve)
            // 或者在这里手动处理 CharacterController 的高度
            // 临时方案：暂时依赖 CC 的 StepOffset 或者允许动画 Root Motion 的 Y 轴生效
        }

        public override void Exit()
        {
            _state = null;
            data.IsVaulting=false;

            // 恢复上半身层
            // player.UpperBodyController.SetWeight(1f);
        }
    }
}
