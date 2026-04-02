using UnityEngine;
using UnityEngine.Events;

namespace BBBNexus
{
    /// <summary>
    /// 可直接挂在场景对象上的轻量交互组件。
    /// 负责基础距离/朝向判定，并可选播放一个交互动画后触发事件。
    /// </summary>
    public class SimpleInteractable : MonoBehaviour, IInteractable
    {
        [Header("Interaction")]
        public bool IsEnabled = true;
        public Transform InteractionPoint;
        [Min(0.1f)] public float MaxDistance = 2f;
        public bool RequireFacing = true;
        [Range(-1f, 1f)] public float MinFacingDot = -0.15f;
        public string PromptText = "互动";

        [Header("Optional Interaction Animation")]
        public AnimationClip InteractionClip;
        public int InteractionPriority = 25;
        [Min(0f)] public float FadeDuration = 0.15f;
        public bool ApplyGravity = true;

        [Header("Events")]
        public UnityEvent OnInteracted;

        public bool CanInteract(BBBCharacterController interactor)
        {
            if (!IsEnabled || !isActiveAndEnabled || interactor == null)
                return false;

            Transform anchor = GetInteractionTransform();
            Vector3 actorPosition = interactor.transform.position;
            Vector3 targetPosition = anchor != null ? anchor.position : transform.position;

            Vector3 offset = targetPosition - actorPosition;
            offset.y = 0f;
            if (offset.magnitude > MaxDistance)
                return false;

            if (!RequireFacing || offset.sqrMagnitude <= 0.0001f)
                return true;

            Vector3 forward = interactor.PlayerCamera != null ? interactor.PlayerCamera.forward : interactor.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude <= 0.0001f)
                forward = interactor.transform.forward;

            return Vector3.Dot(forward.normalized, offset.normalized) >= MinFacingDot;
        }

        public Transform GetInteractionTransform()
        {
            return InteractionPoint != null ? InteractionPoint : transform;
        }

        public string GetPromptText(BBBCharacterController interactor)
        {
            return string.IsNullOrWhiteSpace(PromptText) ? "互动" : PromptText;
        }

        public bool TryGetInteractionRequest(BBBCharacterController interactor, out ActionRequest request)
        {
            if (InteractionClip == null)
            {
                request = default;
                return false;
            }

            request = new ActionRequest(InteractionClip, InteractionPriority, FadeDuration, ApplyGravity);
            return true;
        }

        public void Interact(BBBCharacterController interactor)
        {
            OnInteracted?.Invoke();
        }
    }
}
