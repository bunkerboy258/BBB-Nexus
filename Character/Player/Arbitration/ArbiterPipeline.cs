using BBBNexus;

namespace BBBNexus
{
    public class ArbiterPipeline
    {
        public LODArbiter LOD { get; private set; }
        public HealthArbiter Health { get; private set; }
        public ActionArbiter Action { get; private set; } 

        public ArbiterPipeline(PlayerController player)
        {
            LOD = new LODArbiter(player);
            Health = new HealthArbiter(player);
            Action = new ActionArbiter(player); 
        }

        public void ProcessUpdateArbiters()
        {
            Action.Arbitrate();
            Health.Arbitrate();
            LOD.Arbitrate();
        }

        public void ProcessLateUpdateArbiters()
        {
            Health.Arbitrate();
        }
    }
}