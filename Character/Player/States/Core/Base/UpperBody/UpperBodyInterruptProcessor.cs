using Characters.Player.Core;
using Characters.Player.States;

namespace Characters.Player.Processing
{
    public class UpperBodyInterruptProcessor
    {
        private readonly PlayerController _player;
        private readonly UpperBodyController _upperBody;

        public UpperBodyInterruptProcessor(PlayerController player, UpperBodyController upperBody)
        {
            _player = player;
            _upperBody = upperBody;
        }

        public bool TryProcessInterrupts(UpperBodyBaseState currentState)
        {
            if (_player.Config == null || _player.Config.Brain == null || _player.Config.Brain.UpperBodyInterceptors == null)
                return false;

            var pipeline = _player.Config.Brain.UpperBodyInterceptors;
            for (int i = 0; i < pipeline.Count; i++)
            {
                var interceptor = pipeline[i];
                if (interceptor != null && interceptor.TryIntercept(_player, currentState, out var nextState))
                {
                    _upperBody.StateMachine.ChangeState(nextState);
                    return true;
                }
            }
            return false;
        }
    }
}