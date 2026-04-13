using UnityEngine;

namespace BBBNexus
{
    /// <summary>
    /// 常驻交互感知器：每帧缓存当前最佳交互目标。
    /// 规则固定为：视线直击优先，否则选择范围内最近目标。
    /// </summary>
    public sealed class PlayerInteractionSensor : MonoBehaviour
    {
        private const int MaxOverlapCount = 24;
        private static readonly Collider[] Overlaps = new Collider[MaxOverlapCount];

        [Header("Sensor")]
        public BBBCharacterController Character;
        [Min(0.1f)] public float InteractionRadius = 2.4f;
        public float InteractionHeightOffset = 1.1f;
        public float RaycastDistancePadding = 0.6f;
        public LayerMask InteractionMask = ~0;

        public IInteractable CurrentInteractable { get; private set; }
        public Transform CurrentTransform { get; private set; }
        public Vector3 CurrentPoint { get; private set; }
        public string CurrentPromptText { get; private set; }
        public bool HasInteractable => CurrentInteractable != null;

        private void Awake()
        {
            if (Character == null)
                Character = GetComponentInParent<BBBCharacterController>();
        }

        public void Tick()
        {
            if (Character == null)
            {
                ClearCurrent();
                return;
            }

            if (Character.ReadingOverlay != null && Character.ReadingOverlay.IsOpen)
            {
                ClearCurrent();
                return;
            }

            if (Character.RuntimeData != null && Character.RuntimeData.IsInventoryOpen)
            {
                ClearCurrent();
                return;
            }

            if (TryFindDirectLookTarget(out IInteractable directInteractable, out Transform directTransform, out Vector3 directPoint))
            {
                SetCurrent(directInteractable, directTransform, directPoint);
                return;
            }

            if (TryFindNearestTarget(out IInteractable nearestInteractable, out Transform nearestTransform, out Vector3 nearestPoint))
            {
                SetCurrent(nearestInteractable, nearestTransform, nearestPoint);
                return;
            }

            ClearCurrent();
        }

        private bool TryFindDirectLookTarget(out IInteractable interactable, out Transform targetTransform, out Vector3 point)
        {
            interactable = null;
            targetTransform = null;
            point = Vector3.zero;

            Transform cameraTransform = Character.PlayerCamera;
            if (cameraTransform == null)
                return false;

            float maxDistance = InteractionRadius + RaycastDistancePadding;
            if (!Physics.Raycast(
                    cameraTransform.position,
                    cameraTransform.forward,
                    out RaycastHit hit,
                    maxDistance,
                    InteractionMask,
                    QueryTriggerInteraction.Collide))
            {
                return false;
            }

            interactable = hit.collider != null ? hit.collider.GetComponentInParent<IInteractable>() : null;
            if (interactable == null || !interactable.CanInteract(Character))
            {
                interactable = null;
                return false;
            }

            targetTransform = interactable.GetInteractionTransform();
            point = targetTransform != null ? targetTransform.position : hit.point;
            return true;
        }

        private bool TryFindNearestTarget(out IInteractable bestInteractable, out Transform bestTransform, out Vector3 bestPoint)
        {
            bestInteractable = null;
            bestTransform = null;
            bestPoint = Vector3.zero;

            Vector3 origin = Character.transform.position + Vector3.up * InteractionHeightOffset;
            int count = Physics.OverlapSphereNonAlloc(origin, InteractionRadius, Overlaps, InteractionMask, QueryTriggerInteraction.Collide);
            if (count <= 0)
                return false;

            float bestSqrDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                Collider collider = Overlaps[i];
                if (collider == null)
                    continue;

                var interactable = collider.GetComponentInParent<IInteractable>();
                if (interactable == null || !interactable.CanInteract(Character))
                    continue;

                Transform interactionTransform = interactable.GetInteractionTransform();
                Vector3 interactionPoint = interactionTransform != null ? interactionTransform.position : collider.bounds.center;
                float sqrDistance = (interactionPoint - origin).sqrMagnitude;
                if (sqrDistance >= bestSqrDistance)
                    continue;

                bestSqrDistance = sqrDistance;
                bestInteractable = interactable;
                bestTransform = interactionTransform;
                bestPoint = interactionPoint;
            }

            return bestInteractable != null;
        }

        private void SetCurrent(IInteractable interactable, Transform targetTransform, Vector3 point)
        {
            CurrentInteractable = interactable;
            CurrentTransform = targetTransform;
            CurrentPoint = point;
            CurrentPromptText = interactable != null ? interactable.GetPromptText(Character) : string.Empty;
        }

        private void ClearCurrent()
        {
            CurrentInteractable = null;
            CurrentTransform = null;
            CurrentPoint = Vector3.zero;
            CurrentPromptText = string.Empty;
        }
    }
}
