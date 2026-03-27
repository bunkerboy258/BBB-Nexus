using System;
using System.Collections.Generic;
using UnityEngine;

namespace BBBNexus
{
    /// <summary>
    /// 角色音频配置（开源版极简框架）：
    /// - 只做“事件 -> 音频集合”的映射
    /// - 播放策略交给 AudioDriver/AudioController（当前：随机选一个并 PlayOneShot）
    /// </summary>
    [CreateAssetMenu(fileName = "AudioSO", menuName = "BBBNexus/Player/Modules/AudioSO")]
    public sealed class AudioSO : ScriptableObject
    {
        [Serializable]
        public struct EventEntry
        {
            public PlayerSfxEvent Event;

            [Tooltip("该事件可用的音频集合（会随机挑选一个播放）。")]
            public AudioClip[] Clips;
        }

        [Header("Player SFX Map (Event -> Clips)")]
        [SerializeField] private List<EventEntry> _entries = new List<EventEntry>();

        private Dictionary<PlayerSfxEvent, AudioClip[]> _cache;

        private void OnEnable() => BuildCache();
        private void OnValidate() => BuildCache();

        private void BuildCache()
        {
            if (_cache == null) _cache = new Dictionary<PlayerSfxEvent, AudioClip[]>();
            else _cache.Clear();

            if (_entries == null) return;

            for (int i = 0; i < _entries.Count; i++)
            {
                var e = _entries[i];
                if (e.Clips == null || e.Clips.Length == 0) continue;

                // 后写覆盖前写：便于在 Inspector 里快速覆盖
                _cache[e.Event] = e.Clips;
            }
        }

        public bool TryGetClips(PlayerSfxEvent evt, out AudioClip[] clips)
        {
            clips = null;
            if (_cache == null) BuildCache();
            return _cache != null && _cache.TryGetValue(evt, out clips) && clips != null && clips.Length > 0;
        }

        public bool TryPickClip(PlayerSfxEvent evt, out AudioClip clip)
        {
            clip = null;
            if (!TryGetClips(evt, out var clips)) return false;

            int idx = UnityEngine.Random.Range(0, clips.Length);
            clip = clips[idx];
            return clip != null;
        }
    }
}
