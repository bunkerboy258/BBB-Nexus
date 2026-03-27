using UnityEngine;

namespace BBBNexus
{
    /// <summary>
    /// 输入源基类 - 所有输入源的抽象基类
    /// 提供统一的序列化接口 支持在 Unity 编辑器中拖拽赋值
    /// 所有具体输入源(如PlayerInputReader、AI输入源等)都应继承此类
    /// </summary>
    public abstract class InputSourceBase : MonoBehaviour, IInputSource
    {
        [Header("Input Timing Settings")]
        [Tooltip("WASD 等移动轴的防抖缓存时间（秒），用于抖动抑制）")]
        public float InputFlickerBuffer = 0.05f;

        [Tooltip("动作按键的缓存时间（秒），按下后该按键在此时间内被视为已按下，便于输入缓冲）")]
        public float ActionBufferTime = 0.2f;

        protected PlayerRuntimeData _runtimeData;

        protected virtual void Awake()
        {
            var player = GetComponentInParent<BBBCharacterController>();
            if (player != null) _runtimeData = player.RuntimeData;
        }

        /// <summary>
        /// 由具体实现类重写 负责获取原始输入数据
        /// </summary>
        /// <param name="rawData">用于存储原始输入的结构体引用</param>
        public abstract void FetchRawInput(ref RawInputData rawData);

        public bool IsBlocked => _runtimeData != null && _runtimeData.Arbitration.BlockInput;
    }
}
