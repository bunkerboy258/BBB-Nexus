using UnityEngine;

namespace Characters.Player.Data
{
    [CreateAssetMenu(fileName = "JumpModule", menuName = "Player/Modules/Jump & Landing Module")]
    public class JumpModuleSO : ScriptableObject
    {
        #region Jump & Double Jump 跳跃与二段跳
        [Header("JUMP - BASE - 跳跃基础(默认回退·）")]
        public float JumpForce = 6f;
        public MotionClipData JumpAirAnim;

        [Header("JUMP - WALK/JOG VARIATIONS - 行走/慢跑跳跃变体")]
        public float JumpForceWalk = 5f;
        public MotionClipData JumpAirAnimWalk;
        public float JumpToLandFadeInTime_WalkJog = 0.2f;

        [Header("JUMP - SPRINT VARIATIONS - 冲刺跳跃变体")]
        public float JumpForceSprint = 7f;
        public MotionClipData JumpAirAnimSprint;
        public float JumpToLandFadeInTime_Sprint = 0.3f;

        [Header("JUMP - SPRINT EMPTY HANDED - 空手冲刺跳跃")]
        public float JumpForceSprintEmpty = 8f;
        public MotionClipData JumpAirAnimSprintEmpty;
        public float JumpToLandFadeInTime_SprintEmpty = 0.4f;

        [Header("DOUBLE JUMP - 二段跳")]
        public float DoubleJumpForceUp = 6f;
        public MotionClipData DoubleJumpUp;
        public float DoubleJumpFadeInTime = 0.2f;
        public float DoubleJumpToLandFadeInTime = 0.2f;
        [Space]
        public MotionClipData DoubleJumpSprintRoll;
        public float DoubleJumpSprintRollFadeInTime = 0.2f;
        public float DoubleJumpSprintRollToLandFadeInTime = 0.2f;
        #endregion

        #region Landing System 落地系统
        [Header("LANDING - HEIGHT THRESHOLDS - 落地高度阈值")]
        public float LandHeightWalkJog_Level1 = 2f;
        public float LandHeightWalkJog_Level2 = 5f;
        public float LandHeightWalkJog_Level3 = 8f;
        public float LandHeightWalkJog_Level4 = 12f;
        public float LandHeightLimit = 15f;

        [Header("LANDING - WALK/JOG ANIMATIONS - 行走/慢跑落地动画")]
        public MotionClipData LandBuffer_WalkJog_L1;
        public float LandToLoopFadeInTime_WalkJog_L1 = 0.2f;
        public MotionClipData LandBuffer_WalkJog_L2;
        public float LandToLoopFadeInTime_WalkJog_L2 = 0.3f;
        public MotionClipData LandBuffer_WalkJog_L3;
        public float LandToLoopFadeInTime_WalkJog_L3 = 0.4f;
        public MotionClipData LandBuffer_WalkJog_L4;
        public float LandToLoopFadeInTime_WalkJog_L4 = 0.5f;

        [Header("LANDING - SPRINT ANIMATIONS - 冲刺落地动画")]
        public MotionClipData LandBuffer_Sprint_L1;
        public float LandToLoopFadeInTime_Sprint_L1 = 0.2f;
        public MotionClipData LandBuffer_Sprint_L2;
        public float LandToLoopFadeInTime_Sprint_L2 = 0.3f;
        public MotionClipData LandBuffer_Sprint_L3;
        public float LandToLoopFadeInTime_Sprint_L3 = 0.4f;
        public MotionClipData LandBuffer_Sprint_L4;
        public float LandToLoopFadeInTime_Sprint_L4 = 0.8f;

        [Header("LANDING - CRITICAL - 超限落地动画")]
        public MotionClipData LandBuffer_ExceedLimit;
        public float LandToLoopFadeInTime_ExceedLimit = 0.7f;
        #endregion
    }
}
