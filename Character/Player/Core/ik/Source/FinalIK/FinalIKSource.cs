using UnityEngine;
using RootMotion.FinalIK;

namespace BBBNexus
{
    // Final IK 插件适配器 负责将抽象的 IK 意图转化为具体的插件指令
    public class FinalIKSource : PlayerIKSourceBase
    {
        // 核心组件引用 包含全身双足与瞄准求解器实例
        [Header("Final IK Components")]
        [SerializeField] private FullBodyBipedIK _fbbik;
        [SerializeField] private AimIK _aimIK;

        // 映射变换目标 将黑板中的引用直接注入求解器端点
        public override void SetIKTarget(IKTarget target, Transform targetTransform, float weight)
        {
            switch (target)
            {
                case IKTarget.LeftHand:
                    if (_fbbik != null)
                    {
                        // 绑定左手效应器 注入物理挂点并同步权重
                        _fbbik.solver.leftHandEffector.target = targetTransform;
                        _fbbik.solver.leftHandEffector.positionWeight = weight;
                        _fbbik.solver.leftHandEffector.rotationWeight = weight;
                        // 注意 权重过高可能导致动作僵硬 需要由意图管线平滑处理
                    }
                    break;

                case IKTarget.RightHand:
                    if (_fbbik != null)
                    {
                        // 绑定右手效应器 逻辑与左手保持一致
                        _fbbik.solver.rightHandEffector.target = targetTransform;
                        _fbbik.solver.rightHandEffector.positionWeight = weight;
                        _fbbik.solver.rightHandEffector.rotationWeight = weight;
                    }
                    break;

                case IKTarget.AimReference:
                    if (_aimIK != null)
                    {
                        // 动态替换瞄准发力点 这是实现枪口指向的核心
                        // 通常这个点会绑定在武器的枪口位置 经验是:局部坐标下 Z轴朝前 Y轴朝上
                        _aimIK.solver.transform = targetTransform;

                        // 注意 瞄准点的权重通常由头部追踪接口统一管控
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
                        // 驱动视线追踪 将求解器目标点指向指定的空间位置
                        _aimIK.solver.IKPosition = position;
                        _aimIK.solver.IKPositionWeight = weight;
                        // 这里通常由意图管线传入相机的权威朝向点位
                    }
                    break;

                case IKTarget.LeftHand:
                    if (_fbbik != null)
                    {
                        // 直接通过三维坐标约束手部 常见于翻越斜面时的动态对齐
                        _fbbik.solver.leftHandEffector.position = position;
                        _fbbik.solver.leftHandEffector.positionWeight = weight;
                    }
                    break;

                case IKTarget.RightHand:
                    if (_fbbik != null)
                    {
                        // 右手空间对齐逻辑 
                        _fbbik.solver.rightHandEffector.position = position;
                        _fbbik.solver.rightHandEffector.positionWeight = weight;
                    }
                    break;
            }
        }

        // 运行时更新权重 负责控制各个肢体修正的混合程度
        public override void UpdateIKWeight(IKTarget target, float weight)
        {
            switch (target)
            {
                case IKTarget.LeftHand:
                    if (_fbbik != null)
                    {
                        // 动态调整左手混合权重 决定肢体跟随挂点的强烈程度
                        _fbbik.solver.leftHandEffector.positionWeight = weight;
                        _fbbik.solver.leftHandEffector.rotationWeight = weight;
                    }
                    break;

                case IKTarget.RightHand:
                    if (_fbbik != null)
                    {
                        // 右手权重调整 保证双手在持握物品实例时逻辑统一
                        _fbbik.solver.rightHandEffector.positionWeight = weight;
                        _fbbik.solver.rightHandEffector.rotationWeight = weight;
                    }
                    break;

                case IKTarget.HeadLook:
                    if (_aimIK != null)
                    {
                        // 控制视线偏移的权重 0 为不生效 1 为完全对齐
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
    }
}