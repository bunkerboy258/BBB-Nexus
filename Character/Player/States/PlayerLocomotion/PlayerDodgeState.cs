using UnityEngine;
using Characters.Player.Data;
using Characters.Player.Animation;

namespace Characters.Player.States
{
    /// <summary>
    /// 玩家闪避状态类
    /// 职责：
    /// 1. 根据当前运动状态和量化方向选择闪避动画；
    /// 2. 利用 MotionDriver 的 Warped 模式驱动精准位移；
    /// 3. 在动作结束时平滑切回移动或空闲。
    /// </summary>
    public class PlayerDodgeState : PlayerBaseState
    {
        // 【已废除】不再需要直接引用 AnimancerState
        // private AnimancerState _state;

        // 缓存当前选中的闪避数据
        private WarpedMotionData _selectedData;

        // 跟踪播放时长以及是否已按 EndTime 触发结束逻辑，防止重复触发
        private float _stateDuration;
        private bool _endTimeTriggered;

        public PlayerDodgeState(PlayerController player) : base(player) { }

        // 闪避过程不可被通用强制打断
        protected override bool CheckInterrupts() => false;

        #region State Lifecycle

        public override void Enter()
        {
            data.IsDodgeing = true;
            data.WantsToDodge = false;

            _stateDuration = 0f;
            _endTimeTriggered = false;

            // 1. 动画选择逻辑
            _selectedData = GetDodgeData();

            // 防错处理
            if (_selectedData == null || _selectedData.Clip == null)
            {
                player.StateMachine.ChangeState(player.StateRegistry.GetState<PlayerIdleState>());
                return;
            }

            // 2. 初始化驱动引擎
            player.MotionDriver.InitializeWarpData(_selectedData);

            ChooseOptionsAndPlay(_selectedData.Clip);

            // 4. 设置结束回调（如果动画自然播完会走这里）
            player.AnimFacade.SetOnEndCallback(() =>
            {
                if (_endTimeTriggered) return; // 已通过 EndTime 触发过，则忽略自然结束回调
                HandleDodgeEnd();
            });

            data.ExpectedFootPhase = _selectedData.EndPhase; // 立即设置末相位，确保动画过渡正确
        }

        protected override void UpdateStateLogic()
        {
            //闪避不需要退出逻辑
        }

        public override void PhysicsUpdate()
        {
            if (_selectedData == null) return;

            float normalizedTime = player.AnimFacade.CurrentNormalizedTime;
            player.MotionDriver.UpdateWarpMotion(normalizedTime);

            // 累计播放时长（用于 EndTime 提前触发）
            _stateDuration = player.AnimFacade.CurrentTime;

            if (!_endTimeTriggered && _selectedData.EndTime > 0f && _stateDuration >= _selectedData.EndTime)
            {
                _endTimeTriggered = true;
                HandleDodgeEnd();
                return;
            }
        }

        public override void Exit()
        {
            data.IsDodgeing = false;
            // Motion Warping 相关数据也应由 MotionDriver 统一管理
            // data.IsWarping = false; 
            // data.ActiveWarpData = null;
            data.WantsToDodge = false;

            player.MotionDriver.ClearWarpData();

            // 【规范】铁律：离开状态时，必须清理自己设置的回调
            player.AnimFacade.ClearOnEndCallback();

            _selectedData = null;
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// 根据运动状态和角度获取闪避动画数据。
        /// 修改后：不再根据冲刺选择 Move* 变体，全部使用基础 8 方向动画字段。
        /// </summary>
        private WarpedMotionData GetDodgeData()
        {
            float angle = data.DesiredLocalMoveAngle;

            const float SectorAngle = 45f;
            const float HalfSectorAngle = 22.5f;

            // 8方向判断逻辑（始终使用基础字段，而非 Move* 变体）
            if (angle > -HalfSectorAngle && angle <= HalfSectorAngle) // Fwd
                return config.Dodging.ForwardDodge;

            if (angle > HalfSectorAngle && angle <= HalfSectorAngle + SectorAngle) // Fwd-Right
                return config.Dodging.ForwardRightDodge;

            if (angle > HalfSectorAngle + SectorAngle && angle <= HalfSectorAngle + SectorAngle * 2) // Right
                return config.Dodging.RightDodge;

            if (angle > HalfSectorAngle + SectorAngle * 2 && angle <= 180f - HalfSectorAngle) // Back-Right
                return config.Dodging.BackwardRightDodge;

            if (angle > 180f - HalfSectorAngle || angle <= -180f + HalfSectorAngle) // Back
                return config.Dodging.BackwardDodge;

            if (angle > -180f + HalfSectorAngle && angle <= -HalfSectorAngle - SectorAngle * 2) // Back-Left
                return config.Dodging.BackwardLeftDodge;

            if (angle > -HalfSectorAngle - SectorAngle * 2 && angle <= -HalfSectorAngle - SectorAngle) // Left
                return config.Dodging.LeftDodge;

            if (angle > -HalfSectorAngle - SectorAngle && angle <= -HalfSectorAngle) // Fwd-Left
                return config.Dodging.ForwardLeftDodge;

            // 兜底使用左移闪避
            return config.Dodging.LeftDodge;
        }

        private void HandleDodgeEnd()
        {
            // 防重入
            if (_endTimeTriggered) _endTimeTriggered = true;

            // 闪避结束后，决定下一个状态的淡入时间
            if (data.CurrentLocomotionState == LocomotionState.Idle)
            {
                // 如果停下了，要求 Idle 缓慢淡入，使用 DodgingSO 的 AnimPlayOptions
                data.NextStatePlayOptions = config.Dodging.FadeInIdleOptions;
                player.StateMachine.ChangeState(player.StateRegistry.GetState<PlayerIdleState>());
            }
            else
            {
                // 如果还在移动，根据是否冲刺决定 MoveLoop 的淡入时间
                data.NextStatePlayOptions = config.Dodging.FadeInMoveLoopOptions;
                data.ExpectedFootPhase = _selectedData.EndPhase; // 传递末相位给 MoveLoopState
                player.StateMachine.ChangeState(player.StateRegistry.GetState<PlayerMoveLoopState>());
            }
        }

        #endregion
    }
}
