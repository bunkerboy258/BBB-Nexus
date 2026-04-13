using System.Collections.Generic;
using UnityEngine;
using System;
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

        [Header("Debug - Attack Window Visualization")]
        [Tooltip("是否使用运行时攻击窗口数据来绘制 Gizmo。启用后，只会显示当前伤害窗口内的几何体。")]
        [SerializeField] private bool _useRuntimeAttackWindowForGizmo = false;
        [Tooltip("伤害窗口 Gizmo 颜色")]
        [SerializeField] private Color _damageWindowGizmoColor = new Color(1f, 0.18f, 0.18f, 0.25f);
        [Tooltip("对齐窗口 Gizmo 颜色")]
        [SerializeField] private Color _alignmentWindowGizmoColor = new Color(0.18f, 0.55f, 0.92f, 0.2f);

        private readonly List<Collider> _detectionColliders = new List<Collider>();
        private MeleeHitScanner _scanner;
        private AttackClipGeometryDefinition _runtimeAttackGeometry;
        private string _runtimeAttackGeometryId; // 废弃，保留兼容

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

        public void SetAttackGeometryDefinition(AttackClipGeometryDefinition definition)
        {
            _runtimeAttackGeometry = definition;
        }

        [Obsolete("改用 SetAttackGeometryDefinition")]
        public void SetAttackGeometryId(string geometryId)
        {
            // 兼容旧代码，但不再使用
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

        private static void DrawCapsuleApprox(AttackGeometryShapeDefinition shape, Color color)
        {
            Vector3 size = new Vector3(
                Mathf.Max(0.001f, shape.Radius * 2f),
                Mathf.Max(shape.Radius * 2f, shape.Height),
                Mathf.Max(0.001f, shape.Radius * 2f));

            if (color.a > 0f)
                Gizmos.DrawCube(Vector3.zero, size);

            Gizmos.color = new Color(color.r, color.g, color.b, Mathf.Min(1f, color.a + 0.2f));
            Gizmos.DrawWireCube(Vector3.zero, size);
        }

        private static void DrawRuntimeSphere(AttackGeometryShapeDefinition shape, Color fillColor)
        {
            float radius = Mathf.Max(0.001f, shape.Radius);
            if (fillColor.a > 0f)
            {
                Gizmos.DrawSphere(Vector3.zero, radius);
            }

            Gizmos.color = new Color(fillColor.r, fillColor.g, fillColor.b, Mathf.Min(1f, fillColor.a + 0.24f));
            Gizmos.DrawWireSphere(Vector3.zero, radius);
        }

        private static void DrawRuntimeBox(AttackGeometryShapeDefinition shape, Color fillColor)
        {
            Vector3 size = (Vector3)shape.HalfExtents * 2f;
            size.x = Mathf.Max(0.001f, size.x);
            size.y = Mathf.Max(0.001f, size.y);
            size.z = Mathf.Max(0.001f, size.z);

            if (fillColor.a > 0f)
            {
                Gizmos.DrawCube(Vector3.zero, size);
            }

            Gizmos.color = new Color(fillColor.r, fillColor.g, fillColor.b, Mathf.Min(1f, fillColor.a + 0.24f));
            Gizmos.DrawWireCube(Vector3.zero, size);
        }

        private void DrawAttackGeometryGizmo(bool selected)
        {
            // 如果启用了运行时攻击窗口可视化，优先使用调试服务的数据
            if (_useRuntimeAttackWindowForGizmo && AttackWindowDebugService.HasActiveWindow)
            {
                DrawRuntimeAttackWindowGizmo(selected);
                return;
            }

            if (_attackGeometryRenderMode == AttackGeometryRenderMode.None)
                return;

            AttackClipGeometryDefinition definition = _runtimeAttackGeometry;
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

        private void DrawRuntimeAttackWindowGizmo(bool selected)
        {
            if (!AttackWindowDebugService.ActiveContext.HasValue)
                return;

            var context = AttackWindowDebugService.ActiveContext.Value;
            float currentTime = Time.time;

            // 绘制当前时间对应的伤害窗口几何体
            int currentWindowIndex = AttackWindowDebugService.GetCurrentDamageWindowIndex(currentTime);
            float normalizedProgress = AttackWindowDebugService.GetNormalizedProgress(currentTime);

            AttackClipGeometryDefinition definition = _runtimeAttackGeometry;
            if (definition == null)
                return;

            AttackClipGeometryClipDefinition clip = definition.GetClip(context.ComboIndex);
            if (clip == null || clip.Samples == null)
                return;

            Transform root = transform.root != null ? transform.root : transform;

            // 绘制所有在归一化进度之前的采样点 (表示已经过的攻击轨迹)
            for (int i = 0; i < clip.Samples.Count; i++)
            {
                AttackGeometrySampleDefinition sample = clip.Samples[i];
                if (sample?.Shapes == null)
                    continue;

                // 只绘制在当前归一化进度之前的采样点
                if (sample.SweepProgressNormalized > normalizedProgress)
                    continue;

                // 根据当前伤害窗口索引调整颜色
                Color sampleColor = _damageWindowGizmoColor;
                float sampleProgress = sample.SweepProgressNormalized;
                if (currentWindowIndex >= 0)
                {
                    // 如果该采样点在当前伤害窗口内，使用更亮的颜色
                    float windowStartNorm = context.DominantEnd > 0f ?
                        (context.WindowStartTimes[currentWindowIndex] - context.StartTime) / context.DominantEnd : 0f;
                    float windowEndNorm = context.DominantEnd > 0f ?
                        (context.WindowEndTimes[currentWindowIndex] - context.StartTime) / context.DominantEnd : 1f;

                    if (sampleProgress >= windowStartNorm && sampleProgress <= windowEndNorm)
                    {
                        sampleColor = _activeGizmoColor;
                        sampleColor.a = selected ? 0.35f : 0.25f;
                    }
                    else
                    {
                        sampleColor = _damageWindowGizmoColor;
                        sampleColor.a = selected ? 0.18f : 0.12f;
                    }
                }
                else
                {
                    sampleColor = _inactiveGizmoColor;
                    sampleColor.a = selected ? 0.15f : 0.08f;
                }

                DrawRuntimeSample(root, sample, sampleColor, selected,
                    $"{context.ComboIndex} {Mathf.RoundToInt(sampleProgress * 100f)}%");
            }

            // 绘制对齐窗口指示器 (使用蓝色线框)
            if (context.AlignmentEndTime > context.AlignmentStartTime)
            {
                DrawAlignmentWindowIndicator(root, context, selected);
            }
        }

        private void DrawRuntimeSample(
            Transform root,
            AttackGeometrySampleDefinition sample,
            Color fillColor,
            bool selected,
            string label)
        {
            if (sample.Shapes == null)
                return;

            Matrix4x4 previousMatrix = Gizmos.matrix;
            Color previousColor = Gizmos.color;

            for (int i = 0; i < sample.Shapes.Count; i++)
            {
                AttackGeometryShapeDefinition shape = sample.Shapes[i];
                Matrix4x4 matrix = Matrix4x4.TRS(
                    root.TransformPoint((Vector3)shape.LocalPosition),
                    root.rotation * shape.LocalRotation,
                    Vector3.one);

                Gizmos.matrix = matrix;
                Gizmos.color = fillColor;

                switch (shape.ShapeType)
                {
                    case AttackGeometryShapeType.Sphere:
                        DrawRuntimeSphere(shape, fillColor);
                        break;

                    case AttackGeometryShapeType.Capsule:
                        DrawCapsuleApprox(shape, fillColor);
                        break;

                    case AttackGeometryShapeType.Box:
                    default:
                        DrawRuntimeBox(shape, fillColor);
                        break;
                }

#if UNITY_EDITOR
                if (selected && i == 0)
                {
                    Handles.color = WithAlpha(fillColor, 0.95f);
                    Handles.Label(root.TransformPoint((Vector3)shape.LocalPosition) + root.up * 0.04f, label);
                }
#endif
            }

            Gizmos.matrix = previousMatrix;
            Gizmos.color = previousColor;
        }

        private void DrawAlignmentWindowIndicator(Transform root, AttackWindowDebugService.AttackWindowDebugContext context, bool selected)
        {
            // 在对齐窗口的起始和结束位置绘制蓝色圆环标记
            float alignStartNorm = context.DominantEnd > 0f ? 
                (context.AlignmentStartTime - context.StartTime) / context.DominantEnd : 0f;
            float alignEndNorm = context.DominantEnd > 0f ? 
                (context.AlignmentEndTime - context.StartTime) / context.DominantEnd : 0f;

            Color alignColor = _alignmentWindowGizmoColor;
            alignColor.a = selected ? 0.3f : 0.2f;

            // 绘制对齐窗口范围的文字标签
#if UNITY_EDITOR
            Vector3 labelPos = root.position + Vector3.up * 1.5f;
            Handles.color = alignColor;
            Handles.Label(labelPos, 
                $"对齐窗口：{alignStartNorm:F2} - {alignEndNorm:F2}\n" +
                $"伤害窗口：{context.WindowStartTimes.Length} 个");
#endif
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
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
