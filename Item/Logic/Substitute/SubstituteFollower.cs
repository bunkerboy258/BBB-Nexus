using UnityEngine;

namespace BBBNexus
{
    /// <summary>
    /// 替身追踪器
    ///
    /// 格挡触发时由外部实例化并调用 Init()：
    /// - 持有玩家当前帧的烘焙 Mesh（BakeMesh 快照），冻结姿势
    /// - 从玩家位置快速冲向攻击武器，"抵"在武器上
    /// - 监听攻击者的 StatusEffectArbiter，状态结束后自毁
    ///
    /// 使用裁剪平面材质（SubstituteArm.shader）只显示手臂部分。
    /// </summary>
    public class SubstituteFollower : MonoBehaviour
    {
        [Header("追踪参数")]

        [Tooltip("冲出时的初始速度（从玩家位置飞向武器）")]
        public float EmergeSpeed = 20f;

        [Tooltip("贴附武器后的跟随速度")]
        public float FollowSpeed = 30f;

        [Tooltip("判定-已到达武器位置-的距离阈值（米），到达后切换为跟随模式）")]
        public float ArrivalThreshold = 0.15f;

        [Header("Debug")]
        public bool DebugLog = false;

        // ── 运行时状态 ──────────────────────────────────────────────────

        private MeshFilter _meshFilter;
        private Transform _weaponTransform;
        private BBBCharacterController _attacker;

        private bool _arrived;         // 是否已到达武器位置
        private bool _initialized;
        private Mesh _bakedMesh;       // 持有引用，用于最终释放

        // ── 初始化 ──────────────────────────────────────────────────────

        /// <summary>
        /// 由格挡触发逻辑调用。
        /// </summary>
        /// <param name="bakedMesh">玩家当前帧 BakeMesh 快照，调用方负责传入，本类负责销毁。</param>
        /// <param name="weaponTransform">攻击武器的 Transform（用于追踪位置）。</param>
        /// <param name="attacker">攻击者角色，监听其 StatusEffectArbiter 状态结束。</param>
        public void Init(Mesh bakedMesh, Transform weaponTransform, BBBCharacterController attacker)
        {
            _meshFilter     = GetComponentInChildren<MeshFilter>();
            _weaponTransform = weaponTransform;
            _attacker       = attacker;
            _bakedMesh      = bakedMesh;
            _arrived        = false;

            if (_meshFilter != null)
                _meshFilter.sharedMesh = bakedMesh;
            else
                Debug.LogWarning("[SubstituteFollower] 未找到 MeshFilter，替身无法显示。", this);

            _initialized = true;

            if (DebugLog)
                Debug.Log($"[SubstituteFollower] 初始化完成，目标武器={weaponTransform?.name}，攻击者={attacker?.name}", this);
        }

        // ── 每帧逻辑 ────────────────────────────────────────────────────

        private void Update()
        {
            if (!_initialized) return;

            // 攻击者或武器消失时立即自毁
            if (_attacker == null || _weaponTransform == null)
            {
                Despawn();
                return;
            }

            // 状态结束 → 自毁
            if (!_attacker.StatusEffects.IsActive)
            {
                if (DebugLog)
                    Debug.Log("[SubstituteFollower] 攻击者状态结束，替身消散。", this);
                Despawn();
                return;
            }

            var targetPos = _weaponTransform.position;
            var currentPos = transform.position;
            float speed = _arrived ? FollowSpeed : EmergeSpeed;

            transform.position = Vector3.MoveTowards(currentPos, targetPos, speed * Time.deltaTime);

            // 面向攻击者（让截断平面朝向正确）
            var toAttacker = _attacker.transform.position - transform.position;
            if (toAttacker.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(toAttacker.normalized, Vector3.up);

            // 到达武器位置后切换为跟随模式
            if (!_arrived && Vector3.Distance(transform.position, targetPos) <= ArrivalThreshold)
            {
                _arrived = true;
                if (DebugLog)
                    Debug.Log("[SubstituteFollower] 已抵达武器位置，切换为跟随模式。", this);
            }
        }

        // ── 清理 ────────────────────────────────────────────────────────

        private void Despawn()
        {
            _initialized = false;

            // 释放运行时创建的 Mesh，避免内存泄漏
            if (_bakedMesh != null)
            {
                if (_meshFilter != null)
                    _meshFilter.sharedMesh = null;

                Destroy(_bakedMesh);
                _bakedMesh = null;
            }

            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            // 兜底：即使从外部 Destroy 也释放 Mesh
            if (_bakedMesh != null)
            {
                Destroy(_bakedMesh);
                _bakedMesh = null;
            }
        }
    }
}
