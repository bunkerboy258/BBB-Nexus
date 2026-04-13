using UnityEngine;

namespace BBBNexus
{
public sealed class DebugRespawnPoint : MonoBehaviour
{
    [SerializeField] private float _selectionBias = 0f;

    public float SelectionBias => _selectionBias;

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.2f, 0.75f, 1f, 0.9f);
        Gizmos.DrawWireCube(transform.position + Vector3.up * 0.35f, new Vector3(0.5f, 0.7f, 0.5f));
        Gizmos.DrawRay(transform.position + Vector3.up * 0.1f, transform.forward * 1.1f);
    }
#endif
}
}
