using UnityEngine;

namespace BBBNexus
{
    /// <summary>
    /// 系统流转的第一道关卡 唯一的数据生产者
    /// 数据流向：从 IInputSource (硬件) 提取脏数据 - 栈上清洗 (防抖/缓存) - 压入堆内存黑板 (InputData)
    /// 拥有绝对的写入权限。对外部仅暴露只读引用，外部系统只能通过 Consume 接口进行受控的数据核销
    /// </summary>
    public class InputPipeline
    {
        // 输入源接口（现在使用具体基类以便读取序列化配置）
        private readonly InputSourceBase _inputSource;

        // 0 GC 堆内存容器，在构造时预分配，全生命周期复用
        private InputData _inputData;

        // 栈上瞬时数据缓存
        private RawInputData _rawData;
        private Vector2 _bufferedMove;
        private float _lastNonZeroMoveTime;

        // 配置参数
        // WASD指令缓存时间
        private readonly float _inputFlickerBuffer;
        // 动作指令缓存时间  将原来单帧的硬件触发 拉长为 N 秒的合法“意愿期”
        private readonly float _actionBufferTime;

        // 全局物理帧计数器
        private ulong _frameIndex;

        /// <summary>
        /// 对外暴露的当前输入快照
        /// 其他系统只能读取此引用 绝对禁止直接修改内部字段
        /// </summary>
        public InputData Current => _inputData;

        // 构造函数只接受 InputSourceBase，其他配置从 inputSource 注入；如果 inputSource 未配置则使用其字段的默认值
        public InputPipeline(InputSourceBase inputSource)
        {
            _inputSource = inputSource;
            _inputFlickerBuffer = _inputSource.InputFlickerBuffer;
            _actionBufferTime = _inputSource.ActionBufferTime;

            // 管线作为数据的绝对源头 自行分配容器 避免GC
            _inputData = new InputData();
            _inputData.currentFrameData = new FrameInputData { FrameIndex = 0 };
            _inputData.lastFrameData = new FrameInputData { FrameIndex = 0 };

            _rawData = default;
            _bufferedMove = Vector2.zero;
            _lastNonZeroMoveTime = Time.time;
            _frameIndex = 0;
        }

        /// <summary>
        /// playercontroller Update 最优先调用的函数
        /// 负责历史快照更迭 并拉起一轮新的硬件数据采样
        /// </summary>
        public void Update()
        {
            // 推进历史帧
            _inputData.lastFrameData = _inputData.currentFrameData;

            if (_inputSource != null && _inputSource.IsBlocked)
            {
                _rawData = default;
            }
            else
            {
                // 采样硬件真实状态
                _inputSource.FetchRawInput(ref _rawData);
            }

            // 后处理数据并压入内存
            ProcessRawInput();

            _frameIndex++;
        }

        /// <summary>
        /// 输入数据的后处理方法
        /// </summary>
        private void ProcessRawInput()
        {
            var currentFrame = new FrameInputData
            {
                FrameIndex = _frameIndex,
                Raw = _rawData,
                Processed = default
            };

            // 输入轴防抖处理
            if (_rawData.MoveAxis.sqrMagnitude > 0.01f)
            {
                _bufferedMove = _rawData.MoveAxis;
                _lastNonZeroMoveTime = Time.time;
                currentFrame.Processed.Move = _rawData.MoveAxis;
            }
            else if (Time.time - _lastNonZeroMoveTime < _inputFlickerBuffer)
            {
                // 处于防抖窗口内 使用缓存的最后一次有效值
                currentFrame.Processed.Move = _bufferedMove;
            }
            else
            {
                currentFrame.Processed.Move = Vector2.zero;
            }

            // 注：LookAxis 属于 Delta (增量) 数据，绝对禁止在此处 SmoothDamp，直接原样透传给摄像机逻辑 不然视角会像弹簧一样回到原位
            currentFrame.Processed.Look = _rawData.LookAxis;

            //  持续按压状态的继承
            currentFrame.Processed.JumpHeld = _rawData.JumpHeld;
            currentFrame.Processed.DodgeHeld = _rawData.DodgeHeld;
            currentFrame.Processed.RollHeld = _rawData.RollHeld;
            currentFrame.Processed.SprintHeld = _rawData.SprintHeld;
            currentFrame.Processed.WalkHeld = _rawData.WalkHeld;
            currentFrame.Processed.AimHeld = _rawData.AimHeld;
            currentFrame.Processed.InteractHeld = _rawData.InteractHeld;

            currentFrame.Processed.LeftMouseHeld = _rawData.LeftMouseHeld;
            currentFrame.Processed.FireHeld = _rawData.LeftMouseHeld;

            currentFrame.Processed.Expression1Held = _rawData.Expression1Held;
            currentFrame.Processed.Expression2Held = _rawData.Expression2Held;
            currentFrame.Processed.Expression3Held = _rawData.Expression3Held;
            currentFrame.Processed.Expression4Held = _rawData.Expression4Held;

            currentFrame.Processed.Number1Held = _rawData.Number1Held;
            currentFrame.Processed.Number2Held = _rawData.Number2Held;
            currentFrame.Processed.Number3Held = _rawData.Number3Held;
            currentFrame.Processed.Number4Held = _rawData.Number4Held;
            currentFrame.Processed.Number5Held = _rawData.Number5Held;

            currentFrame.Processed.ActionHeld = _rawData.ActionHeld;

            //  动作缓存池调度
            // 核心机制：一旦硬件触发 JustPressed 给对应的Timer充能 随后随时间衰减。
            // 外部读取的 bool Pressed 是依赖此 Timer 的计算属性
            float dt = Time.deltaTime;
            var lastProc = _inputData.lastFrameData.Processed;

            float UpdateBuffer(float lastTimer, bool justPressed)
            {
                float newTimer = Mathf.Max(0f, lastTimer - dt);
                if (justPressed) newTimer = _actionBufferTime;
                return newTimer;
            }

            currentFrame.Processed.JumpBufferTimer = UpdateBuffer(lastProc.JumpBufferTimer, _rawData.JumpJustPressed);
            currentFrame.Processed.DodgeBufferTimer = UpdateBuffer(lastProc.DodgeBufferTimer, _rawData.DodgeJustPressed);
            currentFrame.Processed.RollBufferTimer = UpdateBuffer(lastProc.RollBufferTimer, _rawData.RollJustPressed);

            currentFrame.Processed.LeftMouseBufferTimer = UpdateBuffer(lastProc.LeftMouseBufferTimer, _rawData.LeftMouseJustPressed);
            currentFrame.Processed.FireBufferTimer = currentFrame.Processed.LeftMouseBufferTimer;

            currentFrame.Processed.Expression1BufferTimer = UpdateBuffer(lastProc.Expression1BufferTimer, _rawData.Expression1JustPressed);
            currentFrame.Processed.Expression2BufferTimer = UpdateBuffer(lastProc.Expression2BufferTimer, _rawData.Expression2JustPressed);
            currentFrame.Processed.Expression3BufferTimer = UpdateBuffer(lastProc.Expression3BufferTimer, _rawData.Expression3JustPressed);
            currentFrame.Processed.Expression4BufferTimer = UpdateBuffer(lastProc.Expression4BufferTimer, _rawData.Expression4JustPressed);

            currentFrame.Processed.Number1BufferTimer = UpdateBuffer(lastProc.Number1BufferTimer, _rawData.Number1JustPressed);
            currentFrame.Processed.Number2BufferTimer = UpdateBuffer(lastProc.Number2BufferTimer, _rawData.Number2JustPressed);
            currentFrame.Processed.Number3BufferTimer = UpdateBuffer(lastProc.Number3BufferTimer, _rawData.Number3JustPressed);
            currentFrame.Processed.Number4BufferTimer = UpdateBuffer(lastProc.Number4BufferTimer, _rawData.Number4JustPressed);
            currentFrame.Processed.Number5BufferTimer = UpdateBuffer(lastProc.Number5BufferTimer, _rawData.Number5JustPressed);

            currentFrame.Processed.ActionBufferTimer = UpdateBuffer(lastProc.ActionBufferTimer, _rawData.ActionJustPressed);

            // 将局部计算完毕的纯净数据 一次性写回堆内存 供全局读取
            _inputData.currentFrameData = currentFrame;
        }

