using UnityEngine;

namespace BBBNexus
{
    public abstract class NavigatorSensorBase : MonoBehaviour, INavigatorSensor
    {
        [Header("Sensor Base Settings")]
        [Tooltip("导航目标。可在 Inspector 序列化配置，或由外部在运行时注入。")]
        [SerializeField] private Transform _target;

        public Transform Target
        {
            get => _target;
            set => _target = value;
        }

        protected NavigationContext _currentContext;

        protected virtual void Awake()
        {
            TryResolveTarget();
        }

        protected virtual void OnEnable()
        {
            TryResolveTarget();
        }

        protected virtual void Update()
        {
            if (_target == null)
            {
                TryResolveTarget();
            }

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

        private void TryResolveTarget()
        {
            if (_target != null)
            {
                return;
            }

            if (BBBCharacterController.PlayerInstance != null)
            {
                _target = BBBCharacterController.PlayerInstance.transform;
                return;
            }

            var go = GameObject.FindGameObjectWithTag("Player");
            if (go != null)
            {
                _target = go.transform;
            }
        }
    }
}
