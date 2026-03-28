using UnityEngine;

namespace BBBNexus
{
    // 玩家闪避状态 
    // 负责执行闪避动画和运动变形 根据移动方向选择8方向闪避 最后回到移动或空闲状态
    public class PlayerDodgeState : PlayerBaseState
    {
        // 缓存当前选中的闪避数据
        private WarpedMotionData _selectedData;

        // 累计播放时长和是否已触发 EndTime 逻辑 防止重复执行
        private float _stateDuration;
        private bool _endTimeTriggered;

        public PlayerDodgeState(BBBCharacterController player) : base(player) { }

        // 闪避状态不可被通用强制打断
        protected override bool CheckInterrupts() => false;

        // 进入状态 选择对应方向的闪避动画 初始化运动变形
        public override void Enter()
        {
            data.IsDodgeing = true;
            data.WantsToDodge = false;

            // 写入音频意图（由 AudioController 统一消费）
            data.SfxQueue.Enqueue(PlayerSfxEvent.Dodge);

            _stateDuration = 0f;
            _endTimeTriggered = false;

            // 将角色朝向 snap 到摄像机正方向
            // 使动画本地空间与摄像机空间对齐，保证四向动画方向正确
            AlignToCameraForward();

            // 根据方向选择闪避数据
            _selectedData = GetDodgeData();

            // 如果没有闪避数据 回到空闲
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
                HandleDodgeEnd();
            });

            // 记录末相位确保后续状态能正确选择脚位
            data.ExpectedFootPhase = _selectedData.EndPhase;
        }

        // 状态逻辑 闪避过程中一般不做任何中断检测
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

            if (!_endTimeTriggered && _selectedData.EndTime > 0f && _stateDuration >= _selectedData.EndTime)
            {
                _endTimeTriggered = true;
                HandleDodgeEnd();
                return;
            }
        }

        // 退出状态 清理Warp数据和回调
        public override void Exit()
        {
            data.IsDodgeing = false;
            data.WantsToDodge = false;

            player.MotionDriver.ClearWarpData();

            player.AnimFacade.ClearOnEndCallback();

            _selectedData = null;
        }

        // 将角色朝向 snap 到摄像机水平正方向
        // 调用后角色本地空间与摄像机空间对齐，四向动画方向均正确
        private void AlignToCameraForward()
        {
            if (data.CameraTransform == null) return;
            Vector3 camFwd = data.CameraTransform.forward;
            camFwd.y = 0f;
            if (camFwd.sqrMagnitude < 0.0001f) return;
            float yaw = Quaternion.LookRotation(camFwd.normalized, Vector3.up).eulerAngles.y;
            player.MotionDriver.RequestYaw(yaw, 0f);
            // override 需在本帧 LateUpdate 前生效（InitializeWarpData 依赖正确的朝向）
            // 由 OverrideState.PhysicsUpdate → UpdateGravityOnly → ConsumeYawRequest 消费
        }

        // 根据运动方向获取闪避动画数据
        // 优先匹配 8 方向；斜向槽位为空时自动退回最近的正向，兼容 4 方向配置
        // AlignToCameraForward 已将角色对齐摄像机，此处用摄像机相对角选动画即可
        private WarpedMotionData GetDodgeData()
        {
            Vector3 worldDir = data.DesiredWorldMoveDir;
            worldDir.y = 0f;

            float angle = 0f;
            if (worldDir.sqrMagnitude > 0.0001f && data.CameraTransform != null)
            {
                Vector3 camFwd = data.CameraTransform.forward;
                camFwd.y = 0f;
                if (camFwd.sqrMagnitude > 0.0001f)
                    angle = Vector3.SignedAngle(camFwd.normalized, worldDir.normalized, Vector3.up);
            }
            var d = config.Dodging;

            if (angle > -22.5f && angle <= 22.5f)
                return d.ForwardDodge;

            if (angle > 22.5f && angle <= 67.5f)
                return FallbackDodge(d.ForwardRightDodge, d.ForwardDodge, d.RightDodge);

            if (angle > 67.5f && angle <= 112.5f)
                return d.RightDodge;

            if (angle > 112.5f && angle <= 157.5f)
                return FallbackDodge(d.BackwardRightDodge, d.BackwardDodge, d.RightDodge);

            if (angle > 157.5f || angle <= -157.5f)
                return d.BackwardDodge;

            if (angle > -157.5f && angle <= -112.5f)
                return FallbackDodge(d.BackwardLeftDodge, d.BackwardDodge, d.LeftDodge);

            if (angle > -112.5f && angle <= -67.5f)
                return d.LeftDodge;

            // [-67.5°, -22.5°]
            return FallbackDodge(d.ForwardLeftDodge, d.ForwardDodge, d.LeftDodge);
        }

        private static WarpedMotionData FallbackDodge(WarpedMotionData diagonal, WarpedMotionData a, WarpedMotionData b)
            => (diagonal != null && diagonal.Clip != null) ? diagonal : (a ?? b);

        // 处理闪避结束 根据当前运动状态切回MoveLoop或Idle
        private void HandleDodgeEnd()
        {
            _endTimeTriggered = true;

            // 如果闪避结束时处于空闲 就回到空闲状态 使用闪避的淡入选项
            if (data.CurrentLocomotionState == LocomotionState.Idle)
            {
                data.NextStatePlayOptions = config.Dodging.FadeInIdleOptions;
                player.StateMachine.ChangeState(player.StateRegistry.GetState<PlayerIdleState>());
            }
            else
            {
                // 否则回到运动循环 继承末相位供MoveLoop选择脚位
                data.NextStatePlayOptions = config.Dodging.FadeInMoveLoopOptions;
                data.ExpectedFootPhase = _selectedData.EndPhase;
                player.StateMachine.ChangeState(player.StateRegistry.GetState<PlayerMoveLoopState>());
            }
        }
    }
}
