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
    /// 
    /// 快捷表情（按键 6789）说明：
    /// - 按键 6：触发 QuickExpression1 事件
    /// - 按键 7：触发 QuickExpression2 事件
    /// - 按键 8：触发 QuickExpression3 事件
    /// - 按键 9：触发 QuickExpression4 事件
    /// 
    /// 使用方法：
    /// 1. 在 EmjSO 配置中的"事件表情"列表中添加 4 个新的 EventEntry
    /// 2. 分别对应 QuickExpression1、QuickExpression2、QuickExpression3、QuickExpression4
    /// 3. 为每个 EventEntry 分配对应的表情动画片段
    /// 4. 运行时按 6789 即可触发对应的表情动画
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
        [Tooltip("基础表情动画")]
        public ClipTransition BaseExpression;

        [Header("事件表情 (Event Expressions)")]
        [Tooltip("配置各种表情事件对应的动画 快捷表情对应按键 6789：QuickExpression1、QuickExpression2、QuickExpression3、QuickExpression4")]
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