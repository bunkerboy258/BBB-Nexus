using UnityEngine;

namespace BBBNexus
{
    /// <summary>
    /// 原始输入数据 - 纯硬件事实汇报，绝不包含任何手感处理
    /// </summary>
    public struct RawInputData
    {
        public Vector2 MoveAxis;
        public Vector2 LookAxis;

        // --- 持续按压状态 ---
        public bool JumpHeld;
        public bool DodgeHeld;
        public bool RollHeld;
        public bool SprintHeld;
        public bool WalkHeld;
        public bool AimHeld;
        public bool InteractHeld;

        public bool Expression1Held;
        public bool Expression2Held;
        public bool Expression3Held;
        public bool Expression4Held;

        public bool Number1Held;
        public bool Number2Held;
        public bool Number3Held;
        public bool Number4Held;
        public bool Number5Held;

        public bool ActionHeld;
        public bool LeftMouseHeld;

        // --- 硬件边沿触发 (瞬间事实) ---
        public bool JumpJustPressed;
        public bool DodgeJustPressed;
        public bool RollJustPressed;
        public bool FireJustPressed;

        public bool Expression1JustPressed;
        public bool Expression2JustPressed;
        public bool Expression3JustPressed;
        public bool Expression4JustPressed;

        public bool Number1JustPressed;
        public bool Number2JustPressed;
        public bool Number3JustPressed;
        public bool Number4JustPressed;
        public bool Number5JustPressed;

        public bool ActionJustPressed;
        public bool LeftMouseJustPressed;
    }

    /// <summary>
    /// 处理后的输入数据 - 游戏逻辑真正使用的意愿快照
    /// </summary>
    public struct ProcessedInputData
    {
        public Vector2 Move;
        public Vector2 Look;

        // --- 持续按压状态 (直接继承) ---
        public bool JumpHeld;
        public bool DodgeHeld;
        public bool RollHeld;
        public bool SprintHeld;
        public bool WalkHeld;
        public bool AimHeld;
        public bool InteractHeld;
        public bool FireHeld;

        public bool Expression1Held;
        public bool Expression2Held;
        public bool Expression3Held;
        public bool Expression4Held;

        public bool Number1Held;
        public bool Number2Held;
        public bool Number3Held;
        public bool Number4Held;
        public bool Number5Held;

        public bool ActionHeld;
        public bool LeftMouseHeld;

        // --- 核心魔法：缓存计时器 (Input Buffers) ---
        public float JumpBufferTimer;
        public float DodgeBufferTimer;
        public float RollBufferTimer;
        public float FireBufferTimer;

        public float Expression1BufferTimer;
        public float Expression2BufferTimer;
        public float Expression3BufferTimer;
        public float Expression4BufferTimer;

        public float Number1BufferTimer;
        public float Number2BufferTimer;
        public float Number3BufferTimer;
        public float Number4BufferTimer;
        public float Number5BufferTimer;

        public float ActionBufferTimer;
        public float LeftMouseBufferTimer;

        // --- 向外暴露的单帧意愿字段（由 InputPipeline 根据 BufferTimer 写入） ---
        // 之前这些是只读属性（基于 BufferTimer 计算），现在改为字段，
        // 由 InputPipeline 最终写入以便显式消费（Consume）行为更直观。
        public bool JumpPressed => JumpBufferTimer > 0f;
        public bool DodgePressed => DodgeBufferTimer > 0f;
        public bool RollPressed => RollBufferTimer > 0f;
        public bool FirePressed => FireBufferTimer > 0f;

        public bool Expression1Pressed => Expression1BufferTimer > 0f;
        public bool Expression2Pressed => Expression2BufferTimer > 0f;
        public bool Expression3Pressed => Expression3BufferTimer > 0f;
        public bool Expression4Pressed => Expression4BufferTimer > 0f;

        public bool Number1Pressed => Number1BufferTimer > 0f;
        public bool Number2Pressed => Number2BufferTimer > 0f;
        public bool Number3Pressed => Number3BufferTimer > 0f;
        public bool Number4Pressed => Number4BufferTimer > 0f;
        public bool Number5Pressed => Number5BufferTimer > 0f;

        public bool ActionPressed => ActionBufferTimer > 0f;
        public bool LeftMousePressed => LeftMouseBufferTimer > 0f;
    }

    public struct FrameInputData
    {
        public ulong FrameIndex;
        public RawInputData Raw;
        public ProcessedInputData Processed;
    }

    public class InputData
    {
        public FrameInputData currentFrameData;
        public FrameInputData lastFrameData;
    }
}