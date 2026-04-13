using System;
using UnityEngine;

namespace BBBNexus
{
    public class SingletonData<T> where T : class, new()
    {
        private static readonly Lazy<T> _instance = new Lazy<T>(() => new T());
        public static T Instance => _instance.Value;
        protected SingletonData() { }
    }

    public class SingletonMono<T> : MonoBehaviour where T : MonoBehaviour
    {
        private static T _instance;
        private static readonly object _lock = new object();
        private static bool _applicationIsQuitting = false;

        public bool DontDestroyOnLoadEnabled = true;

        public static T Instance
        {
            get
            {
                if (_applicationIsQuitting)
                {
                    Debug.LogWarning($"[Singleton] {typeof(T)} 实例在程序退出时被尝试访问，返回null。");
                    return null;
                }

                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = (T)FindObjectOfType(typeof(T));

                        if (_instance == null)
                        {
                            var obj = new GameObject();
                            _instance = obj.AddComponent<T>();
                            obj.name = typeof(T).ToString() + " (Singleton)";
                            DontDestroyOnLoad(obj);
                            Debug.Log($"[Singleton] 自动创建了实例: {obj.name}");
                        }
                    }
                    return _instance;
                }
            }
        }

        protected virtual void Awake()
        {
            if (_instance == null)
            {
                _instance = this as T;
                if (DontDestroyOnLoadEnabled)
                    DontDestroyOnLoad(gameObject);
            }
            else if (_instance != this)
            {
                Debug.LogWarning($"[Singleton] 场景中存在重复的 {typeof(T)}，已自动删除！");
                Destroy(gameObject);
            }
        }

        private void OnApplicationQuit() => _applicationIsQuitting = true;

        private void OnDestroy()
        {
            if (_instance == this)
                _applicationIsQuitting = true;
        }
    }
}
