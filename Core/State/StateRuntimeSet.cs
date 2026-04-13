using System;
using System.Collections.Generic;

namespace BBBNexus
{
    /// <summary>
    /// 运行时状态集合。
    /// 统一包装一组 StateRuntimeBound，避免在外部裸传 List。
    /// </summary>
    public sealed class StateRuntimeSet
    {
        private readonly Dictionary<string, StateRuntimeBound> _states;

        private StateRuntimeSet(Dictionary<string, StateRuntimeBound> states)
        {
            _states = states;
        }

        public IReadOnlyDictionary<string, StateRuntimeBound> States => _states;

        public static StateRuntimeSet CreateFromProfile(StateProfileSO profile)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            var map = new Dictionary<string, StateRuntimeBound>(StringComparer.Ordinal);
            var defs = profile.Definitions;
            for (int i = 0; i < defs.Count; i++)
            {
                var def = defs[i];
                if (def == null || string.IsNullOrWhiteSpace(def.Key))
                {
                    continue;
                }

                if (map.ContainsKey(def.Key))
                {
                    throw new InvalidOperationException($"Duplicate state key in profile '{profile.name}': {def.Key}");
                }

                map.Add(def.Key, new StateRuntimeBound(def));
            }

            return new StateRuntimeSet(map);
        }

        public bool TryGetBound(string key, out StateRuntimeBound bound)
        {
            bound = null;
            return !string.IsNullOrWhiteSpace(key) && _states.TryGetValue(key, out bound);
        }

        public bool TryGetCurrent(string key, out double value)
        {
            value = 0d;
            if (!TryGetBound(key, out var bound))
            {
                return false;
            }

            value = bound.Current;
            return true;
        }

        public bool TrySet(string key, double value)
        {
            if (!TryGetBound(key, out var bound))
            {
                return false;
            }

            bound.Set(value);
            return true;
        }

        public bool TryAdd(string key, double delta)
        {
            if (!TryGetBound(key, out var bound))
            {
                return false;
            }

            bound.Add(delta);
            return true;
        }

        public void ResetAllToDefault()
        {
            foreach (var pair in _states)
            {
                pair.Value.ResetToDefault();
            }
        }
    }
}
