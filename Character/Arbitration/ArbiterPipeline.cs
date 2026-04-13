namespace BBBNexus
{
    public class ArbiterPipeline
    {
        private readonly BBBCharacterController _player;

        public CharacterArbiter Character { get; private set; }
        public LODArbiter LOD { get; private set; }
        public HealthArbiter Health { get; private set; }
        public ActionArbiter Action { get; private set; }
        public StaminaArbiter Stamina { get; private set; }
        public StatusEffectArbiter StatusEffect { get; private set; }

        public ArbiterPipeline(BBBCharacterController player)
        {
            _player = player;
            Character = new CharacterArbiter(player);
            LOD = new LODArbiter(player);
            Health = new HealthArbiter(player);
            Action = new ActionArbiter(player);
            Stamina = new StaminaArbiter(player);
            StatusEffect = new StatusEffectArbiter(player);
        }

        public void ProcessUpdateArbiters()
        {
            _player.RuntimeData?.ResetArbitrationFrameFlags();
            Action.Arbitrate();
            Health.Arbitrate();
            Stamina.Arbitrate();
            LOD.Arbitrate();
            StatusEffect.Arbitrate();
            Character.Arbitrate();
        }

        public void ProcessLateUpdateArbiters()
        {
            //
        }
    }
}
