using UnityEngine;
using Characters.Player.Data;
using Characters.Player.Animation;

namespace Characters.Player.States
{
    public class PlayerVaultState : PlayerBaseState
    {
        private float _stateDuration;
        private bool _endTimeTriggered;
        private WarpedMotionData _selectedWarpData;

        public PlayerVaultState(PlayerController player) : base(player) { }

        protected override bool CheckInterrupts() => false;

        public override void Enter()
        {
            Debug.Log("Entered Vault State");
            _stateDuration = 0f;
            data.IsVaulting = true;
            _endTimeTriggered = false;

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

            data.WantsLowVault = false;
            data.WantsHighVault = false;
            data.WantsToVault = false;

            if (_selectedWarpData == null || _selectedWarpData.Clip == null)
            {
                player.StateMachine.ChangeState(player.IdleState);
                return;
            }

            VaultObstacleInfo info = data.CurrentVaultInfo;

            Vector3[] warpTargets = new Vector3[]
            {
                info.LedgePoint,
                info.ExpectedLandPoint
            };

            // 播放翻越动画
            var options = AnimPlayOptions.Default;
            options.NormalizedTime = 0f;

            // 优先使用新的 PlayOptions 覆写
            if (data.NextStatePlayOptions.HasValue)
            {
                var playOpt = data.NextStatePlayOptions.Value;
                // 保持 NormalizedTime=0 unless overridden explicitly
                if (!playOpt.NormalizedTime.HasValue)
                    playOpt.NormalizedTime = 0f;

                options = playOpt;
                data.NextStatePlayOptions = null;
            }

            AnimFacade.PlayTransition(_selectedWarpData.Clip, options);

            // 初始化 Motion Warping
            player.MotionDriver.InitializeWarpData(_selectedWarpData, warpTargets);

            data.IsWarping = true;
            data.ActiveWarpData = _selectedWarpData;
            data.NormalizedWarpTime = 0f;

            data.WarpIKTarget_LeftHand = data.CurrentVaultInfo.LeftHandPos;
            data.WarpIKTarget_RightHand = data.CurrentVaultInfo.RightHandPos;
            data.WarpIKRotation_Hand = data.CurrentVaultInfo.HandRot;

            AnimFacade.SetOnEndCallback(() =>
            {
                if (data.MoveInput.sqrMagnitude > 0.01f)
                {
                    data.NextStatePlayOptions = AnimPlayOptions.Default;
                    player.StateMachine.ChangeState(player.MoveLoopState);
                }
                else
                {
                    player.StateMachine.ChangeState(player.IdleState);
                }
            });
        }

        protected override void UpdateStateLogic()
        {
        }

        public override void PhysicsUpdate()
        {
            if (_selectedWarpData == null) return;

            float normalizedTime = Mathf.Clamp01(AnimFacade.CurrentNormalizedTime);
            data.NormalizedWarpTime = normalizedTime;

            // 累计播放时长（用于 EndTime 检测）
            _stateDuration = AnimFacade.CurrentTime;

            if (!_endTimeTriggered && data.CurrentLocomotionState != LocomotionState.Idle &&
                _selectedWarpData.EndTime > 0f && _stateDuration >= _selectedWarpData.EndTime)
            {
                _endTimeTriggered = true;
                if (data.MoveInput.sqrMagnitude > 0.01f)
                {
                    data.NextStatePlayOptions = AnimPlayOptions.Default;
                    player.StateMachine.ChangeState(player.MoveLoopState);
                }
                else
                {
                    player.StateMachine.ChangeState(player.IdleState);
                }
                return;
            }

            player.MotionDriver.UpdateWarpMotion(normalizedTime);
        }

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
