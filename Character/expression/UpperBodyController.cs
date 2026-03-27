namespace BBBNexus
{
    // 上半身分层控制器
    // 管理上半身的独立状态机 中央点是处理装备 瞄准 与攻击等上半身行为
    // 使用遮罩确保只影响特定骨骼 与主状态机并行运行 互不干扰
    public class UpperBodyController
    {
        private BBBCharacterController _player;

        public StateMachine StateMachine { get; private set; }
        public UpperBodyStateRegistry StateRegistry { get; private set; }
        public UpperBodyInterruptProcessor InterruptProcessor { get; private set; }

        public UpperBodyController(BBBCharacterController player)
        {
            _player = player;
            // 实例化独立的状态机 与全身状态机完全隔离
            StateMachine = new StateMachine();

            StateRegistry = new UpperBodyStateRegistry();
            InterruptProcessor = new UpperBodyInterruptProcessor(player, this);

            // 从配置的 BrainSO 加载所有上半身状态
            if (player.Config != null && player.Config.Brain != null)
            {
                StateRegistry.InitializeFromBrain(player.Config.Brain, player);
            }
        }

        // 每帧调用一次 由 PlayerController 在主逻辑更新后执行
        // 只是简单地驱动当前上半身状态的逻辑更新 没有额外的侧通道管理
        public void Update()
        {
            if (_player != null && _player.RuntimeData != null && _player.RuntimeData.Arbitration.BlockUpperBody)
                return;

            StateMachine.CurrentState?.LogicUpdate();
        }
    }
}