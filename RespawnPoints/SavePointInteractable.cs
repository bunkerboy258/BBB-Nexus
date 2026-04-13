using UnityEngine;

namespace BBBNexus
{
/// <summary>
/// 存档点互动组件。
/// 玩家交互后绑定为复活点。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SphereCollider))]
public class SavePointInteractable : MonoBehaviour, IInteractable
{
    [Header("存档点设置")]
    [SerializeField] private string _savePointId = "savepoint";
    
    [Header("互动设置")]
    [SerializeField] private float _maxDistance = 2f;
    [SerializeField] private bool _requireFacing = true;
    [Range(-1f, 1f)] [SerializeField] private float _minFacingDot = -0.15f;
    
    [Header("提示文本")]
    [SerializeField] private string _promptText = "休息";
    [SerializeField] private string _messageTitle = "存档点";
    [SerializeField] private string _messageBody = "已记录进度。";

    public string SavePointId => _savePointId;

    private void Awake()
    {
        var collider = GetComponent<SphereCollider>();
        if (collider != null)
        {
            collider.isTrigger = true;
        }
    }


    public bool CanInteract(BBBCharacterController interactor)
    {
        if (interactor == null || !isActiveAndEnabled)
            return false;

        Vector3 toTarget = transform.position - interactor.transform.position;
        toTarget.y = 0f;
        if (toTarget.magnitude > _maxDistance)
            return false;

        if (!_requireFacing)
            return true;

        if (toTarget.sqrMagnitude <= 0.0001f)
            return true;

        Vector3 forward = interactor.PlayerCamera != null ? interactor.PlayerCamera.forward : interactor.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude <= 0.0001f)
            forward = interactor.transform.forward;

        return Vector3.Dot(forward.normalized, toTarget.normalized) >= _minFacingDot;
    }

    public Transform GetInteractionTransform()
    {
        return transform;
    }

    public string GetPromptText(BBBCharacterController interactor)
    {
        return string.IsNullOrWhiteSpace(_promptText) ? "休息" : _promptText;
    }

    public bool TryGetInteractionRequest(BBBCharacterController interactor, out ActionRequest request)
    {
        request = default;
        return false;
    }

    public void Interact(BBBCharacterController interactor)
    {
        if (interactor == null || PlayerRespawnService.Instance == null)
            return;

        // 1. 设置当前存档点为复活点
        PlayerRespawnService.Instance.SetActiveSavePoint(this);

        // 2. 显示提示
        interactor.ReadingOverlay?.Show(
            string.IsNullOrWhiteSpace(_messageTitle) ? "存档点" : _messageTitle,
            string.IsNullOrWhiteSpace(_messageBody) ? "已记录进度。" : _messageBody);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, _maxDistance);
        
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(transform.position + Vector3.up * 0.5f, new Vector3(0.4f, 1f, 0.4f));
        Gizmos.DrawRay(transform.position + Vector3.up * 0.5f, transform.forward * 0.8f);
    }
#endif
}
}
