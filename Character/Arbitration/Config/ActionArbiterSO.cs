using UnityEngine;

namespace BBBNexus
{
    [CreateAssetMenu(fileName = "ActionArbiter_Default", menuName = "BBBNexus/Player/Modules/Action Arbiter")]
    public class ActionArbiterSO : ScriptableObject
    {
        [Tooltip("预留：Action 域规则配置会逐步从旧 OverrideState 迁移到这里。")]
        [TextArea]
        public string Notes;
    }
}
