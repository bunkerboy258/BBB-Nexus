using UnityEngine;
using Animancer;
using Characters.Player.Data;

namespace Characters.Player.States
{
    public class PlayerVaultState : PlayerBaseState
    {
        private AnimancerState _state;
        private float _stateDuration;
        private float _startYaw;

        public PlayerVaultState(PlayerController player) : base(player) { }

        // 翻越过程不可被通用强制打断。
        protected override bool CheckInterrupts() => false;

        public override void Enter()
        {
            Debug.Log("Entered Vault State");
            _stateDuration = 0f;
            _startYaw = player.transform.eulerAngles.y;

            data.IsVaulting = true;

            var clipData = config.VaultFenceAnim;
            if (clipData == null || clipData.Clip.Clip == null)
            {
                player.StateMachine.ChangeState(player.IdleState);
                return;
            }

            _state = player.Animancer.Layers[0].Play(clipData.Clip);

            _state.Events(this).OnEnd = () =>
            {
                if (data.MoveInput.sqrMagnitude > 0.01f)
                    player.StateMachine.ChangeState(player.MoveLoopState);
                else
                    player.StateMachine.ChangeState(player.IdleState);
            };
        }

        protected override void UpdateStateLogic()
        {
            // 翻越过程不可打断
        }

        public override void PhysicsUpdate()
        {
            if (_state == null) return;

            _stateDuration += Time.deltaTime * _state.Speed;

            player.MotionDriver.UpdateMotion(
                config.VaultFenceAnim,
                _stateDuration,
                _startYaw
            );
        }

        public override void Exit()
        {
            _state = null;
            data.IsVaulting = false;
        }
    }
}
