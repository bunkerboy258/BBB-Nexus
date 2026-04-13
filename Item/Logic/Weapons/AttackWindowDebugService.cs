using System;
using UnityEngine;

namespace BBBNexus
{
    /// <summary>
    /// 攻击窗口调试服务。存储当前激活的攻击窗口上下文，供 Gizmo 绘制使用。
    /// 仅用于 Editor 调试，不影响运行时逻辑。
    /// </summary>
    public static class AttackWindowDebugService
    {
        private static AttackWindowDebugContext? _activeContext;

        /// <summary>
        /// 当前激活的攻击窗口上下文。
        /// </summary>
        public static AttackWindowDebugContext? ActiveContext => _activeContext;

        /// <summary>
        /// 是否有激活的攻击窗口。
        /// </summary>
        public static bool HasActiveWindow => _activeContext.HasValue;

        /// <summary>
        /// 注册一个新的攻击窗口上下文。
        /// </summary>
        public static void RegisterWindow(
            float startTime,
            float endTime,
            float[] windowStartTimes,
            float[] windowEndTimes,
            float alignmentStartTime,
            float alignmentEndTime,
            int comboIndex,
            float actualDuration,
            float dominantEnd)
        {
            _activeContext = new AttackWindowDebugContext
            {
                StartTime = startTime,
                EndTime = endTime,
                WindowStartTimes = windowStartTimes,
                WindowEndTimes = windowEndTimes,
                AlignmentStartTime = alignmentStartTime,
                AlignmentEndTime = alignmentEndTime,
                ComboIndex = comboIndex,
                ActualDuration = actualDuration,
                DominantEnd = dominantEnd,
                RegisterTime = Time.time,
            };
        }

        /// <summary>
        /// 清除当前激活的攻击窗口上下文。
        /// </summary>
        public static void ClearWindow()
        {
            _activeContext = null;
        }

        /// <summary>
        /// 检查指定时间是否在任何伤害窗口内。
        /// </summary>
        public static bool IsInAnyDamageWindow(float time)
        {
            if (!_activeContext.HasValue)
                return false;

            var ctx = _activeContext.Value;
            for (int i = 0; i < ctx.WindowStartTimes.Length; i++)
            {
                if (time >= ctx.WindowStartTimes[i] && 
                    time <= ctx.WindowEndTimes[i])
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 获取当前时间对应的伤害窗口索引。
        /// </summary>
        /// <returns>返回窗口索引，如果没有激活的窗口返回 -1</returns>
        public static int GetCurrentDamageWindowIndex(float time)
        {
            if (!_activeContext.HasValue)
                return -1;

            var ctx = _activeContext.Value;
            for (int i = 0; i < ctx.WindowStartTimes.Length; i++)
            {
                if (time >= ctx.WindowStartTimes[i] && 
                    time <= ctx.WindowEndTimes[i])
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// 计算归一化进度 (0-1)，相对于主导结束时间。
        /// </summary>
        public static float GetNormalizedProgress(float time)
        {
            if (!_activeContext.HasValue || _activeContext.Value.DominantEnd <= 0f)
                return 0f;

            var ctx = _activeContext.Value;
            float localTime = time - ctx.StartTime;
            return Mathf.InverseLerp(0f, ctx.DominantEnd, localTime);
        }

        /// <summary>
        /// 攻击窗口调试上下文数据结构。
        /// </summary>
        public struct AttackWindowDebugContext
        {
            /// <summary>攻击开始时间 (Time.time)</summary>
            public float StartTime;
            /// <summary>攻击结束时间 (Time.time)</summary>
            public float EndTime;
            /// <summary>所有伤害窗口的开始时间数组</summary>
            public float[] WindowStartTimes;
            /// <summary>所有伤害窗口的结束时间数组</summary>
            public float[] WindowEndTimes;
            /// <summary>对齐窗口开始时间</summary>
            public float AlignmentStartTime;
            /// <summary>对齐窗口结束时间</summary>
            public float AlignmentEndTime;
            /// <summary>当前连招索引</summary>
            public int ComboIndex;
            /// <summary>动画实际持续时间</summary>
            public float ActualDuration;
            /// <summary>主导结束时间 (扣除淡出)</summary>
            public float DominantEnd;
            /// <summary>注册时间 (用于过期检查)</summary>
            public float RegisterTime;
        }
    }
}
