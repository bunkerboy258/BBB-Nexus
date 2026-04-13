using UnityEngine;

namespace BBBNexus
{
[DisallowMultipleComponent]
[RequireComponent(typeof(SphereCollider))]
public sealed class CheckpointRespawnPoint : MonoBehaviour
{
    [SerializeField] private string _checkpointId = "checkpoint";
    [SerializeField] private bool _activateOnStart;
    [SerializeField] private float _selectionBias = 0f;

    public string CheckpointId => _checkpointId;
    public float SelectionBias => _selectionBias;

    private void Awake()
    {
        var trigger = GetComponent<SphereCollider>();
        if (trigger != null)
        {
            trigger.isTrigger = true;
        }
    }

    private void Start()
    {
        if (_activateOnStart && PlayerRespawnService.Instance != null)
        {
            PlayerRespawnService.Instance.SetActiveCheckpoint(this);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null)
        {
            return;
        }

        var player = other.GetComponentInParent<BBBCharacterController>();
        if (player == null || !player.CompareTag("Player") || PlayerRespawnService.Instance == null)
        {
            return;
        }

        PlayerRespawnService.Instance.SetActiveCheckpoint(this);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.75f, 0.2f, 0.9f);
        Gizmos.DrawWireCube(transform.position + Vector3.up * 0.35f, new Vector3(0.6f, 0.7f, 0.6f));
        Gizmos.DrawRay(transform.position + Vector3.up * 0.1f, transform.forward * 1.1f);
    }
#endif
}
}
