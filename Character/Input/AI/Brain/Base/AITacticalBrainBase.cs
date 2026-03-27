using System;
using Unity.VisualScripting.FullSerializer;
using UnityEngine;

namespace BBBNexus
{
    [Serializable]
    public abstract class AITacticalBrainBase : IAITacticalBrain
    {
        protected Transform _selfTransform;
        protected TacticalIntent _currentIntent;
        protected AITacticalBrainConfigSO _config;

        public virtual void Initialize(Transform selfTransform, AITacticalBrainConfigSO config)
        {
            _selfTransform = selfTransform;
            _config = config;
            _currentIntent = new TacticalIntent(Vector2.zero, Vector2.zero, false, false, false, false, false);
        }

        public ref readonly TacticalIntent EvaluateTactics(in NavigationContext context)
        {
            if (!context.HasValidTarget || _selfTransform == null)
            {
                _currentIntent = new TacticalIntent(Vector2.zero, Vector2.zero, false, false, false, false, false);
                return ref _currentIntent;
            }

            ProcessTactics(in context);
            return ref _currentIntent;
        }

        protected abstract void ProcessTactics(in NavigationContext context);

        protected Vector2 ConvertWorldDirToJoystick(Vector3 worldDir)
        {
            Vector3 localDir = _selfTransform.InverseTransformDirection(worldDir);
            return new Vector2(localDir.x, localDir.z).normalized;
        }

        protected Vector2 CalculateLookInput(Vector3 worldTargetDir)
        {
            if (worldTargetDir == Vector3.zero) return Vector2.zero;

            Vector3 flatForward = Vector3.ProjectOnPlane(_selfTransform.forward, Vector3.up).normalized;
            Vector3 flatTarget = Vector3.ProjectOnPlane(worldTargetDir, Vector3.up).normalized;
            float yawAngle = Vector3.SignedAngle(flatForward, flatTarget, Vector3.up);

            float yawInput = Mathf.Clamp(yawAngle * 0.05f, -1f, 1f);

            return new Vector2(yawInput, 0f);
        }
    }
}