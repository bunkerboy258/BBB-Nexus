using System;
using System.Collections.Generic;
using UnityEngine;

namespace BBBNexus
{
    /// <summary>
    /// 状态配置集合（模板层）。
    /// </summary>
    [CreateAssetMenu(fileName = "StateProfile", menuName = "BBBNexus/State/State Profile")]
    public sealed class StateProfileSO : ScriptableObject
    {
        [SerializeField] private List<StateDefinitionSO> _definitions = new();

        public IReadOnlyList<StateDefinitionSO> Definitions => _definitions;

        public StateRuntimeSet CreateRuntimeSet()
        {
            return StateRuntimeSet.CreateFromProfile(this);
        }

        public bool HasDuplicateKeys(out string duplicateKey)
        {
            duplicateKey = null;
            var set = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < _definitions.Count; i++)
            {
                var def = _definitions[i];
                if (def == null || string.IsNullOrWhiteSpace(def.Key))
                {
                    continue;
                }

                if (!set.Add(def.Key))
                {
                    duplicateKey = def.Key;
                    return true;
                }
            }

            return false;
        }
    }
}
