using System;
using System.Collections.Generic;
using UnityEngine;
using Animancer;

namespace BBBNexus
{
    /// <summary>
    /// 表情系统配置模块（开源版极简框架）：
    /// - BaseExpression：常态循环表情
    /// - Event -> ClipTransition：瞬时表情由状态机通过 PlayerFacialEvent 触发
    /// </summary>
    [CreateAssetMenu(fileName = "EmjSO", menuName = "BBBNexus/Player/Modules/EmjSO")]
    public class EmjSO : ScriptableObject
    {
        [Serializable]
        public struct EventEntry
        {
            public PlayerFacialEvent Event;
            public ClipTransition Transition;
        }

        [Header("基础表情 (Base Expression)")]
        [Tooltip("基础表情动画：循环播放，作为常态表情。")]
        public ClipTransition BaseExpression;

        [Header("事件表情 (Event Expressions)")]
        [SerializeField] private List<EventEntry> _entries = new List<EventEntry>();

        private Dictionary<PlayerFacialEvent, ClipTransition> _cache;

        private void OnEnable() => BuildCache();
        private void OnValidate() => BuildCache();

        private void BuildCache()
        {
            if (_cache == null) _cache = new Dictionary<PlayerFacialEvent, ClipTransition>();
            else _cache.Clear();

            if (_entries == null) return;

            for (int i = 0; i < _entries.Count; i++)
            {
                var e = _entries[i];
                if (e.Transition == null || e.Transition.Clip == null) continue;
                _cache[e.Event] = e.Transition; // 后写覆盖前写
            }
        }

        public bool TryGet(PlayerFacialEvent evt, out ClipTransition transition)
        {
            transition = null;
            if (_cache == null) BuildCache();
            return _cache != null && _cache.TryGetValue(evt, out transition) && transition != null && transition.Clip != null;
        }
    }
}