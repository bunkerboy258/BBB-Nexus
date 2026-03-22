using UnityEngine;

namespace BBBNexus
{
    // 玩家翻越状态 
    // 负责执行翻越障碍物的动画和运动变形 根据输入或高度自动选择低翻或高翻 
    public class PlayerVaultState : PlayerBaseState
    {
        private float _stateDuration;
        private bool _endTimeTriggered;
        private WarpedMotionData _selectedWarpData;

        public PlayerVaultState(PlayerController player) : base(player) { }

        // 翻越状态不允许被通用强制打断 避免反复进入退出
        protected override bool CheckInterrupts() => false;

        // 进入状态 根据意图或高度选择翻越动画 初始化运动变形
        public override void Enter()
        {
            //Debug.Log("Entered Vault State");
            _stateDuration = 0f;
            data.IsVaulting = true;
            _endTimeTriggered = false;

            // 根据明确的意图选择翻越动画
            if (data.WantsLowVault && config.Vaulting.lowVaultAnim != null)
            {
                _selectedWarpData = config.Vaulting.lowVaultAnim;
            }
            else if (data.WantsHighVault && config.Vaulting.highVaultAnim != null)
            {
                _selectedWarpData = config.Vaulting.highVaultAnim;
            }
            else
            {
                // 没有明确意图 根据高度自动选择
                Debug.LogWarning("No explicit vault intent, falling back to height-based selection.");
                if (data.CurrentVaultInfo.IsValid)
                {
                    float h = data.CurrentVaultInfo.Height;
                    if (h >= 0.5f && h < 1.2f && config.Vaulting.lowVaultAnim != null)
                        _selectedWarpData = config.Vaulting.lowVaultAnim;
                    else if (h >= 1.2f && h <= 2.5f && config.Vaulting.highVaultAnim != null)
                        _selectedWarpData = config.Vaulting.highVaultAnim;
                    else
                        _selectedWarpData = null;
                }
                else
                {
                    _selectedWarpData = null;
                }
            }

            // 清空一次性意图
            data.WantsLowVault = false;
            data.WantsHighVault = false;
            data.WantsToVault = false;

            // 如果没有选中动画 直接回到空闲
            if (_selectedWarpData == null || _selectedWarpData.Clip == null)
            {
                player.StateMachine.ChangeState(player.StateRegistry.GetState<PlayerIdleState>());
                return;
            }

            VaultObstacleInfo info = data.CurrentVaultInfo;

            Vector3[] warpTargets = new Vector3[]
            {
                info.LedgePoint,
                info.ExpectedLandPoint
            };

            ChooseOptionsAndPlay(_selectedWarpData.Clip);

            // 初始化 Motion Warping
            player.MotionDriver.InitializeWarpData(_selectedWarpData, warpTargets);

            data.IsWarping = true;
            data.ActiveWarpData = _selectedWarpData;
            data.NormalizedWarpTime = 0f;

            // 注入手部IK目标
            data.WarpIKTarget_LeftHand = data.CurrentVaultInfo.LeftHandPos;
            data.WarpIKTarget_RightHand = data.CurrentVaultInfo.RightHandPos;
            data.WarpIKRotation_Hand = data.CurrentVaultInfo.HandRot;

            AnimFacade.SetOnEndCallback(() =>
            {
                if (data.CurrentLocomotionState != LocomotionState.Idle)
                {
                    data.NextStatePlayOptions = config.Vaulting.VaultToMoveOptions;
                    player.StateMachine.ChangeState(player.StateRegistry.GetState<PlayerMoveLoopState>());
                }
                else
                {
                    data.NextStatePlayOptions = config.Vaulting.VaultToIdleOptions;
                    player.StateMachine.ChangeState(player.StateRegistry.GetState<PlayerIdleState>());
                }
            });
        }

        // 状态逻辑 翻越过程中一般不做任何中断检测
        protected override void UpdateStateLogic()
        {
        }

        // 物理更新 计算运动变形时间 驱动Warp运动
        public override void PhysicsUpdate()
        {
            if (_selectedWarpData == null) return;

            float normalizedTime = Mathf.Clamp01(AnimFacade.CurrentNormalizedTime);
            data.NormalizedWarpTime = normalizedTime;

            // 累计播放时长 用于 EndTime 检测
            _stateDuration = AnimFacade.CurrentTime;

            // 检测是否可以提前切回运动循环
            if (!_endTimeTriggered && data.CurrentLocomotionState != LocomotionState.Idle &&
                _selectedWarpData.EndTime > 0f && _stateDuration >= _selectedWarpData.EndTime)
            {
                _endTimeTriggered = true;
                if (data.MoveInput.sqrMagnitude > 0.01f)
                {
                    data.NextStatePlayOptions = AnimPlayOptions.Default;
                    player.StateMachine.ChangeState(player.StateRegistry.GetState<PlayerMoveLoopState>());
                }
                else
                {
                    player.StateMachine.ChangeState(player.StateRegistry.GetState<PlayerIdleState>());
                }
                return;
            }

            // 驱动运动变形
            player.MotionDriver.UpdateWarpMotion(normalizedTime);
        }

        // 退出状态 清理Warp数据和回调
        public override void Exit()
        {
            AnimFacade.ClearOnEndCallback();

            data.IsVaulting = false;
            data.IsWarping = false;
            data.ActiveWarpData = null;

            player.MotionDriver.ClearWarpData();
            _selectedWarpData = null;
        }
    }
}
