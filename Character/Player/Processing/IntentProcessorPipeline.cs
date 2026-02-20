using Characters.Player.Core;
using Characters.Player.Processing;

namespace Characters.Player.Processing
{
    /// <summary>
    /// 管理所有输入后处理与逻辑生成的统一管道。
    /// </summary>
    public class IntentProcessorPipeline
    {
        // 核心处理器
        private readonly LocomotionIntentProcessor _locomotionIntentProcessor;
        private readonly AimIntentProcessor _aimIntentProcessor;
        private readonly EquipIntentProcessor _equipIntentProcessor;
        private readonly IKIntentProcessor _ikIntentProcessor;
        private readonly JumpOrVaultIntentProcessor _jumpOrVaultIntentProcessor;

        // 参数生成器
        private readonly MovementParameterProcessor _movementParameterProcessor;
        private readonly ViewRotationProcessor _viewRotationProcessor;

        public IntentProcessorPipeline(PlayerController player)
        {
            // 初始化顺序
            _viewRotationProcessor = new ViewRotationProcessor(player);
            _equipIntentProcessor = new EquipIntentProcessor(player);
            _aimIntentProcessor = new AimIntentProcessor(player);
            _locomotionIntentProcessor = new LocomotionIntentProcessor(player);
            _jumpOrVaultIntentProcessor = new JumpOrVaultIntentProcessor(player);

            _movementParameterProcessor = new MovementParameterProcessor(player);
            _ikIntentProcessor = new IKIntentProcessor(player);
        }

        public void update()
        {
            UpdateIntentProcessors();
            UpdateParameterProcessors();
        }

        /// <summary>
        /// 更新所有意图逻辑（第一阶段：输入 -> 逻辑意图）。
        /// 顺序：视角参考系 -> 装备状态 -> 瞄准状态 -> 运动意图（含移动/跑/跳/翻）。
        /// </summary>
        public void UpdateIntentProcessors()
        {
            // 1. 确定权威旋转参考系（AuthorityYaw/Pitch）
            _viewRotationProcessor.Update();

            // 2. 处理装备切换意图
            _equipIntentProcessor.Update();

            // 3. 处理瞄准状态意图
            _aimIntentProcessor.Update();

            // 4. 处理最终的运动方向与行为意图（依赖上述所有状态）
            _locomotionIntentProcessor.Update();

            // 5. 处理跳跃/翻越意图
            _jumpOrVaultIntentProcessor.Update();
        }

        /// <summary>
        /// 更新所有派生参数（第二阶段：逻辑意图 -> 表现层参数）。
        /// 顺序：动画混合参数 -> IK 权重与目标。
        /// </summary>
        public void UpdateParameterProcessors()
        {
            // 1. 根据运动意图计算动画混合参数 (BlendX, BlendY)
            _movementParameterProcessor.Update();

            // 2. 根据运动和动作状态计算 IK 目标
            _ikIntentProcessor.Update();
        }

        // --- 对外公开引用，供特殊初始化或调试使用 ---
        public LocomotionIntentProcessor Locomotion => _locomotionIntentProcessor;
        public EquipIntentProcessor Equip => _equipIntentProcessor;
        public AimIntentProcessor Aim => _aimIntentProcessor;
        public JumpOrVaultIntentProcessor JumpOrVault => _jumpOrVaultIntentProcessor;
    }
}