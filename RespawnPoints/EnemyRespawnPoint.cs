using System.Linq;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace BBBNexus
{
    /// <summary>
    /// 怪物复活点。
    /// 使用预制体拖拽引用生成 BBB 敌人。
    /// 当前只负责场景初始化生成和显式刷新，不负责监听死亡后的自动循环重生。
    /// </summary>
    public class EnemyRespawnPoint : MonoBehaviour, IEnemyRespawnPoint
    {
        [Header("生成配置")]
        [SerializeField] private GameObject _prefab;  // 替换原来的 _packId
        [SerializeField] private bool _spawnOnStart = true;
        [SerializeField] private bool _useObjectPool = true;

        [Header("AI 配置（可选）")]
        [SerializeField] private string _brainType;
        [SerializeField] private string _tacticalConfigId;

        [Header("调试")]
        [SerializeField] private bool _logSpawn = false;
        [SerializeField, HideInInspector] private GameObject _previewInstance;

        private BBBCharacterController _currentActor;

        public BBBCharacterController CurrentActor => _currentActor;
        public GameObject Prefab => _prefab;

        #if UNITY_EDITOR
        public GameObject PreviewInstance => _previewInstance;
        #endif

        private void Start()
        {
            BindExistingPreviewAsCurrentActor();

            if (_spawnOnStart)
            {
                if (_currentActor != null)
                {
                    return;
                }

                SpawnNow();
            }
        }

        [ContextMenu("立即生成")]
        public void SpawnNow()
        {
            if (_prefab == null)
            {
                Debug.LogWarning("[EnemyRespawnPoint] 预制体为空，无法生成。", this);
                return;
            }

            _currentActor = SpawnActor();
        }

        [ContextMenu("刷新生成")]
        public void RefreshSpawn()
        {
            ClearCurrentActor();
            SpawnNow();
        }

        [ContextMenu("清除当前怪物")]
        public void ClearCurrentActor()
        {
            if (_currentActor == null)
            {
                return;
            }

            var actor = _currentActor;
            _currentActor = null;

            if (SimpleObjectPoolSystem.Shared != null)
            {
                SimpleObjectPoolSystem.Shared.TryDespawn(actor.gameObject);
            }
            else
            {
                Destroy(actor.gameObject);
            }
        }

        #if UNITY_EDITOR
        [ContextMenu("生成预览子物体")]
        public void GeneratePreviewAsChild()
        {
            ClearPreviewInstance();

            var prefab = _prefab;
            if (prefab == null)
            {
                Debug.LogWarning($"[EnemyRespawnPoint] 预制体为空，无法生成预览。", this);
                return;
            }

            var preview = PrefabUtility.InstantiatePrefab(prefab, transform) as GameObject;
            if (preview == null)
            {
                preview = Instantiate(prefab, transform);
            }

            Undo.RegisterCreatedObjectUndo(preview, "Generate Enemy Preview");
            preview.name = $"{prefab.name}_Preview";
            preview.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

            _previewInstance = preview;
            EditorUtility.SetDirty(this);
        }

        [ContextMenu("清除预览子物体")]
        public void ClearPreviewInstance()
        {
            if (_previewInstance == null)
            {
                return;
            }

            Undo.DestroyObjectImmediate(_previewInstance);
            _previewInstance = null;
            EditorUtility.SetDirty(this);
        }
        #endif

        private BBBCharacterController SpawnActor()
        {
            if (_prefab == null)
            {
                Debug.LogWarning("[EnemyRespawnPoint] 预制体为空，无法生成。", this);
                return null;
            }

            GameObject instance = _useObjectPool && SimpleObjectPoolSystem.Shared != null
                ? SimpleObjectPoolSystem.Shared.Spawn(_prefab)
                : Instantiate(_prefab);

            // 设置位置旋转
            instance.transform.SetPositionAndRotation(transform.position, transform.rotation);

            // 获取BBBCharacterController组件
            var actor = instance.GetComponent<BBBCharacterController>() ??
                       instance.GetComponentInChildren<BBBCharacterController>(true);

            if (actor != null)
            {
                ApplyOptionalAiConfig(actor);
            }
            else
            {
                Debug.LogWarning($"[EnemyRespawnPoint] 预制体 '{_prefab.name}' 不包含 BBBCharacterController 组件。", this);
            }

            if (_logSpawn)
            {
                Debug.Log($"[EnemyRespawnPoint] Spawned '{actor?.name ?? "未知"}' from prefab '{_prefab.name}'.", this);
            }

            return actor;
        }

        private void ApplyOptionalAiConfig(BBBCharacterController actor)
        {
            if (actor == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(_brainType))
            {
                AIManager.SetBrain(actor, _brainType);
            }

            if (!string.IsNullOrWhiteSpace(_tacticalConfigId))
            {
                AIManager.SetTacticalConfig(actor, _tacticalConfigId);
            }
        }

        private void BindExistingPreviewAsCurrentActor()
        {
            if (_currentActor != null)
            {
                return;
            }

            if (_previewInstance == null)
            {
                _previewInstance = FindPreviewInstanceInChildren();
            }

            if (_previewInstance == null)
            {
                return;
            }

            var actor = _previewInstance.GetComponent<BBBCharacterController>() ??
                       _previewInstance.GetComponentInChildren<BBBCharacterController>(true);
            if (actor == null)
            {
                return;
            }

            _currentActor = actor;
            ApplyOptionalAiConfig(actor);
        }

        private GameObject FindPreviewInstanceInChildren()
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                if (child == null)
                {
                    continue;
                }

                if (child.GetComponent<BBBCharacterController>() != null ||
                    child.GetComponentInChildren<BBBCharacterController>(true) != null)
                {
                    return child.gameObject;
                }
            }

            return null;
        }

        #if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.9f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.25f, 0.35f);
            Gizmos.DrawRay(transform.position + Vector3.up * 0.1f, transform.forward * 1.2f);
        }
        #endif
    }
}