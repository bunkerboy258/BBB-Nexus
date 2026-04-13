using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace BBBNexus
{
    // =========================================================
    // 1. 标签定义 (反射模式专用)
    // =========================================================
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class Subscribe : Attribute
    {
        public string EventName { get; }
        public int Priority { get; }

        public Subscribe(string eventName, int priority = 0)
        {
            EventName = eventName;
            Priority = priority;
        }
    }

    // =========================================================
    // 2. 双模事件总线 (PostSystem)
    // =========================================================
    public class PostSystem : SingletonData<PostSystem>
    {
        private class Handler
        {
            public object Target;
            public Action<object> Action;
            public int Priority;
        }

        private readonly Dictionary<string, List<Handler>> _eventTable = new Dictionary<string, List<Handler>>();
        private readonly Dictionary<object, HashSet<string>> _targetToEvents = new Dictionary<object, HashSet<string>>();

        // =========================================================
        // API 1: 发送事件
        // =========================================================

        public void Send(string eventName, object data = null)
        {
            if (_eventTable.TryGetValue(eventName, out var list))
            {
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    var h = list[i];
                    try
                    {
                        if (h.Target != null && h.Target.Equals(null))
                        {
                            list.RemoveAt(i);
                            continue;
                        }
                        h.Action.Invoke(data);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"<color=red>[BBBNexus.PostSystem] {eventName} Error: {e}</color>");
                    }
                }
            }
        }

        // =========================================================
        // API 2: 反射注册模式 (Register / Unregister)
        // =========================================================

        public void Register(object target)
        {
            if (target == null) return;

            var type = target.GetType();
            while (type != null && type != typeof(MonoBehaviour) && type != typeof(object))
            {
                var methods = type.GetMethods(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

                foreach (var method in methods)
                {
                    var attrs = method.GetCustomAttributes<Subscribe>();
                    foreach (var attr in attrs)
                    {
                        var capturedMethod = method;
                        var paramCount = method.GetParameters().Length;
                        Action<object> action = (data) =>
                        {
                            if (paramCount == 0)
                                capturedMethod.Invoke(target, null);
                            else
                                capturedMethod.Invoke(target, new[] { data });
                        };
                        AddHandler(attr.EventName, target, action, attr.Priority);
                    }
                }

                type = type.BaseType;
            }
        }

        public void Unregister(object target)
        {
            if (target == null) return;
            if (_targetToEvents.TryGetValue(target, out var events))
            {
                foreach (var evtName in events.ToList())
                {
                    if (_eventTable.TryGetValue(evtName, out var list))
                        list.RemoveAll(h => h.Target == target);
                }
                _targetToEvents.Remove(target);
            }
        }

        // =========================================================
        // API 3: 传统委托模式 (On / Off)
        // =========================================================

        public void On(string eventName, Action<object> callback, int priority = 0)
        {
            if (callback == null) return;
            AddHandler(eventName, callback.Target, callback, priority);
        }

        public void Off(string eventName, Action<object> callback)
        {
            if (callback == null) return;
            if (_eventTable.TryGetValue(eventName, out var list))
            {
                var handler = list.FirstOrDefault(h => h.Action == callback);
                if (handler != null)
                {
                    list.Remove(handler);
                    if (handler.Target != null && _targetToEvents.TryGetValue(handler.Target, out var events))
                        events.Remove(eventName);
                }
            }
        }

        private void AddHandler(string eventName, object target, Action<object> action, int priority)
        {
            if (!_eventTable.TryGetValue(eventName, out var list))
            {
                list = new List<Handler>();
                _eventTable[eventName] = list;
            }

            if (list.Any(h => h.Action == action)) return;

            list.Add(new Handler { Target = target, Action = action, Priority = priority });
            list.Sort((a, b) => b.Priority.CompareTo(a.Priority));

            if (target != null)
            {
                if (!_targetToEvents.TryGetValue(target, out var events))
                {
                    events = new HashSet<string>();
                    _targetToEvents[target] = events;
                }
                events.Add(eventName);
            }
        }
    }
}
