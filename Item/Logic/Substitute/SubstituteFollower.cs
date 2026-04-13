using System.Collections.Generic;
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
    /// - 根据格挡类型（普通/完美）切换着色方案，并生成拖影（afterimage）
    ///
    /// 配套着色器：BBBNexus/SubstituteMesh
    /// </summary>
    public class SubstituteFollower : MonoBehaviour
    {
        // ── Inspector 参数 ─────────────────────────────────────────────

        [Header("追踪参数")]

        [Tooltip("冲出时的初始速度（从玩家位置飞向武器）")]
        public float EmergeSpeed = 20f;

        [Tooltip("贴附武器后的跟随速度")]
        public float FollowSpeed = 30f;

        [Tooltip("判定-已到达武器位置-的距离阈值（米），到达后切换为跟随模式）")]
        public float ArrivalThreshold = 0.15f;

        [Header("着色 — 普通格挡")]

        [Tooltip("普通格挡替身颜色（冷蓝色系）")]
        public Color NormalColor = new Color(0.35f, 0.75f, 1f, 0.85f);

        [Tooltip("普通格挡 Rim 发光强度")]
        public float NormalRimIntensity = 2.5f;

        [Header("着色 — 完美格挡")]

        [Tooltip("完美弹反替身颜色（金色系，凌驾于闭眼特效之上）")]
        public Color PerfectColor = new Color(1f, 0.85f, 0.15f, 0.95f);

        [Tooltip("完美弹反 Rim 发光强度（更强烈）")]
        public float PerfectRimIntensity = 5f;

        [Header("裁剪平面")]

        [Tooltip("启用世界 Y 裁剪，只显示武器高度以上的手臂区域")]
        public bool EnableClipPlane = true;

        [Tooltip("裁剪平面相对武器高度的偏移（负值 = 向下偏，显示更多躯干）")]
        public float ClipYOffset = -0.15f;

        [Header("拖影 (Afterimage)")]

        [Tooltip("是否启用拖影效果")]
        public bool EnableTrail = true;

        [Tooltip("每隔多少秒留下一个拖影快照")]
        public float TrailInterval = 0.05f;

        [Tooltip("单个拖影完全消散所需时间（秒）")]
        public float TrailFadeDuration = 0.28f;

        [Tooltip("场上最多同时存在的拖影数量")]
        public int TrailMaxCount = 6;

        [Header("替身音效")]
        [Tooltip("替身生成/飞出瞬间（随机选一个）")]
        public AudioClip[] EmergeSounds;

        [Tooltip("替身贴附武器瞬间（随机选一个）")]
        public AudioClip[] ArrivalSounds;

        [Tooltip("替身消散时（随机选一个）")]
        public AudioClip[] DespawnSounds;

        [Header("Debug")]
        public bool DebugLog = false;

        // ── 运行时状态 ──────────────────────────────────────────────────

        private MeshFilter   _meshFilter;
        private MeshRenderer _renderer;
        private Transform    _weaponTransform;
        private Transform    _mainhandSocket;      // 攻击者主手握持挂点，用于贴手追踪
        private Vector3      _mainhandLocalOffset; // 烘焙瞬间玩家主手相对其根节点的本地偏移
        private bool         _hasMainhandOffset;
        private BBBCharacterController _attacker;

        private bool  _arrived;
        private bool  _initialized;
        private bool  _isPerfectParry;
        private Mesh  _bakedMesh;

        // 材质属性块
        private MaterialPropertyBlock _mpb;
        private MaterialPropertyBlock _trailMpb;

        // 拖影：记录历史帧的世界矩阵 + 出生时间
        private struct TrailSnapshot
        {
            public Matrix4x4 matrix;
            public float     birthTime;
        }
        private readonly List<TrailSnapshot> _trailQueue = new(8);
        private float _nextTrailTime;

        // Shader 属性 ID（静态缓存，避免每帧字符串查找）
        private static readonly int _idTintColor    = Shader.PropertyToID("_TintColor");
        private static readonly int _idAlpha        = Shader.PropertyToID("_Alpha");
        private static readonly int _idRimIntensity = Shader.PropertyToID("_RimIntensity");
        private static readonly int _idClipY        = Shader.PropertyToID("_ClipY");

        // ── 初始化 ──────────────────────────────────────────────────────

        /// <summary>
        /// 由格挡触发逻辑调用。
        /// </summary>
        /// <param name="bakedMesh">玩家当前帧 BakeMesh 快照，本类负责销毁。</param>
        /// <param name="weaponTransform">攻击武器的 Transform（用于追踪位置）。</param>
        /// <param name="attacker">攻击者角色，监听其 StatusEffectArbiter 状态结束。</param>
        /// <param name="mainhandLocalOffset">烘焙瞬间玩家主手挂点相对玩家根节点的本地偏移（由 ParryHandler 传入）。</param>
        /// <param name="isPerfectParry">是否为完美弹反（影响着色方案）。</param>
        public void Init(Mesh bakedMesh, Transform weaponTransform, BBBCharacterController attacker, Vector3 mainhandLocalOffset, bool isPerfectParry = false)
        {
            _meshFilter           = GetComponentInChildren<MeshFilter>();
            _renderer             = GetComponentInChildren<MeshRenderer>();
            _weaponTransform      = weaponTransform;
            _attacker             = attacker;
            _mainhandSocket       = attacker != null ? attacker.MainhandWeaponContainer : null;
            _mainhandLocalOffset  = mainhandLocalOffset;
            _hasMainhandOffset    = mainhandLocalOffset != Vector3.zero;
            _bakedMesh       = bakedMesh;
            _isPerfectParry  = isPerfectParry;
            _arrived         = false;
            _trailQueue.Clear();
            _nextTrailTime   = Time.time;

            if (_meshFilter != null)
                _meshFilter.sharedMesh = bakedMesh;
            else
                Debug.LogWarning("[SubstituteFollower] 未找到 MeshFilter，替身无法显示。", this);

            // 初始化属性块并应用着色方案
            _mpb      = new MaterialPropertyBlock();
            _trailMpb = new MaterialPropertyBlock();
            ApplyStyle();

            _initialized = true;
            WeaponAudioUtil.PlayAt(EmergeSounds, transform.position);

            if (DebugLog)
                Debug.Log($"[SubstituteFollower] 初始化 mode={(_isPerfectParry ? "完美弹反" : "普通格挡")} " +
                          $"weapon={weaponTransform?.name} attacker={attacker?.name}", this);
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

            // ── 移动追踪 ──
            var gripSocket = (_mainhandSocket != null) ? _mainhandSocket : _weaponTransform;

            if (_arrived)
            {
                // 贴附阶段：每帧将锚点精确对齐攻击者握持点（Z 取反 = 绕 Y 旋转 180°）
                SnapAnchorToGrip(gripSocket);
            }
            else
            {
                // 冲出阶段：朝预估的根节点目标位置飞去
                var   targetRootPos = ComputeRootPosForGrip(gripSocket);
                float speed         = EmergeSpeed;
                transform.position  = Vector3.MoveTowards(transform.position, targetRootPos, speed * Time.deltaTime);

                var toAttacker = _attacker.transform.position - transform.position;
                if (toAttacker.sqrMagnitude > 0.001f)
                    transform.rotation = Quaternion.LookRotation(toAttacker.normalized, Vector3.up);
            }

            // 到达判定：根节点到目标根节点的距离
            if (!_arrived && Vector3.Distance(transform.position, ComputeRootPosForGrip(gripSocket)) <= ArrivalThreshold)
            {
                _arrived = true;
                WeaponAudioUtil.PlayAt(ArrivalSounds, transform.position);
                if (DebugLog)
                    Debug.Log("[SubstituteFollower] 已抵达武器位置，切换为跟随模式。", this);
            }

            // ── 动态更新裁剪平面 ──
            UpdateClipPlane();

            // ── 拖影 ──
            if (EnableTrail)
            {
                if (Time.time >= _nextTrailTime)
                {
                    RecordTrailSnapshot();
                    _nextTrailTime = Time.time + TrailInterval;
                }
                DrawTrailGhosts();
            }
        }

        // ── 握持对齐工具 ─────────────────────────────────────────────────

        /// <summary>
        /// 计算让玩家主手对齐 grip（Z 取反）时，替身根节点应在的世界坐标。
        /// 无偏移数据时直接返回 grip 位置（兼容旧行为）。
        /// </summary>
        private Vector3 ComputeRootPosForGrip(Transform grip)
        {
            if (!_hasMainhandOffset || grip == null)
                return grip != null ? grip.position : transform.position;

            // 根节点旋转 = grip 朝向旋转 180°（Z 取反，替身正面迎向攻击者）
            Quaternion rootRot = grip.rotation * Quaternion.Euler(0f, 180f, 0f);
            // 根节点位置 = grip 位置 - 主手偏移在该旋转下的世界向量
            return grip.position - rootRot * _mainhandLocalOffset;
        }

        /// <summary>
        /// 将根节点平移 + 旋转，使玩家主手位置精确贴合 grip（Z 取反）。
        /// </summary>
        private void SnapAnchorToGrip(Transform grip)
        {
            if (!_hasMainhandOffset || grip == null)
            {
                if (grip != null) transform.position = grip.position;
                return;
            }

            transform.rotation = grip.rotation * Quaternion.Euler(0f, 180f, 0f);
            transform.position = grip.position - transform.rotation * _mainhandLocalOffset;
        }

        // ── 着色工具 ─────────────────────────────────────────────────────

        private void ApplyStyle()
        {
            if (_renderer == null) return;

            _renderer.GetPropertyBlock(_mpb);
            Color  c   = _isPerfectParry ? PerfectColor : NormalColor;
            float  rim = _isPerfectParry ? PerfectRimIntensity : NormalRimIntensity;
            _mpb.SetColor(_idTintColor,    c);
            _mpb.SetFloat(_idAlpha,        1f);          // 主体始终完全不透明（由 TintColor.a 控制整体）
            _mpb.SetFloat(_idRimIntensity, rim);
            _renderer.SetPropertyBlock(_mpb);
        }

        private void UpdateClipPlane()
        {
            if (_renderer == null) return;

            float clipY = EnableClipPlane && _weaponTransform != null
                ? _weaponTransform.position.y + ClipYOffset
                : -999f;  // -999 = 不裁剪

            _renderer.GetPropertyBlock(_mpb);
            _mpb.SetFloat(_idClipY, clipY);
            _renderer.SetPropertyBlock(_mpb);
        }

        // ── 拖影工具 ────────────────────────────────────────────────────

        private void RecordTrailSnapshot()
        {
            if (_trailQueue.Count >= TrailMaxCount)
                _trailQueue.RemoveAt(0);          // 淘汰最老的

            _trailQueue.Add(new TrailSnapshot
            {
                matrix    = transform.localToWorldMatrix,
                birthTime = Time.time
            });
        }

        private void DrawTrailGhosts()
        {
            if (_bakedMesh == null || _renderer == null) return;

            Material mat = _renderer.sharedMaterial;
            if (mat == null) return;

            float now = Time.time;
            Color baseColor = _isPerfectParry ? PerfectColor : NormalColor;

            for (int i = 0; i < _trailQueue.Count; i++)
            {
                float age   = now - _trailQueue[i].birthTime;
                float alpha = Mathf.Clamp01(1f - age / TrailFadeDuration);
                if (alpha <= 0.01f) continue;

                // 越早的快照越靠近末端，alpha 额外衰减
                float distanceFactor = (float)(i + 1) / _trailQueue.Count;
                float finalAlpha     = alpha * distanceFactor * 0.55f;

                _trailMpb.SetColor(_idTintColor,    baseColor);
                _trailMpb.SetFloat(_idAlpha,        finalAlpha);
                _trailMpb.SetFloat(_idRimIntensity, (_isPerfectParry ? PerfectRimIntensity : NormalRimIntensity) * alpha);
                _trailMpb.SetFloat(_idClipY,        -999f);   // 拖影不裁剪，保留完整身影

                Graphics.DrawMesh(
                    _bakedMesh,
                    _trailQueue[i].matrix,
                    mat,
                    gameObject.layer,
                    camera: null,
                    submeshIndex: 0,
                    _trailMpb,
                    castShadows: false,
                    receiveShadows: false,
                    useLightProbes: false
                );
            }
        }

        // ── 清理 ────────────────────────────────────────────────────────

        private void Despawn()
        {
            _initialized = false;
            _trailQueue.Clear();
            WeaponAudioUtil.PlayAt(DespawnSounds, transform.position);

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
