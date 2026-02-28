using UnityEngine;
using Characters.Player.Data;
using Characters.Player.Animation;

namespace Characters.Player.States
{
    /// <summary>
    /// 玩家翻滚状态类
    /// 职责：
    /// 1. 根据运动状态和量化方向选择翻滚动画；
    /// 2. 利用 MotionDriver 的 Warped 模式驱动精准位移；
    /// 3. 在动作结束时平滑切回移动或空闲。
    /// 
    /// 与 DodgeState 的区别：
    /// - DodgeState：快速闪避，低体力消耗
    /// - RollState：较长的翻滚动作，高体力消耗，适合躲避范围攻击
    /// </summary>
    public class PlayerRollState : PlayerBaseState
    {
        // 缓存当前选中的翻滚数据
        private WarpedMotionData _selectedData;

        // 跟踪播放时长以及是否已按 EndTime 触发结束逻辑，防止重复触发
        private float _stateDuration;
        private bool _endTimeTriggered;

        public PlayerRollState(PlayerController player) : base(player) { }

        // 翻滚过程不可被通用强制打断
        protected override bool CheckInterrupts() => false;

        #region State Lifecycle

        public override void Enter()
        {
            data.WantsToRoll = false;

            _stateDuration = 0f;
            _endTimeTriggered = false;

            // 1. 动画选择逻辑
            _selectedData = GetRollData();

            // 防错处理
            if (_selectedData == null || _selectedData.Clip == null)
            {
                player.StateMachine.ChangeState(player.IdleState);
                return;
            }

            // 2. 初始化驱动引擎
            player.MotionDriver.InitializeWarpData(_selectedData);

            ChooseOptionsAndPlay(_selectedData.Clip);

            // 3. 设置结束回调（如果动画自然播完会走这里）
            player.AnimFacade.SetOnEndCallback(() =>
            {
                if (_endTimeTriggered) return; // 已通过 EndTime 触发过，则忽略自然结束回调
                HandleRollEnd();
            });

            data.ExpectedFootPhase = _selectedData.EndPhase; // 立即设置末相位，确保动画过渡正确
        }

        protected override void UpdateStateLogic()
        {
            //翻滚不需要主动退出逻辑
        }

        public override void PhysicsUpdate()
        {
            if (_selectedData == null) return;

            float normalizedTime = player.AnimFacade.CurrentNormalizedTime;
            player.MotionDriver.UpdateWarpMotion(normalizedTime);

            // 累计播放时长（用于 EndTime 提前触发）
            _stateDuration = player.AnimFacade.CurrentTime;

            if (!_endTimeTriggered && _selectedData.EndTime > 0f && _stateDuration >= _selectedData.EndTime&&data.CurrentLocomotionState!=LocomotionState.Idle)
            {
                _endTimeTriggered = true;
                HandleRollEnd();
                return;
            }
        }

        public override void Exit()
        {
            // Motion Warping 相关数据由 MotionDriver 统一管理
            data.WantsToRoll = false;

            player.MotionDriver.ClearWarpData();

            // 【规范】铁律：离开状态时，必须清理自己设置的回调
            player.AnimFacade.ClearOnEndCallback();

            _selectedData = null;
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// 根据角度获取翻滚动画数据。
        /// 逻辑与 DodgeState 相同，使用基础 8 方向的翻滚动画字段。
        /// </summary>
        private WarpedMotionData GetRollData()
        {
            float angle = data.DesiredLocalMoveAngle;

            const float SectorAngle = 45f;
            const float HalfSectorAngle = 22.5f;

            // 8方向判断逻辑
            if (angle > -HalfSectorAngle && angle <= HalfSectorAngle) // Fwd
                return config.Rolling.ForwardRoll;

            if (angle > HalfSectorAngle && angle <= HalfSectorAngle + SectorAngle) // Fwd-Right
                return config.Rolling.ForwardRightRoll;

            if (angle > HalfSectorAngle + SectorAngle && angle <= HalfSectorAngle + SectorAngle * 2) // Right
                return config.Rolling.RightRoll;

            if (angle > HalfSectorAngle + SectorAngle * 2 && angle <= 180f - HalfSectorAngle) // Back-Right
                return config.Rolling.BackwardRightRoll;

            if (angle > 180f - HalfSectorAngle || angle <= -180f + HalfSectorAngle) // Back
                return config.Rolling.BackwardRoll;

            if (angle > -180f + HalfSectorAngle && angle <= -HalfSectorAngle - SectorAngle * 2) // Back-Left
                return config.Rolling.BackwardLeftRoll;

            if (angle > -HalfSectorAngle - SectorAngle * 2 && angle <= -HalfSectorAngle - SectorAngle) // Left
                return config.Rolling.LeftRoll;

            if (angle > -HalfSectorAngle - SectorAngle && angle <= -HalfSectorAngle) // Fwd-Left
                return config.Rolling.ForwardLeftRoll;

            // 兜底使用左翻滚
            return config.Rolling.LeftRoll;
        }

        private void HandleRollEnd()
        {
            // 防重入
            if (_endTimeTriggered) _endTimeTriggered = true;

            // 翻滚结束后，决定下一个状态的淡入时间
            if (data.CurrentLocomotionState == LocomotionState.Idle)
            {
                // 如果停下了，要求 Idle 缓慢淡入，使用 RollSO 的 AnimPlayOptions
                data.NextStatePlayOptions = config.Rolling.FadeInIdleOptions;
                player.StateMachine.ChangeState(player.IdleState);
            }
            else
            {
                // 如果还在移动，要求 MoveLoop 缓慢淡入
                data.NextStatePlayOptions = config.Rolling.FadeInMoveLoopOptions;
                data.ExpectedFootPhase = _selectedData.EndPhase; // 传递末相位给 MoveLoopState
                player.StateMachine.ChangeState(player.MoveLoopState);
            }
        }

        #endregion
    }
}
