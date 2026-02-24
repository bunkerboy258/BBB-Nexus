using UnityEngine;
using Animancer;
using Characters.Player.Data;

namespace Characters.Player.States
{
    /// <summary>
    /// 玩家闪避状态类
    /// 职责：
    /// 1. 根据当前运动状态（Run vs others）和量化方向选择闪避动画；
    /// 2. 利用 MotionDriver 的 Warped 模式驱动精准位移；
    /// 3. 在动作结束时平滑切回移动或空闲。
    /// </summary>
    public class PlayerDodgeState : PlayerBaseState
    {
        private AnimancerState _state;
        private WarpedMotionData _selectedData;

        /// <summary>
        /// 状态构造函数
        /// </summary>
        public PlayerDodgeState(PlayerController player) : base(player) { }

        // 闪避过程不可被通用强制打断
        protected override bool CheckInterrupts() => false;

        #region State Lifecycle（状态生命周期）

        /// <summary>
        /// 进入状态：确定闪避方向和动画，初始化扭曲驱动并播放动画
        /// </summary>
        public override void Enter()
        {
            data.IsDodgeing = true;
            data.WantsToDodge = false;

            // 1. 动画选择逻辑
            _selectedData = GetDodgeData();

            // 防错处理
            if (_selectedData == null || _selectedData.Clip == null)
            {
                player.StateMachine.ChangeState(player.IdleState);
                return;
            }

            // 2. 初始化驱动引擎
            player.MotionDriver.InitializeWarpData(_selectedData);

            // 3. 播放动画
            _state = player.Animancer.Layers[0].Play(_selectedData.Clip);

            // 4. 配置结束回调
            _state.Events(this).OnEnd = () =>
            {
                if(data.CurrentLocomotionState == LocomotionState.Idle)
                {
                    player.StateMachine.ChangeState(player.IdleState);
                }
                else
                {
                    data.loopFadeInTime = data.CurrentLocomotionState == LocomotionState.Sprint ? 0f:0.7f ;
                    data.ExpectedFootPhase = _selectedData.EndPhase; // 传递末相位给 MoveLoopState
                    player.StateMachine.ChangeState(player.MoveLoopState);
                }
            };
        }

        /// <summary>
        /// 更新状态逻辑：同步 Warping 时间进度
        /// </summary>
        protected override void UpdateStateLogic()
        {
            if (_state == null) return;
            data.NormalizedWarpTime = Mathf.Clamp01(_state.NormalizedTime);
        }

        /// <summary>
        /// 物理更新：通过 MotionDriver 执行扭曲位移计算
        /// </summary>
        public override void PhysicsUpdate()
        {
            if (_state == null) return;

            // 驱动 Warped 物理移动
            player.MotionDriver.UpdateWarpMotion(data.NormalizedWarpTime);
        }

        /// <summary>
        /// 退出状态：重置标志位，清理驱动器数据
        /// </summary>
        public override void Exit()
        {
            data.IsDodgeing = false;
            data.IsWarping = false;
            data.ActiveWarpData = null;
            data.WantsToDodge = false;

            player.MotionDriver.ClearWarpData();
            _state = null;
        }

        /// <summary>
        /// 根据运动状态和角度获取闪避动画数据。
        /// Idle/Walk/Jog：支持八个方向
        /// Sprint：支持八个方向（映射到对应的 MoveDodge 动画）
        /// </summary>
        private WarpedMotionData GetDodgeData()
        {
            LocomotionState state = data.CurrentLocomotionState;
            float angle = data.DesiredLocalMoveAngle;
            bool isSprint = state == LocomotionState.Sprint;

            // 定义扇区角度常量（参考 PlayerMoveStartState）
            const float SectorAngle = 45f;
            const float HalfSectorAngle = 22.5f;

            // 8方向判断逻辑
            // Fwd
            if (angle > -HalfSectorAngle && angle <= HalfSectorAngle)
            {
                return isSprint ? config.MoveLeftDodge : config.ForwardDodge;
            }
            // Fwd-Right
            if (angle > HalfSectorAngle && angle <= HalfSectorAngle + SectorAngle)
            {
                return isSprint ? config.MoveForwardRightDodge : config.ForwardRightDodge;
            }
            // Right
            if (angle > HalfSectorAngle + SectorAngle && angle <= HalfSectorAngle + SectorAngle * 2)
            {
                return isSprint ? config.MoveRightDodge : config.RightDodge;
            }
            // Back-Right
            if (angle > HalfSectorAngle + SectorAngle * 2 && angle <= 180f - HalfSectorAngle)
            {
                return isSprint ? config.MoveRightDodge : config.BackwardRightDodge;
            }
            // Back (covers +157.5 to 180 and -180 to -157.5)
            if (angle > 180f - HalfSectorAngle || angle <= -180f + HalfSectorAngle)
            {
                return isSprint ? config.MoveRightDodge : config.BackwardDodge;
            }
            // Back-Left
            if (angle > -180f + HalfSectorAngle && angle <= -HalfSectorAngle - SectorAngle * 2)
            {
                return isSprint ? config.MoveLeftDodge : config.BackwardLeftDodge;
            }
            // Left
            if (angle > -HalfSectorAngle - SectorAngle * 2 && angle <= -HalfSectorAngle - SectorAngle)
            {
                return isSprint ? config.MoveLeftDodge : config.LeftDodge;
            }
            // Fwd-Left
            if (angle > -HalfSectorAngle - SectorAngle && angle <= -HalfSectorAngle)
            {
                return isSprint ? config.MoveForwardLeftDodge : config.ForwardLeftDodge;
            }

            // 兜底：默认左闪避（保留原逻辑倾向）
            return isSprint ? config.MoveLeftDodge : config.LeftDodge;
        }
        //注意:这里由我买的动画包影响 移动状态只有四个方向
        #endregion
    }
}
