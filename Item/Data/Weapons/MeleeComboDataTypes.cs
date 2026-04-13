using UnityEngine;

namespace BBBNexus
{
    public enum FistsAttackHand
    {
        MainHand = 0,
        OffHand = 1,
        BothHands = 2,
    }

    [System.Serializable]
    public struct DamageSubWindow
    {
        [Range(0f, 1f)]
        public float StartNormalized;

        [Range(0f, 1f)]
        public float EndNormalized;
    }

    [System.Serializable]
    public struct FistsDamageWindowSidecar
    {
        [Tooltip("是否启用该伤害窗口侧链。关闭时运行时可回退到旧逻辑。")]
        public bool Enabled;

        [Tooltip("该连招段的伤害倍率。1 = 使用基础伤害；2 = 双倍伤害。")]
        [Min(0f)]
        public float DamageMultiplier;

        [Tooltip("相对动画实际统治时间的归一化起点。0 表示实际统治时间开始，1 表示结束。")]
        [Range(0f, 1f)]
        public float StartNormalized;

        [Tooltip("相对动画实际统治时间的归一化终点。0 表示实际统治时间开始，1 表示结束。")]
        [Range(0f, 1f)]
        public float EndNormalized;

        [Tooltip("额外的伤害子窗口。每个子窗口是一段独立的伤害判定区间。主窗口 (Start/End) 始终作为第一个窗口。")]
        public DamageSubWindow[] ExtraWindows;

        public static FistsDamageWindowSidecar Default =>
            new FistsDamageWindowSidecar
            {
                Enabled = false,
                DamageMultiplier = 1f,
                StartNormalized = 0.15f,
                EndNormalized = 0.45f,
                ExtraWindows = null,
            };

        public int WindowCount => 1 + (ExtraWindows != null ? ExtraWindows.Length : 0);

        public void GetWindow(int index, out float start, out float end)
        {
            if (index <= 0)
            {
                start = StartNormalized;
                end = EndNormalized;
            }
            else
            {
                int extraIndex = index - 1;
                if (ExtraWindows != null && extraIndex < ExtraWindows.Length)
                {
                    start = ExtraWindows[extraIndex].StartNormalized;
                    end = ExtraWindows[extraIndex].EndNormalized;
                }
                else
                {
                    start = 0f;
                    end = 0f;
                }
            }

            if (end < start)
                (start, end) = (end, start);
        }
    }

    [System.Serializable]
    public struct FistsAlignmentWindowSidecar
    {
        [Tooltip("是否启用该前摇对齐窗口侧链。关闭时表示该动作段没有专门的对齐时段。")]
        public bool Enabled;

        [Tooltip("相对动画实际统治时间的归一化起点。0 表示实际统治时间开始，1 表示结束。")]
        [Range(0f, 1f)]
        public float StartNormalized;

        [Tooltip("相对动画实际统治时间的归一化终点。0 表示实际统治时间开始，1 表示结束。")]
        [Range(0f, 1f)]
        public float EndNormalized;

        public static FistsAlignmentWindowSidecar Default =>
            new FistsAlignmentWindowSidecar
            {
                Enabled = false,
                StartNormalized = 0f,
                EndNormalized = 0.25f,
            };
    }
}
