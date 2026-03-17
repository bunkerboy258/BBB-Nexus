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

        // 新增：计算视角/转身摇杆输入
        protected Vector2 CalculateLookInput(Vector3 worldTargetDir)
        {
            if (worldTargetDir == Vector3.zero) return Vector2.zero;

            // 计算自身正前方与目标方向在水平面上的夹角
            Vector3 flatForward = Vector3.ProjectOnPlane(_selfTransform.forward, Vector3.up).normalized;
            Vector3 flatTarget = Vector3.ProjectOnPlane(worldTargetDir, Vector3.up).normalized;
            float yawAngle = Vector3.SignedAngle(flatForward, flatTarget, Vector3.up);

            // P控制器逻辑：将角度差映射为 [-1, 1] 的摇杆输入
            // 乘以 0.05f 意味着：如果偏差达到 20 度以上，直接推满摇杆全速转身；小于 20 度时平滑减速
            float yawInput = Mathf.Clamp(yawAngle * 0.05f, -1f, 1f);

            // 当前仅处理水平转向(Yaw)，如果你的3A控制器需要AI抬头/低头，可同理计算Pitch
            return new Vector2(yawInput, 0f);
        }
    }
}