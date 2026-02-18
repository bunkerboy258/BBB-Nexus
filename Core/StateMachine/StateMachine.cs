namespace Core.StateMachine
{
    // 状态机：负责管理并切换状态
    public class StateMachine
    {
        // 当前活跃的状态（只读）
        public BaseState CurrentState { get; private set; }

        // 初始化状态机，设置起始状态并进入该状态
        public void Initialize(BaseState startingState)
        {
            CurrentState = startingState;
            CurrentState.Enter();
        }

        public void ChangeState(BaseState newState)
        {
            CurrentState.Exit();
            CurrentState = newState;
            CurrentState.Enter();
        }
    }
}
