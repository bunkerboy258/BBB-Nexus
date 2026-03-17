using UnityEngine;

namespace BBBNexus
{
    public abstract class PlayerIKSourceBase : MonoBehaviour, IPlayerIKSource
    {
        public abstract void SetIKTarget(IKTarget target, Transform targetTransform, float weight);
        public abstract void SetIKTarget(IKTarget target, Vector3 position, Quaternion rotation, float weight);
        public abstract void UpdateIKWeight(IKTarget target, float weight);
        public abstract void EnableAllIK();
        public abstract void DisableAllIK();
    }
}

