using UnityEngine;

namespace BBBNexus
{
    public abstract class NavigatorSensorBase : MonoBehaviour, INavigatorSensor
    {
        [Header("Sensor Base Settings")]
        public Transform Target;

        protected NavigationContext _currentContext;

        protected virtual void Update()
        {
            if (Target == null)
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
            if (Target != null && _currentContext.HasValidTarget)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawRay(transform.position + Vector3.up, _currentContext.DesiredWorldDirection * 2f);
            }
        }
    }
}