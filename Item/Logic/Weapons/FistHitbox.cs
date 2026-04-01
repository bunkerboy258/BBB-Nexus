using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace BBBNexus
{
    /// <summary>
    /// Fist hitbox driven by <see cref="FistsBehaviour"/>.
    /// It only deals damage while the attack window is active.
    /// </summary>
    public class FistHitbox : MonoBehaviour
    {
        [Header("Damage")]
        public float Damage = 10f;

        [Header("Debug")]
        [SerializeField] private bool _drawGizmo = true;
        [SerializeField] private Color _inactiveGizmoColor = new Color(0.78f, 0.78f, 0.78f, 0f);
        [SerializeField] private Color _activeGizmoColor = new Color(1f, 0.2f, 0.2f, 0.14f);

        private Collider _hitCollider;
        private MeleeHitScanner _scanner;

        private void Awake()
        {
            _hitCollider = GetComponent<Collider>();
            _scanner = new MeleeHitScanner(_hitCollider);
            enabled = false;
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

        private void FixedUpdate()
        {
            if (!enabled)
                return;

            PerformScan();
        }

        private void PerformScan()
        {
            _scanner?.Scan(Damage);
        }

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

            Collider collider = _hitCollider != null ? _hitCollider : GetComponent<Collider>();
            if (collider == null)
                return;

            Color color = enabled ? _activeGizmoColor : _inactiveGizmoColor;
            if (selected)
                color.a = Mathf.Min(1f, color.a + 0.15f);

            Matrix4x4 previousMatrix = Gizmos.matrix;
            Color previousColor = Gizmos.color;

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
            if (enabled)
            {
                Handles.color = new Color(1f, 0.35f, 0.35f, 0.95f);
                Handles.Label(labelWorldPos, "ACTIVE");
            }
#endif

            Gizmos.matrix = previousMatrix;
            Gizmos.color = previousColor;
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
    }
}
