using UnityEngine;

#if BBBNEXUS_HAS_FINALIK
using RootMotion.FinalIK;
#endif

namespace BBBNexus
{
    // Final IK 插件适配器 负责将抽象的 IK 意图转化为具体的插件指令
    public class FinalIKSource : PlayerIKSourceBase
    {
#if BBBNEXUS_HAS_FINALIK
        // 核心组件引用 包含全身双足与瞄准求解器实例
        [Header("Final IK Components")]
        [SerializeField] private FullBodyBipedIK _fbbik;
        [SerializeField] private AimIK _aimIK;

        [Header("AimIK Runtime Proxies")]
        [Tooltip("运行时用于承接 AimIK.solver.target 的代理点。会在 Awake 时自动创建为当前对象子节点。")]
        [SerializeField] private Transform _aimTargetProxy;

        [Tooltip("当武器 AimReference 丢失/被对象池失活时，AimIK.solver.transform 的兜底挂点（建议绑定到角色胸/头骨骼）。")]
        [SerializeField] private Transform _aimPivotFallback;

        private void Awake()
        {
            EnsureAimTargetProxy();
            EnsureAimPivotFallback();

            // 在 Prefab/场景里先绑定一次 避免运行时 inspector 一直显示 None
            if (_aimIK != null)
            {
                if (_aimIK.solver.target == null) _aimIK.solver.target = _aimTargetProxy;

                // 关键：AimIK 运行时 solver.transform 不能为空 否则 FinalIK 内部会 NRE（IKSolverAim.GetAngle 等处）
                if (_aimIK.solver.transform == null) _aimIK.solver.transform = _aimPivotFallback;
            }
        }

        private void EnsureAimTargetProxy()
        {
            if (_aimTargetProxy != null) return;

            var go = new GameObject("AimTarget_Proxy");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.forward * 5f;
            go.transform.localRotation = Quaternion.identity;
            _aimTargetProxy = go.transform;
        }

        private void EnsureAimPivotFallback()
        {
            if (_aimPivotFallback != null) return;

            _aimPivotFallback = transform;
        }

        // 映射变换目标 将黑板中的引用直接注入求解器端点
        public override void SetIKTarget(IKTarget target, Transform targetTransform, float weight)
        {
            switch (target)
            {
                case IKTarget.LeftHand:
                    if (_fbbik != null)
                    {
                        // 注: FinalIK 更推荐使用 effector.target（可同时驱动位置/旋转） 避免只写 position 导致旋转/后处理脚本不生效 
                        _fbbik.solver.leftHandEffector.target = targetTransform;
                        if (targetTransform != null)
                        {
                            _fbbik.solver.leftHandEffector.position = targetTransform.position;
                            //_fbbik.solver.leftHandEffector.rotation = targetTransform.rotation;
                        }
                        _fbbik.solver.leftHandEffector.positionWeight = weight;
                       // _fbbik.solver.leftHandEffector.rotationWeight = weight;
                    }
                    break;

                case IKTarget.RightHand:
                    if (_fbbik != null)
                    {
                        _fbbik.solver.rightHandEffector.target = targetTransform;
                        if (targetTransform != null)
                        {
                            _fbbik.solver.rightHandEffector.position = targetTransform.position;
                            //_fbbik.solver.rightHandEffector.rotation = targetTransform.rotation;
                        }
                        _fbbik.solver.rightHandEffector.positionWeight = weight;
                        //_fbbik.solver.rightHandEffector.rotationWeight = weight;
                    }
                    break;

                case IKTarget.AimReference:
                    if (_aimIK != null)
                    {
                        // 注： Aim Transform (pivot) 必须是一个稳定且始终有效的 Transform（例如武器枪口/武器根）
                        // 若这里为空，FinalIK 会在 IKSolverAim.OnUpdate() 警告/报错
                        EnsureAimTargetProxy();
                        EnsureAimPivotFallback();

                        // Aim Transform (pivot) 必须始终是有效 Transform。
                        // 武器 muzzle 为 null/失活时，回退到 fallback，避免 FinalIK 内部空引用。
                        _aimIK.solver.transform = targetTransform != null ? targetTransform : _aimPivotFallback;

                        // Target 永远绑定到 proxy，避免 target 为空导致 solver 走 null 分支。
                        if (_aimIK.solver.target == null) _aimIK.solver.target = _aimTargetProxy;

                        if (!_aimIK.enabled) _aimIK.enabled = true;
                    }
                    break;
            }
        }

