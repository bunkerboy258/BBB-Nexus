using System;
using System.Collections.Generic;
using UnityEngine;

namespace BBBNexus
{
    [DisallowMultipleComponent]
    public sealed class EnemyInventoryInitializer : MonoBehaviour, IPoolable
    {
        public enum SeedMode
        {
            EnsureMinimum,
            Additive
        }

        [Serializable]
        public sealed class Entry
        {
            public ItemDefinitionSO Item;
            [Min(1)] public int Count = 1;
        }

        [Header("--- Scope ---")]
        [SerializeField] private bool _skipPlayer = true;

        [Header("--- Seed ---")]
        [SerializeField] private SeedMode _seedMode = SeedMode.EnsureMinimum;
        [SerializeField] private List<Entry> _entries = new();

        private BBBCharacterController _owner;

        private void Start()
        {
            Apply();
        }

        public void OnSpawned()
        {
            Apply();
        }

        public void OnDespawned()
        {
        }

        [ContextMenu("Apply Inventory Seed")]
        public void Apply()
        {
            _owner = ResolveOwner();
            if (_owner == null)
            {
                Debug.LogWarning("[EnemyInventoryInitializer] Parent BBBCharacterController not found.", this);
                return;
            }

            if (_skipPlayer && _owner.CompareTag("Player"))
            {
                return;
            }

            for (var i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                if (entry == null || entry.Item == null || entry.Count <= 0)
                {
                    continue;
                }

                string itemId = entry.Item.ItemID;
                if (string.IsNullOrWhiteSpace(itemId))
                {
                    Debug.LogWarning("[EnemyInventoryInitializer] ItemID is empty, skipped.", this);
                    continue;
                }

                int addCount = entry.Count;
                if (_seedMode == SeedMode.EnsureMinimum)
                {
                    int currentCount = _owner.InventoryService?.GetCount(entry.Item) ?? 0;
                    addCount = Mathf.Max(0, entry.Count - currentCount);
                }

                if (addCount <= 0)
                {
                    continue;
                }

                if (_owner.InventoryService == null || _owner.InventoryService.TryAdd(entry.Item, addCount) == 0)
                {
                    Debug.LogWarning($"[EnemyInventoryInitializer] Failed to add item '{itemId}' x{addCount}.", this);
                }
            }
        }

        private BBBCharacterController ResolveOwner()
        {
            return GetComponentInParent<BBBCharacterController>(true);
        }
    }
}
