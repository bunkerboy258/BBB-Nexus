using System;
using System.Collections.Generic;
using UnityEngine;

namespace BBBNexus
{
    /// <summary>
    /// 音效归一化增益表（ScriptableObject）。
    /// 由 Editor 工具扫描项目内所有 AudioClip，计算 RMS 后写入增益值。
    /// WeaponAudioUtil.PlayAt 运行时查表，传入 AudioSource.PlayClipAtPoint 的 volume 参数。
    ///
    /// 用法：
    ///   1. 在 Project 窗口 Create → BBBNexus → Audio → AudioNormalizationProfile
    ///   2. 放入 Assets/Resources/ 文件夹（命名 AudioNormalization）
    ///   3. 点击 Inspector 里的「扫描并生成增益表」
    ///   4. WeaponAudioUtil 自动加载，不需要手动赋值
    /// </summary>
    [CreateAssetMenu(fileName = "AudioNormalization",
                     menuName  = "BBBNexus/Audio/AudioNormalizationProfile")]
    public class AudioNormalizationProfile : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public AudioClip Clip;
            [Range(0f, 1f)]
            [Tooltip("归一化增益（0~1）。由编辑器工具写入，勿手动调。")]
            public float Gain;
        }

        [Tooltip("目标 RMS 电平（线性值，0.25 ≈ -12 dBFS，0.126 ≈ -18 dBFS）。\n" +
                 "只做降音量，不做放大，所有 Gain ≤ 1。")]
        [Range(0.05f, 0.9f)]
        public float TargetRms = 0.25f;

        public List<Entry> Entries = new();

        // ── 运行时查找表（延迟初始化）────────────────────────────────────
        private Dictionary<AudioClip, float> _lookup;

        /// <summary>返回 clip 的归一化增益，未收录时返回 1（不衰减）。</summary>
        public float GetGain(AudioClip clip)
        {
            if (clip == null) return 1f;
            if (_lookup == null) BuildLookup();
            return _lookup.TryGetValue(clip, out float g) ? g : 1f;
        }

        private void BuildLookup()
        {
            _lookup = new Dictionary<AudioClip, float>(Entries.Count);
            foreach (var e in Entries)
                if (e.Clip != null) _lookup[e.Clip] = e.Gain;
        }

        /// <summary>Entries 变化后调用，强制重建查找表。</summary>
        public void InvalidateLookup() => _lookup = null;
    }
}
