using UnityEngine;

namespace BBBNexus
{
    [CreateAssetMenu(fileName = "StatusArbiter_Default", menuName = "BBBNexus/Player/Modules/Status Arbiter")]
    public class StatusArbiterSO : ScriptableObject
    {
        [Tooltip("预留：Status 域规则配置会逐步从旧 StatusEffectState 迁移到这里。")]
        [TextArea]
        public string Notes;
    }
}
