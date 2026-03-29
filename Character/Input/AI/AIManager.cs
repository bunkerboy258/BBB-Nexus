using System;
using System.Linq;
using UnityEngine;

namespace BBBNexus
{
    public static class AIManager
    {
        public static AIBlackboardInputAdapter GetAdapter(BBBCharacterController actor)
        {
            if (actor == null)
            {
                Debug.LogError("[AIManager] Actor is null.");
                return null;
            }

            if (actor.InputSourceRef is AIBlackboardInputAdapter adapterFromInput)
            {
                return adapterFromInput;
            }

            var adapter = actor.GetComponentInChildren<AIBlackboardInputAdapter>(true);
            if (adapter == null)
            {
                Debug.LogError($"[AIManager] No AIBlackboardInputAdapter found on actor '{actor.name}'.");
            }

            return adapter;
        }

        public static IAITacticalBrain ResolveBrain(string brainTypeName)
        {
            if (string.IsNullOrWhiteSpace(brainTypeName))
            {
                Debug.LogError("[AIManager] Brain type name is empty.");
                return null;
            }

            var type = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(SafeGetTypes)
                .FirstOrDefault(candidate =>
                    candidate != null &&
                    !candidate.IsAbstract &&
                    typeof(IAITacticalBrain).IsAssignableFrom(candidate) &&
                    (string.Equals(candidate.Name, brainTypeName, StringComparison.Ordinal) ||
                     string.Equals(candidate.FullName, brainTypeName, StringComparison.Ordinal)));

            if (type == null)
            {
                Debug.LogError($"[AIManager] Tactical brain type not found: {brainTypeName}");
                return null;
            }

            try
            {
                return Activator.CreateInstance(type) as IAITacticalBrain;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AIManager] Failed to create tactical brain '{brainTypeName}': {ex.Message}");
                return null;
            }
        }

        public static AITacticalBrainConfigSO ResolveTacticalConfig(string globalId)
        {
            if (string.IsNullOrWhiteSpace(globalId))
            {
                Debug.LogError("[AIManager] Tactical config id is empty.");
                return null;
            }

            var config = MetaLib.GetObject<AITacticalBrainConfigSO>(globalId);
            if (config == null)
            {
                Debug.LogError($"[AIManager] Failed to resolve AITacticalBrainConfigSO from MetaLib id '{globalId}'.");
            }

            return config;
        }

        public static bool SetBrain(BBBCharacterController actor, string brainTypeName)
        {
            var adapter = GetAdapter(actor);
            var brain = ResolveBrain(brainTypeName);
            if (adapter == null || brain == null)
            {
                return false;
            }

            adapter.ConfigureBrain(brain);
            return true;
        }

        public static bool SetTacticalConfig(BBBCharacterController actor, string configId)
        {
            var adapter = GetAdapter(actor);
            var config = ResolveTacticalConfig(configId);
            if (adapter == null || config == null)
            {
                return false;
            }

            adapter.ConfigureTacticalConfig(config);
            return true;
        }

        public static bool SetTarget(BBBCharacterController actor, Transform target)
        {
            var adapter = GetAdapter(actor);
            if (adapter == null || target == null)
            {
                return false;
            }

            adapter.ConfigureTarget(target);
            return true;
        }

        private static Type[] SafeGetTypes(System.Reflection.Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch
            {
                return Array.Empty<Type>();
            }
        }
    }
}
