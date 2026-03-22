
namespace BBBNexus
{
    // 上半身分层控制器
    // 管理上半身的独立状态机 中央点是处理装备 瞄准 与攻击等上半身行为
    // 使用遮罩确保只影响特定骨骼 与主状态机并行运行 互不干扰
    public class UpperBodyController
    {
        private PlayerController _player;

        // 上半身的专属状态机 驱动装备 瞄准 攻击等动作的流转
        public StateMachine StateMachine { get; private set; }
        // 上半身状态的注册表 从 BrainSO 加载初始状态与所有可用的上半身状态
        public UpperBodyStateRegistry StateRegistry { get; private set; }
        // 上半身的全局打断处理器 负责检测何时能进入特定状态 例如装备检查等
        public UpperBodyInterruptProcessor InterruptProcessor { get; private set; }

        public UpperBodyController(PlayerController player)
        {
            _player = player;
            // 实例化独立的状态机 与全身状态机完全隔离
            StateMachine = new StateMachine();

            // 初始化注册表和打断处理器 这两个是状态机能运作的必要基础
            StateRegistry = new UpperBodyStateRegistry();
            InterruptProcessor = new UpperBodyInterruptProcessor(player, this);

            // 从配置的 BrainSO 加载所有上半身状态 反射或枚举映射都可以
            // 必须在启动状态机前完成 否则初始化会失败
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