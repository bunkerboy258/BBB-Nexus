using UnityEngine;

namespace BBBNexus
{
    // 核心后处理管线
    // 将后处理的输入数据转为角色意图
    // 从平级的InputPipeline提取栈上后处理输入数据快照指针 -> 翻译为意图 -> 写入堆内黑板
    public class MainProcessorPipeline
    {
        // 外部注入的平级输入管线
        private readonly InputPipeline _inputPipeline;

        // 目标写入黑板与配置
        private readonly PlayerRuntimeData _runtimeData;
        private readonly PlayerSO _config;

        // 持有各意图处理器
        private readonly LocomotionIntentProcessor _locomotionIntentProcessor;
        private readonly AimIntentProcessor _aimIntentProcessor;
        private readonly JumpOrVaultIntentProcessor _jumpOrVaultIntentProcessor;
        private readonly EojIntentProcessor _eojIntentProcessor;
        private readonly HotbarIntentProcessor _hotbarIntentProcessor;
        private readonly ActionIntentProcessor _actionIntentProcessor;

        // 持有各参数处理器
        private readonly MovementParameterProcessor _movementParameterProcessor;
        private readonly ViewRotationProcessor _viewRotationProcessor;
        public MainProcessorPipeline(BBBCharacterController player, InputPipeline inputPipeline)
        {
            _inputPipeline = inputPipeline; // 重构 不再自己 new 接收外部独立运行的输入管线
            _runtimeData = player.RuntimeData;
            _config = player.Config;

            _aimIntentProcessor = new AimIntentProcessor(_runtimeData);
            _locomotionIntentProcessor = new LocomotionIntentProcessor(_runtimeData, _config);
            _jumpOrVaultIntentProcessor = new JumpOrVaultIntentProcessor(_runtimeData, _config, player.transform);
            _eojIntentProcessor = new EojIntentProcessor(_runtimeData, _inputPipeline);
            _hotbarIntentProcessor = new HotbarIntentProcessor(_runtimeData);
            _actionIntentProcessor = new ActionIntentProcessor(_runtimeData);

            _movementParameterProcessor = new MovementParameterProcessor(_runtimeData, _config, player.transform);
            _viewRotationProcessor = new ViewRotationProcessor(_runtimeData, _config);
        }


        // 翻译机集群遍历
        public void UpdateIntentProcessors()
        {
            // 注:ref readonly 的语义上只读引用 让编译器强制禁止在 Processor 内部对 inputSnapshot 的任何修改行为
            // 同时避免INPUTDATA的复制开销 
            ref readonly ProcessedInputData inputSnapshot = ref _inputPipeline.Current.currentFrameData.Processed;

            _viewRotationProcessor.Update(in inputSnapshot);
            _aimIntentProcessor.Update(in inputSnapshot);
            _locomotionIntentProcessor.Update(in inputSnapshot);
            _jumpOrVaultIntentProcessor.Update(in inputSnapshot);
            _eojIntentProcessor.Update(in inputSnapshot);
            _hotbarIntentProcessor.Update(in inputSnapshot);
            _actionIntentProcessor.Update(in inputSnapshot);
        }

        // 参数降维与混合树驱动运算
        // 此阶段已彻底脱离 Input 快照 纯粹基于黑板中的 Intent 意图 进行数学运算
        public void UpdateParameterProcessors()
        {
            _movementParameterProcessor.Update();
        }
    }
}