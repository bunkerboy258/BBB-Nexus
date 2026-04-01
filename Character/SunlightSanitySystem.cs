using UnityEngine;
using UnityEngine.Events;

namespace BBBNexus
{
    /// <summary>
    /// 理智规则驱动：
    /// - 闭眼：缓慢持续扣除理智
    /// - 睁眼且暴晒：快速扣除理智
    /// - 睁眼且非暴晒：按阶段递增的速率恢复理智
    /// 权威数据始终在 BBBCharacterController.RuntimeData。
    /// </summary>
    public class SunlightSanitySystem : MonoBehaviour
    {
        [Header("--- 角色引用 ---")]
        [Tooltip("玩家角色控制器，用于获取检测原点")]
        [SerializeField] private BBBCharacterController _character;

        [Header("--- 太阳光源 ---")]
        [Tooltip("场景中的平行光（太阳）；留空则自动查找第一个 Directional Light")]
        [SerializeField] private Light _sunLight;

        [Tooltip("射线起点相对角色脚底的垂直偏移（米），避免射线从地面内部发出")]
        [SerializeField] private float _rayOriginHeightOffset = 1.0f;

        [Tooltip("遮挡检测用的 LayerMask，通常排除角色自身层和触发器")]
        [SerializeField] private LayerMask _occlusionMask = ~0;

        [Header("--- 理智规则 ---")]
        [Tooltip("闭眼时每秒扣除的理智量")]
        [SerializeField] private float _eyesClosedDrainRate = 4f;

        [Tooltip("睁眼且暴露在阳光下时每秒扣除的理智量")]
        [SerializeField] private float _sunDrainRate = 12f;

        [Tooltip("睁眼且未暴晒时，第一阶段每秒恢复的理智量")]
        [SerializeField] private float _recoverRateStage1 = 3f;

        [Tooltip("睁眼且未暴晒时，第二阶段每秒恢复的理智量")]
        [SerializeField] private float _recoverRateStage2 = 7f;

        [Tooltip("睁眼且未暴晒时，第三阶段每秒恢复的理智量")]
        [SerializeField] private float _recoverRateStage3 = 12f;

        [Tooltip("从睁眼恢复开始，第一阶段持续时长")]
        [SerializeField] private float _recoverStage1Duration = 0.8f;

        [Tooltip("从睁眼恢复开始，第二阶段持续时长")]
        [SerializeField] private float _recoverStage2Duration = 2.0f;

        [Header("--- 事件 ---")]
        [Tooltip("理智归零时触发（仅触发一次，直到理智恢复）")]
        public UnityEvent OnSanityDepleted;
        [Tooltip("每帧理智变化时触发，参数为归一化值 0~1")]
        public UnityEvent<float> OnSanityChanged;

        public float CurrentSanity => Character != null ? Character.CurrentSanity : 0f;
        public float MaxSanity => Character != null ? Character.CurrentMaxSanity : 100f;
        /// <summary>当前是否暴露在阳光下</summary>
        public bool IsExposedToSun { get; private set; }
        public bool IsEyesClosed => EyesClosedSystemManager.Instance != null && EyesClosedSystemManager.Instance.IsEyesClosed;

        private bool _depletedFired;
        private float _openRecoveryTimer;
        private BBBCharacterController Character => _character != null ? _character : BBBCharacterController.PlayerInstance;

        private void Awake()
        {
            if (_character == null)
            {
                _character = BBBCharacterController.PlayerInstance;
            }

            if (_sunLight == null)
                TryFindSun();
        }

        private void TryFindSun()
        {
            var lights = FindObjectsOfType<Light>();
            foreach (var l in lights)
            {
                if (l.type == LightType.Directional && l.isActiveAndEnabled)
                {
                    _sunLight = l;
                    return;
                }
            }

            Debug.LogWarning("[SunlightSanity] 未找到场景内的 Directional Light，暴晒检测将始终返回 false。", this);
        }

        private void Update()
        {
            IsExposedToSun = CheckSunExposure();

            if (IsEyesClosed)
            {
                _openRecoveryTimer = 0f;
                ApplySanityDelta(-_eyesClosedDrainRate * Time.deltaTime);
            }
            else if (IsExposedToSun)
            {
                _openRecoveryTimer = 0f;
                ApplySanityDelta(-_sunDrainRate * Time.deltaTime);
            }
            else
            {
                _openRecoveryTimer += Time.deltaTime;
                ApplySanityDelta(GetOpenRecoveryRate(_openRecoveryTimer) * Time.deltaTime);
            }
        }

