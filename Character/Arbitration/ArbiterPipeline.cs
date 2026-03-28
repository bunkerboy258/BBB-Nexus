namespace BBBNexus
{
    public class ArbiterPipeline
    {
        public LODArbiter LOD { get; private set; }
        public HealthArbiter Health { get; private set; }
        public ActionArbiter Action { get; private set; }
        public StaminaArbiter Stamina { get; private set; }
        public StatusEffectArbiter StatusEffect { get; private set; }

        public ArbiterPipeline(BBBCharacterController player)
        {
            LOD = new LODArbiter(player);
            Health = new HealthArbiter(player);
            Action = new ActionArbiter(player);
            Stamina = new StaminaArbiter(player);
            StatusEffect = new StatusEffectArbiter(player);
        }

        public void ProcessUpdateArbiters()
        {
            Action.Arbitrate();
            Health.Arbitrate();
            Stamina.Arbitrate();
            LOD.Arbitrate();
            StatusEffect.Arbitrate();
        }

        public void ProcessLateUpdateArbiters()
        {
            //
        }
    }
}