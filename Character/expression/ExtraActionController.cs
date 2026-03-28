using UnityEngine;

namespace BBBNexus
{
    /// <summary>
    /// 额外动作控制器 — 处理扩展行为输入（如闭眼、特殊交互等）
    /// 架构模式：纯 C# 类，由 BBBCharacterController 在 Awake 实例化，Update 调用
    /// 
    /// 设计说明：
    /// - 使用 Expression5-8 输入槽位作为额外动作触发器
    /// - Expression5：闭眼交互（Toggle 状态）
    /// - Expression6-8：预留未来扩展（如特殊技能、情境动作等）
    /// 
    /// 扩展方式：
    /// - 新增额外动作时，在 ExtraActionType 枚举中添加新类型
    /// - 在 ExtraActionConfigSO 中配置每个动作的行为参数
    /// - 在 ProcessExtraAction 中添加具体实现
    /// </summary>
    public class ExtraActionController
    {
        private readonly PlayerRuntimeData _runtimeData;
        private readonly EyesClosedSystemManager _eyesClosedManager;

        // 状态追踪
        private bool _lastExpression5;
        private bool _lastExpression6;
        private bool _lastExpression7;
        private bool _lastExpression8;

        public ExtraActionController(PlayerRuntimeData runtimeData, EyesClosedSystemManager eyesClosedManager)
        {
            _runtimeData = runtimeData;
            _eyesClosedManager = eyesClosedManager;
        }

        public void Update()
        {
            // 检测 Expression5-8 的边沿触发（从 false 到 true 的跳变）
            ProcessExpression5();
            ProcessExpression6();
            ProcessExpression7();
            ProcessExpression8();
        }

        /// <summary>
        /// Expression5：闭眼交互（Toggle 状态）
        /// </summary>
        private void ProcessExpression5()
        {
            bool currentIntent = _runtimeData.WantsExpression5;
            if (currentIntent && !_lastExpression5)
            {
                // Toggle 闭眼状态
                _eyesClosedManager?.ForceSetEyesClosed(!_eyesClosedManager.IsEyesClosed);
            }
            _lastExpression5 = currentIntent;
        }

        /// <summary>
        /// Expression6：预留扩展
        /// </summary>
        private void ProcessExpression6()
        {
            bool currentIntent = _runtimeData.WantsExpression6;
            if (currentIntent && !_lastExpression6)
            {
                // TODO: 未来扩展其他额外动作
                Debug.Log("[ExtraActionController] Expression6 触发（预留）");
            }
            _lastExpression6 = currentIntent;
        }

        /// <summary>
        /// Expression7：预留扩展
        /// </summary>
        private void ProcessExpression7()
        {
            bool currentIntent = _runtimeData.WantsExpression7;
            if (currentIntent && !_lastExpression7)
            {
                // TODO: 未来扩展其他额外动作
                Debug.Log("[ExtraActionController] Expression7 触发（预留）");
            }
            _lastExpression7 = currentIntent;
        }

        /// <summary>
        /// Expression8：预留扩展
        /// </summary>
        private void ProcessExpression8()
        {
            bool currentIntent = _runtimeData.WantsExpression8;
            if (currentIntent && !_lastExpression8)
            {
                // TODO: 未来扩展其他额外动作
                Debug.Log("[ExtraActionController] Expression8 触发（预留）");
            }
            _lastExpression8 = currentIntent;
        }
    }
}
