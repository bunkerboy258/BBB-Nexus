using System;
using System.Collections.Generic;
using UnityEngine;

namespace BBBNexus
{
    /// <summary>
    /// 基础对象池（开源版）：
    /// - 面向 GameObject prefab 的复用
    /// - 可配置预加载数量
    /// - 支持自定义父节点、位置、旋转、激活状态
    /// 
    /// 约定（重要）：
    /// - 所有池中对象闲置时都挂在 PoolRoot 下。
    /// - Spawn 时如指定 Parent，则将对象挂到 Parent 下，并以 Parent 的本地空间对齐（WorldPositionStays=false）。
    /// - Despawn 时自动清理父级，重新挂回 PoolRoot，并重置本地 Transform，避免复用残留导致错位/缩放异常。
    /// </summary>
    public sealed class SimpleObjectPoolSystem : MonoBehaviour
    {
        [Serializable]
        public struct PrewarmEntry
        {
            public GameObject Prefab;
            [Min(0)] public int PrewarmCount;

            [Tooltip("预热对象默认挂在哪个父节点下（为空则挂到 PoolRoot）。")]
            public Transform DefaultParent;
        }

        public struct SpawnParams
        {
            public Transform Parent;
            public Vector3 Position;
            public Quaternion Rotation;
            public bool WorldPositionStays;

            public static SpawnParams Default => new SpawnParams
            {
                Parent = null,
                Position = Vector3.zero,
                Rotation = Quaternion.identity,
                WorldPositionStays = false,
            };
        }

        public struct DespawnParams
        {
            public bool SetInactive;
            public Transform ReparentTo;

            public static DespawnParams Default => new DespawnParams
            {
                SetInactive = true,
                ReparentTo = null,
            };
        }

        public static SimpleObjectPoolSystem Shared { get; private set; }

        [Header("Pool Root")]
        [SerializeField] private Transform _poolRoot;

        [Header("Prewarm")]
        [SerializeField] private List<PrewarmEntry> _prewarm = new List<PrewarmEntry>();

        private readonly Dictionary<GameObject, Queue<GameObject>> _pool = new Dictionary<GameObject, Queue<GameObject>>();
        private readonly Dictionary<int, GameObject> _instanceToPrefab = new Dictionary<int, GameObject>();

        private void Awake()
        {
            if (Shared != null && Shared != this)
            {
                Destroy(gameObject);
                return;
            }

            Shared = this;

            if (_poolRoot == null)
            {
                var go = new GameObject("[PoolRoot]");
                _poolRoot = go.transform;
                _poolRoot.SetParent(transform, false);
            }

            PrewarmAll();
        }

        private void PrewarmAll()
        {
            if (_prewarm == null) return;

            for (int i = 0; i < _prewarm.Count; i++)
            {
                var e = _prewarm[i];
                if (e.Prefab == null || e.PrewarmCount <= 0) continue;

                Prewarm(e.Prefab, e.PrewarmCount, e.DefaultParent);
            }
        }

        public void Prewarm(GameObject prefab, int count, Transform defaultParent = null)
        {
            if (prefab == null || count <= 0) return;

            var q = GetOrCreateQueue(prefab);
            var parent = defaultParent != null ? defaultParent : _poolRoot;

            for (int i = 0; i < count; i++)
            {
                var inst = CreateInstance(prefab, parent);
                ResetForPool(inst.transform);
                inst.SetActive(false);
                q.Enqueue(inst);
            }
        }

        public GameObject Spawn(GameObject prefab, in SpawnParams p)
        {
            if (prefab == null) return null;

            var q = GetOrCreateQueue(prefab);
            GameObject inst = null;

            while (q.Count > 0 && inst == null)
                inst = q.Dequeue();

            if (inst == null)
                inst = CreateInstance(prefab, _poolRoot);

            var t = inst.transform;

            // Always reparent under pool root first to clear any previous hierarchy side effects.
            t.SetParent(_poolRoot, false);
            ResetForPool(t);

            // Then parent to target container if provided.
            if (p.Parent != null)
            {
                // 强制使用本地空间对齐，避免 worldPositionStays=true 造成奇怪的位移/缩放串联。
                t.SetParent(p.Parent, false);
                t.localPosition = p.Position;
                t.localRotation = p.Rotation;
            }
            else
            {
                // World space spawn, still lives under pool root.
                t.SetParent(_poolRoot, false);
                t.position = p.Position;
                t.rotation = p.Rotation;
            }

            // Ensure scale is always sane on reuse.
            t.localScale = Vector3.one;

            inst.SetActive(true);
            return inst;
        }

        /// <summary>在世界坐标生成（不指定父节点，仍挂在 PoolRoot 下）。</summary>
        public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            var p = SpawnParams.Default;
            p.Parent = null;
            p.Position = position;
            p.Rotation = rotation;
            p.WorldPositionStays = false;
            return Spawn(prefab, in p);
        }

        public GameObject Spawn(GameObject prefab, Transform parent)
        {
            var p = SpawnParams.Default;
            p.Parent = parent;
            p.Position = Vector3.zero;
            p.Rotation = Quaternion.identity;
            p.WorldPositionStays = false;
            return Spawn(prefab, in p);
        }

        public void Despawn(GameObject instance, in DespawnParams p)
        {
            if (instance == null) return;

            if (!_instanceToPrefab.TryGetValue(instance.GetInstanceID(), out var prefab) || prefab == null)
            {
#if UNITY_EDITOR
                Debug.LogWarning($"[SimpleObjectPoolSystem] Despawn called for a non-pooled instance: {instance.name}. Ignored.", instance);
#endif
                return;
            }

            var q = GetOrCreateQueue(prefab);

            if (p.SetInactive)
                instance.SetActive(false);

            var t = instance.transform;

            // 按约定：回收时必须清理父级，统一挂回 poolRoot（或显式指定 ReparentTo）。
            var parent = p.ReparentTo != null ? p.ReparentTo : _poolRoot;
            t.SetParent(parent, false);
            ResetForPool(t);

            q.Enqueue(instance);
        }

        public void Despawn(GameObject instance)
        {
            var p = DespawnParams.Default;
            Despawn(instance, in p);
        }

        private Queue<GameObject> GetOrCreateQueue(GameObject prefab)
        {
            if (!_pool.TryGetValue(prefab, out var q) || q == null)
            {
                q = new Queue<GameObject>();
                _pool[prefab] = q;
            }

            return q;
        }

        private GameObject CreateInstance(GameObject prefab, Transform parent)
        {
            var inst = Instantiate(prefab, parent);
            _instanceToPrefab[inst.GetInstanceID()] = prefab;
            ResetForPool(inst.transform);
            return inst;
        }

        private static void ResetForPool(Transform t)
        {
            if (t == null) return;
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;
        }
    }
}
