using System;
using System.Collections.Generic;
using UnityEngine;

namespace BBBNexus
{
    /// <summary>
    /// 基础对象池
    /// </summary>
    public sealed class SimpleObjectPoolSystem : MonoBehaviour
    {
        [Serializable]
        public struct PrewarmEntry
        {
            public GameObject Prefab;
            [Min(0)] public int Count;
        }

        public static SimpleObjectPoolSystem Shared { get; private set; }

        [Header("Prewarm")]
        [SerializeField] private List<PrewarmEntry> _prewarm = new List<PrewarmEntry>();

        // 这里给一个保守的初始容量 减少运行时扩容概率
        private readonly Dictionary<GameObject, Queue<GameObject>> _pool = new Dictionary<GameObject, Queue<GameObject>>(16);
        private readonly Dictionary<int, GameObject> _instanceToPrefab = new Dictionary<int, GameObject>(256);

        // 注: GetComponentsInChildren<T>() 每次调用都会返回新数组，必然产生 GC.Alloc。
        // 对象池场景下 Spawn/Despawn 属于高频路径，因此对每个实例缓存其 IPoolable[]。
        private readonly Dictionary<int, IPoolable[]> _instancePoolablesCache = new Dictionary<int, IPoolable[]>(256);

        private void Awake()
        {
            if (Shared != null && Shared != this)
            {
                Destroy(gameObject);
                return;
            }

            Shared = this;
            PrewarmAll();
        }

        private void PrewarmAll()
        {
            if (_prewarm == null) return;
            for (int i = 0; i < _prewarm.Count; i++)
            {
                var e = _prewarm[i];
                if (e.Prefab == null || e.Count <= 0) continue;
                Prewarm(e.Prefab, e.Count);
            }
        }

        public void Prewarm(GameObject prefab, int count)
        {
            if (prefab == null || count <= 0) return;

            var q = GetOrCreateQueue(prefab);
            for (int i = 0; i < count; i++)
            {
                var inst = CreateInstance(prefab);
                InternalDespawn(inst, callCallbacks: true);
                q.Enqueue(inst);
            }
        }

        /// <summary>
        /// Spawn：取出或创建一个实例 并激活
        /// 注意：不设置 parent 不改 transform 调用者自行定位
        /// </summary>
        public GameObject Spawn(GameObject prefab)
        {
            if (prefab == null) return null;

            var q = GetOrCreateQueue(prefab);

            GameObject inst = null;
            while (q.Count > 0 && inst == null)
                inst = q.Dequeue();

            if (inst == null)
                inst = CreateInstance(prefab);

            InternalSpawn(inst, callCallbacks: true);
            return inst;
        }

        /// <summary>
        /// 尝试回收：如果 instance 不是由对象池创建的实例 则不会警告 返回 false
        /// 适用于 VFX 这类“有时被 Instantiate 有时被池 Spawn”的资源
        /// </summary>
        public bool TryDespawn(GameObject instance)
        {
            if (instance == null) return true;

            if (!_instanceToPrefab.TryGetValue(instance.GetInstanceID(), out var prefab) || prefab == null)
                return false;

            var q = GetOrCreateQueue(prefab);
            InternalDespawn(instance, callCallbacks: true);
            q.Enqueue(instance);
            return true;
        }

        /// <summary>
        /// Despawn：回收一个实例
        /// </summary>
        public void Despawn(GameObject instance)
        {
            if (instance == null) return;

            if (!_instanceToPrefab.TryGetValue(instance.GetInstanceID(), out var prefab) || prefab == null)
            {
                Debug.LogWarning($"[SimpleObjectPoolSystem] Despawn called for non-pooled instance: {instance.name}", instance);

                return;
            }

            var q = GetOrCreateQueue(prefab);
            InternalDespawn(instance, callCallbacks: true);
            q.Enqueue(instance);
        }

        private Queue<GameObject> GetOrCreateQueue(GameObject prefab)
        {
            if (!_pool.TryGetValue(prefab, out var q) || q == null)
            {
                // 扩容风险：Queue 默认容量较小 会随着 Enqueue 扩容分配
                // 这里尝试根据预热配置给一个更合理的初始容量
                int initialCapacity = 0;
                if (_prewarm != null)
                {
                    for (int i = 0; i < _prewarm.Count; i++)
                    {
                        if (_prewarm[i].Prefab == prefab)
                        {
                            initialCapacity = Mathf.Max(0, _prewarm[i].Count);
                            break;
                        }
                    }
                }

                q = initialCapacity > 0 ? new Queue<GameObject>(initialCapacity) : new Queue<GameObject>();
                _pool[prefab] = q;
            }
            return q;
        }

        private GameObject CreateInstance(GameObject prefab)
        {
            var inst = Instantiate(prefab);
            int id = inst.GetInstanceID();
            _instanceToPrefab[id] = prefab;

            // 预缓存 IPoolable[] 避免 Spawn/Despawn 时 GetComponentsInChildren 分配数组
            CachePoolables(inst);

            return inst;
        }

        private void CachePoolables(GameObject instance)
        {
            if (instance == null) return;

            int id = instance.GetInstanceID();
            if (_instancePoolablesCache.ContainsKey(id)) return;

            // GC RISK NOTE: 这个 API 返回数组 会产生一次性分配
            // 但这发生在“创建实例时”（低频） 可接受；并换取后续 Spawn/Despawn 0 GC
            var poolables = instance.GetComponentsInChildren<IPoolable>(true);
            _instancePoolablesCache[id] = poolables;

        }

        private void InternalSpawn(GameObject instance, bool callCallbacks)
        {
            if (instance == null) return;

            // 时序修复：先激活，确保 OnEnable/Start 等生命周期已就绪，然后再通知 IPoolable。
            instance.SetActive(true);

            if (callCallbacks)
            {
                int id = instance.GetInstanceID();
                if (!_instancePoolablesCache.TryGetValue(id, out var poolables) || poolables == null)
                {
                    CachePoolables(instance);
                    _instancePoolablesCache.TryGetValue(id, out poolables);
                }

                if (poolables != null)
                {
                    for (int i = 0; i < poolables.Length; i++)
                    {
                        try { poolables[i]?.OnSpawned(); } catch { }
                    }
                }
            }
        }

        private void InternalDespawn(GameObject instance, bool callCallbacks)
        {
            if (instance == null) return;

            // 回收时：先让对象清理自身状态，再失活。
            if (callCallbacks)
            {
                int id = instance.GetInstanceID();
                if (!_instancePoolablesCache.TryGetValue(id, out var poolables) || poolables == null)
                {
                    CachePoolables(instance);
                    _instancePoolablesCache.TryGetValue(id, out poolables);
                }

                if (poolables != null)
                {
                    for (int i = 0; i < poolables.Length; i++)
                    {
                        try { poolables[i]?.OnDespawned(); } catch { }
                    }
                }
            }

            instance.SetActive(false);
        }
    }
}
