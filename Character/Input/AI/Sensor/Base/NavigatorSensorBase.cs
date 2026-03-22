using UnityEngine;

namespace BBBNexus
{
    public abstract class NavigatorSensorBase : MonoBehaviour, INavigatorSensor
    {
        [Header("Sensor Base Settings")]
        [Tooltip("导航目标。可在 Inspector 序列化配置，或由外部在运行时注入。")]
        [SerializeField] private Transform _target;

        /// <summary>
        /// 对外暴露 Target（兼容旧代码）
        /// </summary>
        public Transform Target
        {
            get => _target;
            set => _target = value;
        }

        protected NavigationContext _currentContext;

        protected virtual void Awake()
        {
            // 对象池/预制体场景兜底：如果未手动配置 Target，尝试找 Player tag 作为默认目标。
            // 该查找仅在 Awake 执行一次（不在 Update 每帧找）。
            if (_target == null)
            {
                var go = GameObject.FindGameObjectWithTag("Player");
                if (go != null) _target = go.transform;
            }
        }

        protected virtual void OnEnable()
        {
            // 池化复用时：Awake 不会重复调用，OnEnable 兜底补一次。
            if (_target == null)
            {
                var go = GameObject.FindGameObjectWithTag("Player");
                if (go != null) _target = go.transform;
            }
        }

        protected virtual void Update()
        {
            if (_target == null)
            {
                _currentContext = new NavigationContext(Vector3.zero, Vector3.zero, 0f, false, false);
                return;
            }

            ProcessSensorLogic();
        }

        protected abstract void ProcessSensorLogic();

        public ref readonly NavigationContext GetCurrentContext()
        {
            return ref _currentContext;
        }

        public void DrawGizmos()
        {
            OnDrawGizmos();
        }

        protected virtual void OnDrawGizmos()
        {
            if (_target != null && _currentContext.HasValidTarget)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawRay(transform.position + Vector3.up, _currentContext.DesiredWorldDirection * 2f);
            }
        }
    }
}