using UnityEngine;

namespace BBBNexus
{
    // 玩家翻滚状态 
    // 负责执行翻滚动画和运动变形 根据移动方向选择8方向翻滚 最后回到移动或空闲状态
    public class PlayerRollState : PlayerBaseState
    {
        // 缓存当前选中的翻滚数据
        private WarpedMotionData _selectedData;

        // 累计播放时长和是否已触发 EndTime 逻辑 防止重复执行
        private float _stateDuration;
        private bool _endTimeTriggered;

        public PlayerRollState(BBBCharacterController player) : base(player) { }

        // 翻滚状态不可被通用强制打断
        protected override bool CheckInterrupts() => false;

        // 进入状态 选择对应方向的翻滚动画 初始化运动变形
        public override void Enter()
        {
            data.WantsToRoll = false;

            // 写入音频意图（由 AudioController 统一消费）
            data.SfxQueue.Enqueue(PlayerSfxEvent.Roll);

            _stateDuration = 0f;
            _endTimeTriggered = false;

            // 根据方向选择翻滚数据
            _selectedData = GetRollData();

            // 如果没有翻滚数据 回到空闲
            if (_selectedData == null || _selectedData.Clip == null)
            {
                player.StateMachine.ChangeState(player.StateRegistry.GetState<PlayerIdleState>());
                return;
            }

            // 初始化运动变形
            player.MotionDriver.InitializeWarpData(_selectedData);

            ChooseOptionsAndPlay(_selectedData.Clip);

            // 设置结束回调 如果提前触发EndTime则忽略此回调
            player.AnimFacade.SetOnEndCallback(() =>
            {
                if (_endTimeTriggered) return;
                HandleRollEnd();
            });

            // 记录末相位确保后续状态能正确选择脚位
            data.ExpectedFootPhase = _selectedData.EndPhase;
        }

        // 状态逻辑 翻滚过程中一般不做任何中断检测
        protected override void UpdateStateLogic()
        {
        }

        // 物理更新 计算运动变形时间 驱动Warp运动
        public override void PhysicsUpdate()
        {
            if (_selectedData == null) return;

            float normalizedTime = player.AnimFacade.CurrentNormalizedTime;
            player.MotionDriver.UpdateWarpMotion(normalizedTime);

            // 累计播放时长 检查是否到达 EndTime 提前切换
            _stateDuration = player.AnimFacade.CurrentTime;

            if (!_endTimeTriggered && _selectedData.EndTime > 0f && _stateDuration >= _selectedData.EndTime && data.CurrentLocomotionState != LocomotionState.Idle)
            {
                _endTimeTriggered = true;
                HandleRollEnd();
                return;
            }
        }

        // 退出状态 清理Warp数据和回调
        public override void Exit()
        {
            data.WantsToRoll = false;

            player.MotionDriver.ClearWarpData();

            player.AnimFacade.ClearOnEndCallback();

            _selectedData = null;
        }

        // 根据方向获取翻滚动画数据 8方向量化
        private WarpedMotionData GetRollData()
        {
            float angle = data.DesiredLocalMoveAngle;

            const float SectorAngle = 45f;
            const float HalfSectorAngle = 22.5f;

            // 8方向判断逻辑
            if (angle > -HalfSectorAngle && angle <= HalfSectorAngle)
                return config.Rolling.ForwardRoll;

            if (angle > HalfSectorAngle && angle <= HalfSectorAngle + SectorAngle)
                return config.Rolling.ForwardRightRoll;

            if (angle > HalfSectorAngle + SectorAngle && angle <= HalfSectorAngle + SectorAngle * 2)
                return config.Rolling.RightRoll;

            if (angle > HalfSectorAngle + SectorAngle * 2 && angle <= 180f - HalfSectorAngle)
                return config.Rolling.BackwardRightRoll;

            if (angle > 180f - HalfSectorAngle || angle <= -180f + HalfSectorAngle)
                return config.Rolling.BackwardRoll;

            if (angle > -180f + HalfSectorAngle && angle <= -HalfSectorAngle - SectorAngle * 2)
                return config.Rolling.BackwardLeftRoll;

            if (angle > -HalfSectorAngle - SectorAngle * 2 && angle <= -HalfSectorAngle - SectorAngle)
                return config.Rolling.LeftRoll;

            if (angle > -HalfSectorAngle - SectorAngle && angle <= -HalfSectorAngle)
                return config.Rolling.ForwardLeftRoll;

            // 兜底使用左翻滚
            return config.Rolling.LeftRoll;
        }

        // 处理翻滚结束 根据当前运动状态切回MoveLoop或Idle
        private void HandleRollEnd()
        {
            _endTimeTriggered = true;

            // 如果翻滚结束时处于空闲 就回到空闲状态 使用翻滚的淡入选项
            if (data.CurrentLocomotionState == LocomotionState.Idle)
            {
                data.NextStatePlayOptions = config.Rolling.FadeInIdleOptions;
                player.StateMachine.ChangeState(player.StateRegistry.GetState<PlayerIdleState>());
            }
            else
            {
                // 否则回到运动循环 继承末相位供MoveLoop选择脚位
                data.NextStatePlayOptions = config.Rolling.FadeInMoveLoopOptions;
                data.ExpectedFootPhase = _selectedData.EndPhase;
                player.StateMachine.ChangeState(player.StateRegistry.GetState<PlayerMoveLoopState>());
            }
        }
    }
}
