namespace Core.StateMachine
{
    // ����״̬�ĳ�����࣬������������
    public abstract class BaseState
    {
        public abstract void Enter();           // ����״̬ʱ���� (������)
        public abstract void LogicUpdate();     // ÿ֡���� (�߼��ж�)
        public abstract void PhysicsUpdate();   // ����֡���� (�ƶ�����)
        public abstract void Exit();            // �˳�״̬ʱ���� (����)
    }
}