        // 映射空间坐标 适用于翻越系统或视线追踪等动态计算场景
        public override void SetIKTarget(IKTarget target, Vector3 position, Quaternion rotation, float weight)
        {
            switch (target)
            {
                case IKTarget.HeadLook:
                    if (_aimIK != null)
                    {
                        EnsureAimTargetProxy();
                        EnsureAimPivotFallback();

                        // 保证 pivot 非空
                        if (_aimIK.solver.transform == null) _aimIK.solver.transform = _aimPivotFallback;

                        // 永久目标点：AimIK 的 Target 始终是 proxy Transform
                        _aimTargetProxy.position = position;
                        _aimTargetProxy.rotation = rotation;

                        if (_aimIK.solver.target == null) _aimIK.solver.target = _aimTargetProxy;

                        // 仍然写 IKPosition/Weight 兼容 FinalIK 的内部流程（target != null 时会覆盖 IKPosition）
                        _aimIK.solver.IKPosition = position;
                        _aimIK.solver.IKPositionWeight = weight;
                    }
                    break;

                case IKTarget.LeftHand:
                    if (_fbbik != null)
                    {
                        _fbbik.solver.leftHandEffector.target = null;
                        _fbbik.solver.leftHandEffector.position = position;
                        //_fbbik.solver.leftHandEffector.rotation = rotation;
                        _fbbik.solver.leftHandEffector.positionWeight = weight;
                        //_fbbik.solver.leftHandEffector.rotationWeight = weight;
                    }
                    break;

                case IKTarget.RightHand:
                    if (_fbbik != null)
                    {
                        _fbbik.solver.rightHandEffector.target = null;
                        _fbbik.solver.rightHandEffector.position = position;
                        //_fbbik.solver.rightHandEffector.rotation = rotation;
                        _fbbik.solver.rightHandEffector.positionWeight = weight;
                       // _fbbik.solver.rightHandEffector.rotationWeight = weight;
                    }
                    break;
            }
        }

        // 运行时更新目标权重 负责控制各个肢体修正的混合程度
        public override void UpdateIKWeight(IKTarget target, float weight)
        {
            switch (target)
            {
                case IKTarget.LeftHand:
                    if (_fbbik != null)
                    {
                        _fbbik.solver.leftHandEffector.positionWeight = weight;
                        //_fbbik.solver.leftHandEffector.rotationWeight = weight;
                    }
                    break;

                case IKTarget.RightHand:
                    if (_fbbik != null)
                    {
                        _fbbik.solver.rightHandEffector.positionWeight = weight;
                        //_fbbik.solver.rightHandEffector.rotationWeight = weight;
                    }
                    break;

                case IKTarget.AimReference:
                    if (_aimIK != null)
                    {
                        // 不要把 solver.transform 清空 
                        // AimIKWeight=0 时 solver 早退 但内部仍可能访问 transform 做连续性计算/调试 
                        // pivot 永远保持一个有效引用 避免 NullReferenceException
                        EnsureAimPivotFallback();
                        if (_aimIK.solver.transform == null) _aimIK.solver.transform = _aimPivotFallback;
                    }
                    break;

                case IKTarget.HeadLook:
                    if (_aimIK != null)
                    {
                        _aimIK.solver.IKPositionWeight = weight;
                    }
                    break;
            }
        }

        public override void EnableAllIK()
        {
            if (_fbbik != null) _fbbik.enabled = true;
            if (_aimIK != null) _aimIK.enabled = true;
        }

        public override void DisableAllIK()
        {
            if (_fbbik != null) _fbbik.enabled = false;
            if (_aimIK != null) _aimIK.enabled = false;
        }
#else
        // 如果用户没装 FinalIK 我们把整个类或者所有方法直接删掉 那么：
        // 编译器会报错：“FinalIKSource 没有实现基类的抽象方法” 
        // 原本挂载了这个脚本的 Prefab（预制体）会丢失脚本引用（Missing Script） 
        // 提供这些空方法 就是为了让引擎“觉得”这个类依然合法完整 顺利通过编译 
        public override void SetIKTarget(IKTarget target, Transform targetTransform, float weight) { }
        public override void SetIKTarget(IKTarget target, Vector3 position, Quaternion rotation, float weight) { }
        public override void UpdateIKWeight(IKTarget target, float weight) { }
        public override void EnableAllIK() { }
        public override void DisableAllIK() { }
#endif
    }
}