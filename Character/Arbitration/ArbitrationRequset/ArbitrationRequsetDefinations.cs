using UnityEngine;
using System;

namespace BBBNexus
{
    [Serializable]
    public struct ActionRequest
    {
        public AnimationClip Clip;
        public float FadeDuration;
        public int Priority;
        public bool ApplyGravity;
        /// <summary>播放速度倍率。-1 表示使用默认速度（1x）</summary>
        public float Speed;
        /// <summary>
        /// 完整的 Animancer ITransition（ClipTransition 等），nullable。
        /// 设置后 StartTime / EndTime / Speed / FadeDuration 全部由 Animancer 接管。
        /// 未设置时退回到 Clip + FadeDuration + Speed 旧路径。
        /// 使用 object 类型避免对 Animancer 程序集的强依赖（与 PlayTransition 同模式）。
        /// </summary>
        public object Transition;
        /// <summary>
        /// true = 近战攻击模式：角色间阻挡时整体归零，不产生切线滑移。
        /// false = 默认：剥掉朝向分量，保留切线分量（行走行为）。
        /// </summary>
        public bool HardStopOnBlock;

        public ActionRequest(AnimationClip clip, int priority = 20, float fadeDuration = 0.2f, bool applyGravity = true, float speed = -1f)
        {
            Clip = clip;
            Priority = priority;
            FadeDuration = fadeDuration;
            ApplyGravity = applyGravity;
            Speed = speed;
            Transition = null;
            HardStopOnBlock = false;
        }
    }
}