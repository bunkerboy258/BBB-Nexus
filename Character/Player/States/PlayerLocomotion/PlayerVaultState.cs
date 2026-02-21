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
        private bool _endTimeTriggered; // 标记是否已根据 EndTime 提前退出

        // 【关键修改】：类型变更为 WarpedMotionData
        private WarpedMotionData _selectedWarpData;

        public PlayerVaultState(PlayerController player) : base(player) { }

        // 翻越过程不可被通用强制打断。
        protected override bool CheckInterrupts() => false;

        public override void Enter()
        {
            Debug.Log("Entered Vault State");
            _stateDuration = 0f;
            _startYaw = player.transform.eulerAngles.y;
            data.IsVaulting = true;
            _endTimeTriggered = false; // 重置提前退出标记

            // --- 1. 选择动画 ---
            // 注意：您需要在 PlayerSO 中将 lowVaultAnim 和 highVaultAnim 改为 WarpedMotionData 类型
            if (data.WantsLowVault && config.lowVaultAnim != null)
            {
                _selectedWarpData = config.lowVaultAnim;
            }
            else if (data.WantsHighVault && config.highVaultAnim != null)
            {
                _selectedWarpData = config.highVaultAnim;
            }
            else
            {
                Debug.LogWarning("No explicit vault intent, falling back to height-based selection.");
                if (data.CurrentVaultInfo.IsValid)
                {
                    float h = data.CurrentVaultInfo.Height;
                    if (h >= 0.5f && h < 1.2f && config.lowVaultAnim != null)
                        _selectedWarpData = config.lowVaultAnim;
                    else if (h >= 1.2f && h <= 2.5f && config.highVaultAnim != null)
                        _selectedWarpData = config.highVaultAnim;
                    else
                        _selectedWarpData = null;
                }
                else
                {
                    _selectedWarpData = null;
                }
            }

            // 消费意图
            data.WantsLowVault = false;
            data.WantsHighVault = false;
            data.WantsToVault = false;

            if (_selectedWarpData == null || _selectedWarpData.Clip == null)
            {
                player.StateMachine.ChangeState(player.IdleState);
                return;
            }

            // --- 2. 准备扭曲目标点 ---
            // 假设烘焙数据里有 2 个特征点 (例如：0.4接触墙沿，1.0翻越落地)
            // 我们必须提供对应数量的世界坐标！
            VaultObstacleInfo info = data.CurrentVaultInfo;

            // 构建目标点数组。注意：这里的点必须和您在烘焙器中定义的 WarpPoints 顺序和数量绝对一致！
            // TODO: 这里需要确保 info 里包含了 ExpectedLandPoint (目前您的处理器可能还没算)
            Vector3[] warpTargets = new Vector3[]
            {
                info.LedgePoint,
                info.ExpectedLandPoint // 如果您还没写这个，可以暂时写一个 info.LedgePoint + player.transform.forward * 1f; 用于测试
            };

            // --- 3. 播放动画与初始化引擎 ---
            _state = player.Animancer.Layers[0].Play(_selectedWarpData.Clip);

            // 告诉 MotionDriver：接下来我要开始高级空间扭曲了
            player.MotionDriver.InitializeWarpData(_selectedWarpData, warpTargets);


            // 1. 触发通用 Warp 状态
            data.IsWarping = true;
            data.ActiveWarpData = _selectedWarpData;
            data.NormalizedWarpTime = 0f;

            // 2. 将翻越特有的“环境感知点”转化为通用的“Warp IK 目标”
            // 以后如果是“处决状态”，这里填的就是敌人的脖子坐标
            data.WarpIKTarget_LeftHand = data.CurrentVaultInfo.LeftHandPos;
            data.WarpIKTarget_RightHand = data.CurrentVaultInfo.RightHandPos;
            data.WarpIKRotation_Hand = data.CurrentVaultInfo.HandRot;

            // --- 4. 结束回调 ---
            _state.Events(this).OnEnd = () =>
            {
                if (data.MoveInput.sqrMagnitude > 0.01f)
                { 
                    data.loopFadeInTime = 0.4f;
                    player.StateMachine.ChangeState(player.MoveLoopState);
                }
                else
                    player.StateMachine.ChangeState(player.IdleState);
            };

        }

        protected override void UpdateStateLogic()
        {
            // 翻越过程不可打断
            // 可以在这里处理手部 IK 的吸附逻辑 (基于 _state.NormalizedTime)
        }

        public override void PhysicsUpdate()
        {
            if (_state == null || _selectedWarpData == null) return;

            // 计算归一化时间 (0.0 ~ 1.0)
            float normalizedTime = _state.NormalizedTime;
            data.NormalizedWarpTime = Mathf.Clamp01(_state.NormalizedTime);

            // 防止动画结束时传入大于 1 的值导致计算越界
            normalizedTime = Mathf.Clamp01(normalizedTime);

            // 累计播放时长（用于 EndTime 检测）
            _stateDuration += Time.deltaTime * _state.Speed;

            // 如果配置了 EndTime 并且已达到，提前结束翻越（行为与 PlayerLandState 保持一致）
            if (!_endTimeTriggered  && data.CurrentLocomotionState != LocomotionState.Idle&& _selectedWarpData.EndTime > 0f && _stateDuration >= _selectedWarpData.EndTime)
            {
                _endTimeTriggered = true;
                if (data.MoveInput.sqrMagnitude > 0.01f)
                {
                    data.loopFadeInTime = 0.4f;
                    player.StateMachine.ChangeState(player.MoveLoopState);
                }
                else
                {
                    player.StateMachine.ChangeState(player.IdleState);
                }
                return;
            }

            // 【关键修改】：主动调用专门的 UpdateWarpMotion
            // 不再使用单曲线的 UpdateMotion
            player.MotionDriver.UpdateWarpMotion(normalizedTime);


        }

        public override void Exit()
        {
            _state = null;
            data.IsVaulting = false;
            data.IsWarping = false;
            data.ActiveWarpData = null;
            player.MotionDriver.ClearWarpData();
            // 【关键修改】：清理扭曲数据，交还控制权
        }
    }
}
