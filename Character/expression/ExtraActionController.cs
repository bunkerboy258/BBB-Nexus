using UnityEngine;

namespace BBBNexus
{
    /// <summary>
    /// 额外动作控制器 — 处理扩展行为输入（如闭眼、特殊交互等）
    /// 架构模式：纯 C# 类，由 BBBCharacterController 在 Awake 实例化，Update 调用
    ///
    /// 设计说明：
    /// - 使用独立的 ExtraAction1-4 输入槽位（与 Expression 系统完全分离）
    /// - ExtraAction1：闭眼交互（Toggle 状态）
    /// - ExtraAction2-4：预留未来扩展（如特殊技能、情境动作等）
    ///
    /// 与 Expression 系统的区别：
    /// - Expression：专用于面部表情/动画（按键 6789）
    /// - ExtraAction：专用于游戏逻辑/特殊交互（独立绑定）
    ///
    /// 扩展方式：
    /// - 新增额外动作时，添加新的 ProcessExtraActionX 方法
    /// - 在 WantsExtraActionX 中存储意图
    /// - 在 ExtraActionController 中添加具体实现
    /// </summary>
    public class ExtraActionController
    {
        private readonly PlayerRuntimeData _runtimeData;
        private readonly EyesClosedSystemManager _eyesClosedManager;

        // 状态追踪（边沿检测）
        private bool _lastExtraAction1;
        private bool _lastExtraAction2;
        private bool _lastExtraAction3;
        private bool _lastExtraAction4;

        public ExtraActionController(PlayerRuntimeData runtimeData, EyesClosedSystemManager eyesClosedManager)
        {
            _runtimeData = runtimeData;
            _eyesClosedManager = eyesClosedManager;
        }

        public void Update()
        {
            // 检测 ExtraAction1-4 的边沿触发（从 false 到 true 的跳变）
            ProcessExtraAction1();
            ProcessExtraAction2();
            ProcessExtraAction3();
            ProcessExtraAction4();
        }

        /// <summary>
        /// ExtraAction1：闭眼交互（Toggle 状态）
        /// </summary>
        private void ProcessExtraAction1()
        {
            bool currentIntent = _runtimeData.WantsExtraAction1;
            if (currentIntent && !_lastExtraAction1)
            {
                // Toggle 闭眼状态
                _eyesClosedManager?.ForceSetEyesClosed(!_eyesClosedManager.IsEyesClosed);
            }
            _lastExtraAction1 = currentIntent;
        }

        /// <summary>
        /// ExtraAction2：预留扩展
        /// </summary>
        private void ProcessExtraAction2()
        {
            bool currentIntent = _runtimeData.WantsExtraAction2;
            if (currentIntent && !_lastExtraAction2)
            {
                // TODO: 未来扩展其他额外动作
                Debug.Log("[ExtraActionController] ExtraAction2 触发（预留）");
            }
            _lastExtraAction2 = currentIntent;
        }

        /// <summary>
        /// ExtraAction3：预留扩展
        /// </summary>
        private void ProcessExtraAction3()
        {
            bool currentIntent = _runtimeData.WantsExtraAction3;
            if (currentIntent && !_lastExtraAction3)
            {
                // TODO: 未来扩展其他额外动作
                Debug.Log("[ExtraActionController] ExtraAction3 触发（预留）");
            }
            _lastExtraAction3 = currentIntent;
        }

        /// <summary>
        /// ExtraAction4：预留扩展
        /// </summary>
        private void ProcessExtraAction4()
        {
            bool currentIntent = _runtimeData.WantsExtraAction4;
            if (currentIntent && !_lastExtraAction4)
            {
                // TODO: 未来扩展其他额外动作
                Debug.Log("[ExtraActionController] ExtraAction4 触发（预留）");
            }
            _lastExtraAction4 = currentIntent;
        }
    }
}
