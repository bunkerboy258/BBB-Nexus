using System;
using Characters.Player.Arbitration;
using Characters.Player.Data;

namespace Characters.Player.States.Override
{
    [Serializable]
    public sealed class PlayerDeathState : Characters.Player.States.PlayerBaseState
    {
        public PlayerDeathState(PlayerController player) : base(player) { }

        protected override bool CheckInterrupts() => false;

        public override void Enter()
        {
            data.IsDead = true;
            data.Arbitration.IsDead = true;
            data.Arbitration.BlockInput = true;
            data.Arbitration.BlockUpperBody = true;
            data.Arbitration.BlockFacial = true;
            data.Arbitration.BlockIK = true;
            data.Arbitration.BlockInventory = true;

            var clip = config?.Core?.DeathAnim;
            if (clip != null)
                AnimFacade.PlayFullBodyAction(clip, 0.05f);
        }

        protected override void UpdateStateLogic()
        {
        }

        public override void PhysicsUpdate()
        {
            player.MotionDriver.UpdateGravityOnly();
        }

        public override void Exit()
        {
            AnimFacade.StopFullBodyAction();
        }
    }
}
