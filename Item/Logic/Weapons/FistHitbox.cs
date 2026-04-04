using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace BBBNexus
{
    /// <summary>
    /// 近战攻击碰撞体。收集自身及子物体上的所有 Collider 作为唯一权威数据源，
    /// 同时负责 Gizmo 预览、运行时攻击检测、攻击范围预烘焙。
    /// </summary>
    public class FistHitbox : MonoBehaviour
    {
        public event System.Action<Collider, IDamageable, DamageRequest> HitRegistered;

        private enum AttackGeometryRenderMode
        {
            None = 0,
            SingleClip = 1,
            UnifiedAll = 2,
        }

        [Header("Damage")]
        public float Damage = 10f;

        [Header("Debug")]
        [SerializeField] private bool _drawGizmo = true;
        [SerializeField] private Color _inactiveGizmoColor = new Color(0.78f, 0.78f, 0.78f, 0f);
        [SerializeField] private Color _activeGizmoColor = new Color(1f, 0.2f, 0.2f, 0.14f);
        [SerializeField] private AttackGeometryRenderMode _attackGeometryRenderMode = AttackGeometryRenderMode.UnifiedAll;
        [SerializeField] private int _singleGeometryClipIndex = 0;
        [SerializeField] private string _attackGeometryIdOverride;

        private readonly List<Collider> _detectionColliders = new List<Collider>();
        private MeleeHitScanner _scanner;
        private string _runtimeAttackGeometryId;

        private void Awake()
        {
            CollectColliders();
            AutoCorrectColliders();
            _scanner = new MeleeHitScanner(_detectionColliders);
            enabled = false;
        }

        private void CollectColliders()
        {
            _detectionColliders.Clear();
            GetComponentsInChildren(true, _detectionColliders);

            // 兼容回退：如果没有子物体 Collider，使用根节点的
            if (_detectionColliders.Count == 0)
            {
                Collider fallback = GetComponent<Collider>();
                if (fallback != null)
                    _detectionColliders.Add(fallback);
            }
        }

        private void AutoCorrectColliders()
        {
            for (int i = 0; i < _detectionColliders.Count; i++)
            {
                Collider col = _detectionColliders[i];
                if (col is MeshCollider mc)
                {
                    mc.convex = true;
                    mc.isTrigger = true;
                }
                else
                {
                    col.isTrigger = true;
                }
            }
        }

        /// <summary>
        /// 返回收集到的所有检测 Collider（供 Editor 烘焙工具读取）。
        /// </summary>
        public IReadOnlyList<Collider> GetDetectionColliders()
        {
            if (_detectionColliders.Count == 0)
                CollectColliders();
            return _detectionColliders;
        }

        public void SetOwner(BBBCharacterController owner)
        {
            _scanner?.SetOwner(owner);
        }

        public void Activate()
        {
            Activate(null, true);
        }

        public void Activate(HashSet<IDamageable> sharedHitSet, bool clearHitSet)
        {
            _scanner?.BeginWindow(sharedHitSet, clearHitSet);
            enabled = true;
            PerformScan();
        }

        public void Deactivate()
        {
            _scanner?.EndWindow();
            enabled = false;
        }

        public bool TryGetQueryBox(out Vector3 center, out Vector3 halfExtents, out Quaternion rotation)
        {
            if (_scanner == null)
            {
                center = default;
                halfExtents = default;
                rotation = Quaternion.identity;
                return false;
            }

            return _scanner.TryGetQueryBox(out center, out halfExtents, out rotation);
        }

        public void SetAttackGeometryId(string geometryId)
        {
            _runtimeAttackGeometryId = geometryId;
        }

        private void FixedUpdate()
        {
            if (!enabled)
                return;

            PerformScan();
        }

        private void PerformScan()
        {
            _scanner?.Scan(Damage, NotifyHitRegistered);
        }

        private void NotifyHitRegistered(Collider other, IDamageable damageable, DamageRequest request)
        {
            HitRegistered?.Invoke(other, damageable, request);
        }

        // ─────────────────────────────────────────────────────
        // Gizmo
        // ─────────────────────────────────────────────────────

        private void OnDrawGizmos()
        {
            DrawGizmo(false);
        }

        private void OnDrawGizmosSelected()
        {
            DrawGizmo(true);
        }

        private void DrawGizmo(bool selected)
        {
            if (!_drawGizmo)
                return;

            // 收集 Collider（编辑模式下 _detectionColliders 可能为空）
            List<Collider> colliders;
            if (_detectionColliders != null && _detectionColliders.Count > 0)
            {
                colliders = _detectionColliders;
            }
            else
            {
                colliders = new List<Collider>();
                GetComponentsInChildren(true, colliders);
                if (colliders.Count == 0)
                {
                    Collider fallback = GetComponent<Collider>();
                    if (fallback != null)
                        colliders.Add(fallback);
                }
            }

            if (colliders.Count == 0)
                return;

            Color color = enabled ? _activeGizmoColor : _inactiveGizmoColor;
            if (selected)
                color.a = Mathf.Min(1f, color.a + 0.15f);

            Matrix4x4 previousMatrix = Gizmos.matrix;
            Color previousColor = Gizmos.color;

            for (int i = 0; i < colliders.Count; i++)
            {
                Collider collider = colliders[i];
                if (collider == null) continue;

                Gizmos.color = color;
                Vector3 labelWorldPos;

                switch (collider)
                {
                    case SphereCollider sphere:
                        Gizmos.matrix = sphere.transform.localToWorldMatrix;
                        if (color.a > 0f)
                            Gizmos.DrawSphere(sphere.center, sphere.radius);
                        Gizmos.color = new Color(color.r, color.g, color.b, Mathf.Min(1f, color.a + 0.2f));
                        Gizmos.DrawWireSphere(sphere.center, sphere.radius);
                        labelWorldPos = sphere.transform.TransformPoint(sphere.center + Vector3.up * (sphere.radius + 0.06f));
                        break;

                    case BoxCollider box:
                        Gizmos.matrix = box.transform.localToWorldMatrix;
                        if (color.a > 0f)
                            Gizmos.DrawCube(box.center, box.size);
                        Gizmos.color = new Color(color.r, color.g, color.b, Mathf.Min(1f, color.a + 0.2f));
                        Gizmos.DrawWireCube(box.center, box.size);
                        labelWorldPos = box.transform.TransformPoint(box.center + Vector3.up * (box.size.y * 0.5f + 0.06f));
                        break;

                    case CapsuleCollider capsule:
                        Gizmos.matrix = capsule.transform.localToWorldMatrix;
                        DrawCapsuleApprox(capsule, color);
                        float capsuleHeight = capsule.height * 0.5f;
                        labelWorldPos = capsule.transform.TransformPoint(capsule.center + Vector3.up * (capsuleHeight + 0.06f));
                        break;

                    default:
                        Bounds bounds = collider.bounds;
                        Gizmos.matrix = Matrix4x4.identity;
                        if (color.a > 0f)
                            Gizmos.DrawCube(bounds.center, bounds.size);
                        Gizmos.color = new Color(color.r, color.g, color.b, Mathf.Min(1f, color.a + 0.2f));
                        Gizmos.DrawWireCube(bounds.center, bounds.size);
                        labelWorldPos = bounds.center + Vector3.up * (bounds.extents.y + 0.06f);
                        break;
                }

#if UNITY_EDITOR
                if (enabled && i == 0)
                {
                    Handles.color = new Color(1f, 0.35f, 0.35f, 0.95f);
                    Handles.Label(labelWorldPos, "ACTIVE");
                }
#endif
            }

            Gizmos.matrix = previousMatrix;
            Gizmos.color = previousColor;

            DrawAttackGeometryGizmo(selected);
        }

        private static void DrawCapsuleApprox(CapsuleCollider capsule, Color color)
        {
            Vector3 size;
            switch (capsule.direction)
            {
                case 0:
                    size = new Vector3(capsule.height, capsule.radius * 2f, capsule.radius * 2f);
                    break;
                case 1:
                    size = new Vector3(capsule.radius * 2f, capsule.height, capsule.radius * 2f);
                    break;
                default:
                    size = new Vector3(capsule.radius * 2f, capsule.radius * 2f, capsule.height);
                    break;
            }

            if (color.a > 0f)
                Gizmos.DrawCube(capsule.center, size);

            Gizmos.color = new Color(color.r, color.g, color.b, Mathf.Min(1f, color.a + 0.2f));
            Gizmos.DrawWireCube(capsule.center, size);
        }

        private void DrawAttackGeometryGizmo(bool selected)
        {
            if (_attackGeometryRenderMode == AttackGeometryRenderMode.None)
                return;

            string geometryId = ResolveAttackGeometryId();
            if (string.IsNullOrWhiteSpace(geometryId))
                return;

            AttackClipGeometryDefinition definition = AttackClipGeometryLibrary.LoadOrNull(geometryId);
            if (definition == null)
                return;

            Transform root = transform.root != null ? transform.root : transform;
            switch (_attackGeometryRenderMode)
            {
                case AttackGeometryRenderMode.SingleClip:
                    AttackClipGeometryGizmoRenderer.DrawSingle(root, definition.GetClip(_singleGeometryClipIndex), selected);
                    break;

                case AttackGeometryRenderMode.UnifiedAll:
                    AttackClipGeometryGizmoRenderer.DrawUnified(root, definition, selected);
                    break;
            }
        }

        private string ResolveAttackGeometryId()
        {
            if (!string.IsNullOrWhiteSpace(_runtimeAttackGeometryId))
            {
                return _runtimeAttackGeometryId.Trim();
            }

            if (!string.IsNullOrWhiteSpace(_attackGeometryIdOverride))
            {
                return _attackGeometryIdOverride.Trim();
            }

            return null;
        }
    }
}
