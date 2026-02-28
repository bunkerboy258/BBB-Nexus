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

        [Header("JUMP - SPRINT VARIATIONS - 冲刺跳跃变体")]
        public float JumpForceSprint = 7f;
        public MotionClipData JumpAirAnimSprint;

        [Header("JUMP - SPRINT EMPTY HANDED - 空手冲刺跳跃")]
        public float JumpForceSprintEmpty = 8f;
        public MotionClipData JumpAirAnimSprintEmpty;

        [Header("DOUBLE JUMP - 二段跳")]
        public float DoubleJumpForceUp = 6f;
        public float DoubleJumpEmptyHandSprintForceUp = 8f;

        public MotionClipData DoubleJumpUp;
        public AnimPlayOptions DoubleJumpFadeInOptions;

        public MotionClipData DoubleJumpSprintRoll;
        public AnimPlayOptions DoubleJumpSprintRollFadeInOptions;
        #endregion

        #region Landing System 落地系统
        [Header("LANDING - HEIGHT THRESHOLDS - 落地高度阈值")]
        public float LandHeight_Level0 = 2f;
        public AnimPlayOptions LandHeight_Level0_options;
        public float LandHeight_Level1 = 2f;
        public AnimPlayOptions LandHeight_Level1_options;
        public float LandHeight_Level2 = 5f;
        public AnimPlayOptions LandHeight_Level2_options;
        public float LandHeight_Level3 = 8f;
        public AnimPlayOptions LandHeight_Level3_options;
        public float LandHeight_Level4 = 12f;
        public AnimPlayOptions LandHeight_Level4_options;

        [Header("LANDING - WALK/JOG ANIMATIONS - 行走/慢跑落地动画")]
        public AnimPlayOptions LandToIdleOptions = AnimPlayOptions.Default;

        public MotionClipData LandBuffer_WalkJog_L0;
        public AnimPlayOptions LandToLoopFadeInTime_WalkJog_L0ptions = AnimPlayOptions.Default;
        public MotionClipData LandBuffer_WalkJog_L1;
        public AnimPlayOptions LandToLoopFadeInTime_WalkJog_L1ptions = AnimPlayOptions.Default;
        public MotionClipData LandBuffer_WalkJog_L2;
        public AnimPlayOptions LandToLoopFadeInTime_WalkJog_L2ptions = AnimPlayOptions.Default;
        public MotionClipData LandBuffer_WalkJog_L3;
        public AnimPlayOptions LandToLoopFadeInTime_WalkJog_L3ptions = AnimPlayOptions.Default;

        [Header("LANDING - SPRINT ANIMATIONS - 冲刺落地动画")]
        public MotionClipData LandBuffer_Sprint_L0;
        public AnimPlayOptions LandToLoopFadeInTime_Sprint_L0ptions = AnimPlayOptions.Default;
        public MotionClipData LandBuffer_Sprint_L1;
        public AnimPlayOptions LandToLoopFadeInTime_Sprint_L1ptions = AnimPlayOptions.Default;
        public MotionClipData LandBuffer_Sprint_L2;
        public AnimPlayOptions LandToLoopFadeInTime_Sprint_L2ptions = AnimPlayOptions.Default;
        public MotionClipData LandBuffer_Sprint_L3;
        public AnimPlayOptions LandToLoopFadeInTime_Sprint_L3ptions = AnimPlayOptions.Default;

        [Header("LANDING - CRITICAL - 超限落地动画")]
        public MotionClipData LandBuffer_ExceedLimit;
        public AnimPlayOptions LandToLoopFadeInTime_ExceedLimitOptions = AnimPlayOptions.Default;
        #endregion
    }
}
