using Core.StateMachine;
using Characters.Player.Core;
using Characters.Player.Processing;

namespace Characters.Player.States
{
    public class UpperBodyController
    {
        private PlayerController _player;

        public StateMachine StateMachine { get; private set; }
        public UpperBodyStateRegistry StateRegistry { get; private set; }
        public UpperBodyInterruptProcessor InterruptProcessor { get; private set; }

        public UpperBodyController(PlayerController player)
        {
            _player = player;
            StateMachine = new StateMachine();

            // 1. 初始化注册表和处理器
            StateRegistry = new UpperBodyStateRegistry();
            InterruptProcessor = new UpperBodyInterruptProcessor(player, this);

            // 2. 从 BrainSO 加载状态
            if (player.Config != null && player.Config.Brain != null)
            {
                StateRegistry.InitializeFromBrain(player.Config.Brain, player);
            }

            // 3. 启动状态机 (取列表第0个)
            if (StateRegistry.InitialState != null)
            {
                StateMachine.Initialize(StateRegistry.InitialState);
            }
        }

        public void Update()
        {
            StateMachine.CurrentState?.LogicUpdate();
        }
    }
}