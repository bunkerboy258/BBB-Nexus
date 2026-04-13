using UnityEngine;

namespace BBBNexus
{
    /// <summary>
    /// 脊椎朝向驱动器：在 Animancer 动画应用完毕后，对脊椎骨施加 Y 轴旋转修正。
    ///
    /// 两个约束：
    ///   非瞄准时 — (gunPivot - spine).XZ 对齐 character.transform.forward.XZ（移动朝向）
    ///   瞄准时   — (gunPivot - spine).XZ 对齐 (TargetAimPoint - spine).XZ（准星意图世界点）
    ///
    /// [DefaultExecutionOrder(1000)] 保证晚于 Animancer LateUpdate 执行。
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class SpineAimDriver : MonoBehaviour
    {
        [Header("--- 引用 ---")]
        [SerializeField] private BBBCharacterController _character;
        [Tooltip("脊椎骨骼 Transform（MyPlayerArmature/Spine）")]
        [SerializeField] private Transform _spine;

        [Header("--- 参数 ---")]
        [Tooltip("最大旋转角度（度），超出截断防止骨骼扭曲")]
        [SerializeField] private float _maxAngle = 75f;
        [Tooltip("旋转权重 0~1")]
        [SerializeField][Range(0f, 1f)] private float _weight = 1f;
        [Tooltip("角度平滑时间（秒），0 = 瞬时到位")]
        [SerializeField] private float _smoothTime = 0.08f;

        private float _currentAngle;
        private float _angleVelocity;

        private void LateUpdate()
        {
            if (_character == null || _spine == null)
                return;

            var data = _character.RuntimeData;
            if (data == null)
                return;

            bool hasWeapon = data.CurrentAimReference != null;
            float targetAngle = 0f;

            if (hasWeapon)
            {
                // 当前枪管方向：(muzzle - spine) 的 XZ 投影
                Vector3 currentDir = data.CurrentAimReference.position - _spine.position;
                currentDir.y = 0f;

                // 目标方向：
                //   非瞄准 → 角色移动朝向（character.forward）
                //   瞄准中 → 准星意图世界点方向（TargetAimPoint - spine）
                Vector3 targetDir;
                if (data.IsTacticalStance)
                {
                    targetDir = data.TargetAimPoint - _spine.position;
                    targetDir.y = 0f;
                }
                else
                {
                    targetDir = _character.transform.forward;
                    targetDir.y = 0f;
                }

                if (currentDir.sqrMagnitude > 0.0001f && targetDir.sqrMagnitude > 0.0001f)
                {
                    float rawAngle = Vector3.SignedAngle(currentDir, targetDir, Vector3.up);
                    targetAngle = Mathf.Clamp(rawAngle, -_maxAngle, _maxAngle) * _weight;
                }
            }

            _currentAngle = _smoothTime > 0f
                ? Mathf.SmoothDamp(_currentAngle, targetAngle, ref _angleVelocity, _smoothTime)
                : targetAngle;

            _spine.Rotate(Vector3.up, _currentAngle, Space.World);
        }
    }
}
