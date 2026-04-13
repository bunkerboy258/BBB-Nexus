using UnityEngine;

namespace BBBNexus
{
public sealed class PlayerSpawnPoint : MonoBehaviour
{
    [SerializeField] private string _spawnId = "default";
    [SerializeField] private bool _isDefault = true;

    public string SpawnId => _spawnId;
    public bool IsDefault => _isDefault;

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.25f, 0.95f, 0.35f, 0.9f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.2f, 0.3f);
        Gizmos.DrawRay(transform.position + Vector3.up * 0.1f, transform.forward * 1.2f);
    }
#endif
}
}
