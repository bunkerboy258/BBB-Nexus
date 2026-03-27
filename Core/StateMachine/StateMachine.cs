namespace BBBNexus
{
    public class StateMachine
    {
        public BaseState CurrentState { get; private set; }
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
