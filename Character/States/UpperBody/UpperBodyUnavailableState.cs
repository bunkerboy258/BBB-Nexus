namespace BBBNexus
{
    public class UpperBodyUnavailableState : UpperBodyBaseState
    {
        public UpperBodyUnavailableState(BBBCharacterController player) : base(player) { }

        public override void Enter()
        {
            player.AnimFacade.SetLayerWeight(1, 0f, 0.2f);

            // Do not clear equipped items here. Sprint jump -> fall may enter this state,
            // and clearing RuntimeData items breaks the return path back to HoldItem.
            player.EquipmentDriver?.MainhandItemDirector?.OnForceUnequip();
            player.EquipmentDriver?.OffhandItemDirector?.OnForceUnequip();
        }

        public override void Exit()
        {
        }

        protected override void UpdateStateLogic()
        {
        }
    }
}
