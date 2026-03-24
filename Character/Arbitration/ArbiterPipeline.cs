namespace BBBNexus
{
    public class ArbiterPipeline
    {
        public LODArbiter LOD { get; private set; }
        public HealthArbiter Health { get; private set; }
        public ActionArbiter Action { get; private set; }
        public StaminaArbiter Stamina { get; private set; }

        private readonly BBBCharacterController _player;

        public ArbiterPipeline(BBBCharacterController player)
        {
            _player = player;
            LOD = new LODArbiter(player);
            Health = new HealthArbiter(player);
            Action = new ActionArbiter(player);
            Stamina = new StaminaArbiter(player);
        }

        public void ProcessUpdateArbiters()
        {
            Action.Arbitrate();
            Health.Arbitrate();
            Stamina.Arbitrate();

            if (_player == null || _player.EnableLODArbiter)
                LOD.Arbitrate();
        }

        public void ProcessLateUpdateArbiters()
        {
            //
        }
    }
}