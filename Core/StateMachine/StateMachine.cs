namespace Core.StateMachine
{
    // ״̬��������������״̬�л�
    public class StateMachine
    {
        public BaseState CurrentState { get; private set; }

        // ��ʼ��
        public void Initialize(BaseState startingState)
        {
            CurrentState = startingState;
            CurrentState.Enter();
        }

        // �л�״̬���˳���״̬ -> ��ֵ -> ������״̬
        public void ChangeState(BaseState newState)
        {
            CurrentState.Exit();
            CurrentState = newState;
            CurrentState.Enter();
        }
    }
}
