using UnityEngine;
using UnityEngine.Events;

namespace BBBNexus
{
    /// <summary>
    /// 已停用：BBB 内不再承载理智逻辑。
    /// </summary>
    public class SunlightSanitySystem : MonoBehaviour
    {
        public UnityEvent OnSanityDepleted;
        public UnityEvent<float> OnSanityChanged;

        public float CurrentSanity => 0f;
        public float MaxSanity => 0f;
        public bool IsExposedToSun => false;
        public bool IsEyesClosed => false;

        public void NotifySanityStateChanged() { }

        public void ApplyMaxSanity(float maxSanity, bool refillCurrent = true) { }
    }
}
