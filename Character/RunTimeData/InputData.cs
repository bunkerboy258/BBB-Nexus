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
        public bool PrimaryAttackHeld;
        public bool SecondaryAttackHeld;

        public bool Expression1Held;
        public bool Expression2Held;
        public bool Expression3Held;
        public bool Expression4Held;
        public bool Expression5Held;
        public bool Expression6Held;
        public bool Expression7Held;
        public bool Expression8Held;

        public bool Number1Held;
        public bool Number2Held;
        public bool Number3Held;
        public bool Number4Held;
        public bool Number5Held;

        // --- 硬件边沿触发 (瞬间事实) ---
        public bool JumpJustPressed;
        public bool DodgeJustPressed;
        public bool RollJustPressed;
        public bool InteractJustPressed;
        public bool PrimaryAttackJustPressed;
        public bool SecondaryAttackJustPressed;

        public bool Expression1JustPressed;
        public bool Expression2JustPressed;
        public bool Expression3JustPressed;
        public bool Expression4JustPressed;
        public bool Expression5JustPressed;
        public bool Expression6JustPressed;
        public bool Expression7JustPressed;
        public bool Expression8JustPressed;

        public bool Number1JustPressed;
        public bool Number2JustPressed;
        public bool Number3JustPressed;
        public bool Number4JustPressed;
        public bool Number5JustPressed;
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
        public bool PrimaryAttackHeld;
        public bool SecondaryAttackHeld;

        public bool Expression1Held;
        public bool Expression2Held;
        public bool Expression3Held;
        public bool Expression4Held;
        public bool Expression5Held;
        public bool Expression6Held;
        public bool Expression7Held;
        public bool Expression8Held;

        public bool Number1Held;
        public bool Number2Held;
        public bool Number3Held;
        public bool Number4Held;
        public bool Number5Held;

        // --- 核心魔法：缓存计时器 (Input Buffers) ---
        public float JumpBufferTimer;
        public float DodgeBufferTimer;
        public float RollBufferTimer;
        public float InteractBufferTimer;
        public float PrimaryAttackBufferTimer;
        public float SecondaryAttackBufferTimer;

        public float Expression1BufferTimer;
        public float Expression2BufferTimer;
        public float Expression3BufferTimer;
        public float Expression4BufferTimer;
        public float Expression5BufferTimer;
        public float Expression6BufferTimer;
        public float Expression7BufferTimer;
        public float Expression8BufferTimer;

        public float Number1BufferTimer;
        public float Number2BufferTimer;
        public float Number3BufferTimer;
        public float Number4BufferTimer;
        public float Number5BufferTimer;

        // --- 向外暴露的单帧意愿（依赖 BufferTimer 的计算属性）---
        public bool JumpPressed => JumpBufferTimer > 0f;
        public bool DodgePressed => DodgeBufferTimer > 0f;
        public bool RollPressed => RollBufferTimer > 0f;
        public bool InteractPressed => InteractBufferTimer > 0f;
        public bool PrimaryAttackPressed => PrimaryAttackBufferTimer > 0f;
        public bool SecondaryAttackPressed => SecondaryAttackBufferTimer > 0f;

        public bool Expression1Pressed => Expression1BufferTimer > 0f;
        public bool Expression2Pressed => Expression2BufferTimer > 0f;
        public bool Expression3Pressed => Expression3BufferTimer > 0f;
        public bool Expression4Pressed => Expression4BufferTimer > 0f;
        public bool Expression5Pressed => Expression5BufferTimer > 0f;
        public bool Expression6Pressed => Expression6BufferTimer > 0f;
        public bool Expression7Pressed => Expression7BufferTimer > 0f;
        public bool Expression8Pressed => Expression8BufferTimer > 0f;

        public bool Number1Pressed => Number1BufferTimer > 0f;
        public bool Number2Pressed => Number2BufferTimer > 0f;
        public bool Number3Pressed => Number3BufferTimer > 0f;
        public bool Number4Pressed => Number4BufferTimer > 0f;
        public bool Number5Pressed => Number5BufferTimer > 0f;
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
