namespace Core.StateMachine
{
    public abstract class BaseState
    {
        public abstract void Enter();
        public abstract void LogicUpdate();
        public abstract void PhysicsUpdate();
        public abstract void Exit();     
    }
}
