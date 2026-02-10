using UnityEngine;

namespace Items.Logic
{
    public class RangedDevice : DeviceController
    {
        [Header("射击设置 (Ranged Specific)")]
        [Tooltip("枪口位置 (火光/射线起点)")]
        public Transform MuzzlePoint;

        [Tooltip("抛壳口位置 (可选)")]
        public Transform EjectionPoint;

        // 运行时状态
        private float _lastFireTime;

        public override void OnTriggerDown()
        {
            // 检查冷却
            if (Time.time - _lastFireTime < _config.Cooldown) return;

            _lastFireTime = Time.time;

            // 执行射击逻辑 (暂时留空，打桩)
            Fire();
        }

        public override void OnTriggerUp()
        {
            // 对于自动武器，这里停止连射
        }

        private void Fire()
        {
            Debug.Log($"[Device] {_config.Name} 开火了! (Bang!)");

            // TODO: 
            // 1. 播放枪口特效 (MuzzleFlash)
            // 2. 发射射线 (Raycast)
            // 3. 播放音效
            // 4. 应用后坐力 (Recoil)
            // 5. 扣除弹药
        }
    }
}