        private bool CheckSunExposure()
        {
            if (_sunLight == null || !_sunLight.isActiveAndEnabled)
                return false;

            Vector3 origin = GetRayOrigin();
            // 平行光的"来向"是 light.transform.forward 的反方向
            Vector3 toSun = -_sunLight.transform.forward;

            // 射线打向太阳方向，若未被遮挡则暴露
            return !Physics.Raycast(origin, toSun, Mathf.Infinity, _occlusionMask, QueryTriggerInteraction.Ignore);
        }

        private Vector3 GetRayOrigin()
        {
            if (_character != null)
                return _character.transform.position + Vector3.up * _rayOriginHeightOffset;

            return transform.position + Vector3.up * _rayOriginHeightOffset;
        }

        private float GetOpenRecoveryRate(float openDuration)
        {
            if (openDuration < _recoverStage1Duration)
            {
                return _recoverRateStage1;
            }

            if (openDuration < _recoverStage2Duration)
            {
                return _recoverRateStage2;
            }

            return _recoverRateStage3;
        }

        private void ApplySanityDelta(float delta)
        {
            if (Character == null || Mathf.Approximately(delta, 0f))
            {
                return;
            }

            float before = CurrentSanity;
            Character.AddSanityDelta(delta);
            float after = CurrentSanity;

            if (Mathf.Approximately(before, after))
            {
                return;
            }

            NotifySanityStateChanged();

            if (after <= 0f && before > 0f && !_depletedFired)
            {
                _depletedFired = true;
                OnSanityDepleted?.Invoke();
            }
            else if (after > 0f)
            {
                _depletedFired = false;
            }
        }

        public void NotifySanityStateChanged()
        {
            float normalized = MaxSanity > 0f ? CurrentSanity / MaxSanity : 0f;
            OnSanityChanged?.Invoke(normalized);
            if (CurrentSanity > 0f)
            {
                _depletedFired = false;
            }
        }

        public void ApplyMaxSanity(float maxSanity, bool refillCurrent = true)
        {
            Character?.SetMaxSanityValue(maxSanity, refillCurrent);
        }

#if UNITY_EDITOR
        [Header("--- Gizmo 调试 ---")]
        [Tooltip("面状射线的半径（米）")]
        [SerializeField] private float _gizmoDiscRadius = 0.6f;
        [Tooltip("面状射线的射线根数")]
        [SerializeField] private int _gizmoRayCount = 16;
        [Tooltip("射线显示长度（米）")]
        [SerializeField] private float _gizmoRayLength = 8f;

        private void OnDrawGizmos()
        {
            if (_sunLight == null) return;

            Vector3 origin = GetRayOrigin();
            Vector3 toSun = -_sunLight.transform.forward;

            Color exposed = new Color(1f, 0.1f, 0.1f, 0.85f);
            Color occluded = new Color(0.35f, 0.35f, 0.35f, 0.4f);
            Color col = IsExposedToSun ? exposed : occluded;

            // 求圆盘的切平面基向量
            Vector3 right = Vector3.Cross(toSun, Vector3.up);
            if (right.sqrMagnitude < 0.001f)
                right = Vector3.Cross(toSun, Vector3.forward);
            right.Normalize();
            Vector3 up = Vector3.Cross(right, toSun).normalized;

            // 圆盘边缘的采样点
            var rimPoints = new Vector3[_gizmoRayCount];
            for (int i = 0; i < _gizmoRayCount; i++)
            {
                float angle = i * Mathf.PI * 2f / _gizmoRayCount;
                rimPoints[i] = origin
                    + right * (Mathf.Cos(angle) * _gizmoDiscRadius)
                    + up    * (Mathf.Sin(angle) * _gizmoDiscRadius);
            }

            Gizmos.color = col;

            // 圆盘轮廓
            for (int i = 0; i < _gizmoRayCount; i++)
                Gizmos.DrawLine(rimPoints[i], rimPoints[(i + 1) % _gizmoRayCount]);

            // 圆心到轮廓的辐射线（面感）
            for (int i = 0; i < _gizmoRayCount; i++)
                Gizmos.DrawLine(origin, rimPoints[i]);

            // 每个轮廓点向太阳方向延伸的射线束
            Color rayCol = new Color(col.r, col.g, col.b, col.a * 0.55f);
            Gizmos.color = rayCol;
            for (int i = 0; i < _gizmoRayCount; i++)
                Gizmos.DrawRay(rimPoints[i], toSun * _gizmoRayLength);

            // 中心主射线（更亮）
            Gizmos.color = col;
            Gizmos.DrawRay(origin, toSun * _gizmoRayLength);
            Gizmos.DrawWireSphere(origin, 0.07f);
        }
#endif
    }
}
