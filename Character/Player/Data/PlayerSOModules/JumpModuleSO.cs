using UnityEngine;
using Characters.Player.Animation;

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
        public AnimPlayOptions JumpToLandFadeInTime_WalkJogOptions;

        [Header("JUMP - SPRINT VARIATIONS - 冲刺跳跃变体")]
        public float JumpForceSprint = 7f;
        public MotionClipData JumpAirAnimSprint;
        public AnimPlayOptions JumpToLandFadeInTime_SprintOptions;

        [Header("JUMP - SPRINT EMPTY HANDED - 空手冲刺跳跃")]
        public float JumpForceSprintEmpty = 8f;
        public MotionClipData JumpAirAnimSprintEmpty;
        public AnimPlayOptions JumpToLandFadeInTime_SprintEmptyOptions;

        [Header("DOUBLE JUMP - 二段跳")]
        public float DoubleJumpForceUp = 6f;
        public MotionClipData DoubleJumpUp;
        public AnimPlayOptions DoubleJumpFadeInOptions;
        public AnimPlayOptions DoubleJumpToLandFadeInOptions;
        [Space]
        public MotionClipData DoubleJumpSprintRoll;
        public AnimPlayOptions DoubleJumpSprintRollFadeInOptions;
        public AnimPlayOptions DoubleJumpSprintRollToLandFadeInOptions;
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
        public AnimPlayOptions LandToLoopFadeInTime_WalkJog_L1Options;
        public MotionClipData LandBuffer_WalkJog_L2;
        public AnimPlayOptions LandToLoopFadeInTime_WalkJog_L2Options;
        public MotionClipData LandBuffer_WalkJog_L3;
        public AnimPlayOptions LandToLoopFadeInTime_WalkJog_L3Options;
        public MotionClipData LandBuffer_WalkJog_L4;
        public AnimPlayOptions LandToLoopFadeInTime_WalkJog_L4Options;

        [Header("LANDING - SPRINT ANIMATIONS - 冲刺落地动画")]
        public MotionClipData LandBuffer_Sprint_L1;
        public AnimPlayOptions LandToLoopFadeInTime_Sprint_L1Options;
        public MotionClipData LandBuffer_Sprint_L2;
        public AnimPlayOptions LandToLoopFadeInTime_Sprint_L2Options;
        public MotionClipData LandBuffer_Sprint_L3;
        public AnimPlayOptions LandToLoopFadeInTime_Sprint_L3Options;
        public MotionClipData LandBuffer_Sprint_L4;
        public AnimPlayOptions LandToLoopFadeInTime_Sprint_L4Options;

        [Header("LANDING - CRITICAL - 超限落地动画")]
        public MotionClipData LandBuffer_ExceedLimit;
        public AnimPlayOptions LandToLoopFadeInTime_ExceedLimitOptions;
        #endregion
    }
}
