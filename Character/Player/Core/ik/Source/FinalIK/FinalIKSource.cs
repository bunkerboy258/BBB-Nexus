using UnityEngine;
using RootMotion.FinalIK;
using Characters.Player.Data;

namespace Characters.Player.Core.IK.Source
{
    public class FinalIKSource : PlayerIKSourceBase
    {
        [Header("Final IK Components")]
        [SerializeField] private FullBodyBipedIK _fbbik;
        [SerializeField] private AimIK _aimIK;
        public override void SetIKTarget(IKTarget target, Transform targetTransform, float weight)
        {
            switch (target)
            {
                case IKTarget.LeftHand:
                    if (_fbbik != null)
                    {
                        _fbbik.solver.leftHandEffector.target = targetTransform;
                        _fbbik.solver.leftHandEffector.positionWeight = weight;
                        //_fbbik.solver.leftHandEffector.rotationWeight = weight;
                    }
                    break;

                case IKTarget.RightHand:
                    if (_fbbik != null)
                    {
                        _fbbik.solver.rightHandEffector.target = targetTransform;
                        _fbbik.solver.rightHandEffector.positionWeight = weight;
                        //_fbbik.solver.rightHandEffector.rotationWeight = weight;
                    }
                    break;

                case IKTarget.AimReference:
                    if (_aimIK != null)
                    {
                        // 动态替换 AimIK 的求解器发力点
                        // 传进来枪管，角色就用枪管瞄准；(传进来眼睛，角色就用眼睛瞄准(不推荐 第三人称射击点一般就在枪口))
                        _aimIK.solver.transform = targetTransform;

                        // 注意：finalik的 AimReference 通常不需要设置 weight，
                        // 它的生效与否取决于 _aimIK.solver.IKPositionWeight (由 HeadLook 接口控制)
                    }
                    break;
            }
        }

        public override void SetIKTarget(IKTarget target, Vector3 position, Quaternion rotation, float weight)
        {
            switch (target)
            {
                case IKTarget.HeadLook:
                    if (_aimIK != null)
                    {
                        // AimIK 负责将指定部位（如头部或设备）指向该位置
                        _aimIK.solver.IKPosition = position;
                        _aimIK.solver.IKPositionWeight = weight;
                    }
                    break;

                // 如果翻越系统传来的是 Vector3 坐标，这里也可以处理双手
                case IKTarget.LeftHand:
                    if (_fbbik != null)
                    {
                        _fbbik.solver.leftHandEffector.position = position;
                        //_fbbik.solver.leftHandEffector.rotation = rotation;
                        _fbbik.solver.leftHandEffector.positionWeight = weight;
                        //_fbbik.solver.leftHandEffector.rotationWeight = weight;
                    }
                    break;

                case IKTarget.RightHand:
                    if (_fbbik != null)
                    {
                        _fbbik.solver.rightHandEffector.position = position;
                        //_fbbik.solver.rightHandEffector.rotation = rotation;
                        _fbbik.solver.rightHandEffector.positionWeight = weight;
                        //_fbbik.solver.rightHandEffector.rotationWeight = weight;
                    }
                    break;
            }
        }
        //传入vector3一般是视线/设备对齐

        public override void UpdateIKWeight(IKTarget target, float weight)
        {
            switch (target)
            {
                case IKTarget.LeftHand:
                    if (_fbbik != null)
                    {
                        _fbbik.solver.leftHandEffector.positionWeight = weight;
                        _fbbik.solver.leftHandEffector.rotationWeight = weight;
                    }
                    break;

                case IKTarget.RightHand:
                    if (_fbbik != null)
                    {
                        _fbbik.solver.rightHandEffector.positionWeight = weight;
                        _fbbik.solver.rightHandEffector.rotationWeight = weight;
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
    }
}