        // 消费仲裁接口 
        // IntentProcessor (意图仲裁) 或 State在动作确立时调用。
        // 调用后 Timer 瞬间归零 配合 实现同帧内核销。

        public void ConsumeJumpPressed() { var f = _inputData.currentFrameData; f.Processed.JumpBufferTimer = 0f; _inputData.currentFrameData = f; }
        public void ConsumeDodgePressed() { var f = _inputData.currentFrameData; f.Processed.DodgeBufferTimer = 0f; _inputData.currentFrameData = f; }
        public void ConsumeRollPressed() { var f = _inputData.currentFrameData; f.Processed.RollBufferTimer = 0f; _inputData.currentFrameData = f; }

        public void ConsumeFirePressed() => ConsumeLeftMousePressed();

        public void ConsumeExpression1Pressed() { var f = _inputData.currentFrameData; f.Processed.Expression1BufferTimer = 0f; _inputData.currentFrameData = f; }
        public void ConsumeExpression2Pressed() { var f = _inputData.currentFrameData; f.Processed.Expression2BufferTimer = 0f; _inputData.currentFrameData = f; }
        public void ConsumeExpression3Pressed() { var f = _inputData.currentFrameData; f.Processed.Expression3BufferTimer = 0f; _inputData.currentFrameData = f; }
        public void ConsumeExpression4Pressed() { var f = _inputData.currentFrameData; f.Processed.Expression4BufferTimer = 0f; _inputData.currentFrameData = f; }

        public void ConsumeNumber1Pressed() { var f = _inputData.currentFrameData; f.Processed.Number1BufferTimer = 0f; _inputData.currentFrameData = f; }
        public void ConsumeNumber2Pressed() { var f = _inputData.currentFrameData; f.Processed.Number2BufferTimer = 0f; _inputData.currentFrameData = f; }
        public void ConsumeNumber3Pressed() { var f = _inputData.currentFrameData; f.Processed.Number3BufferTimer = 0f; _inputData.currentFrameData = f; }
        public void ConsumeNumber4Pressed() { var f = _inputData.currentFrameData; f.Processed.Number4BufferTimer = 0f; _inputData.currentFrameData = f; }
        public void ConsumeNumber5Pressed() { var f = _inputData.currentFrameData; f.Processed.Number5BufferTimer = 0f; _inputData.currentFrameData = f; }

        public void ConsumeActionPressed() { var f = _inputData.currentFrameData; f.Processed.ActionBufferTimer = 0f; _inputData.currentFrameData = f; }

        // legacy alias (if any code still calls old name, keep a thin forwarder)
        public void ConsumeWavePressed() => ConsumeActionPressed();

        public void ConsumeLeftMousePressed() { var f = _inputData.currentFrameData; f.Processed.LeftMouseBufferTimer = 0f; _inputData.currentFrameData = f; }
    }
}