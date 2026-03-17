using UnityEngine;

namespace BBBNexus
{
    public interface IAITacticalBrain
    {
        void Initialize(Transform selfTransform, AITacticalBrainConfigSO config);

        ref readonly TacticalIntent EvaluateTactics(in NavigationContext context);
    }
}