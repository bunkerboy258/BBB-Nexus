using UnityEngine;

namespace BBBNexus
{
    // 跳跃全局拦截器
    // 统一处理“跳跃意图 -> 进入跳跃状态”的强制转移，避免各状态内重复检测。
    [CreateAssetMenu(fileName = "JumpInterceptor", menuName = "BBBNexus/Player/Interceptors/Jump")]
    public class JumpInterceptorSO : StateInterceptorSO
    {
        public override bool TryIntercept(BBBCharacterController player, PlayerBaseState currentState, out PlayerBaseState nextState)
        {
            nextState = null;
            var data = player.RuntimeData;
            var config = player.Config;

            if (data == null || config == null) return false;

            if (!data.WantsToJump) return false;

            if (currentState is PlayerJumpState ||
                currentState is PlayerDoubleJumpState ||
                currentState is PlayerVaultState ||
                currentState is PlayerFallState ||
                currentState is PlayerRollState ||
                currentState is PlayerDodgeState)//注:闪避和翻滚由于endtime退出逻辑 故不做检测
            {
                return false;
            }

            data.NextStatePlayOptions = config.LocomotionAnims != null
                ? config.LocomotionAnims.FadeInJumpOptions
                : data.NextStatePlayOptions;

            // 消费意图，避免本帧后续重复触发（状态 Enter 内也会 ConsumeJumpPressed）
            data.WantsToJump = false;

            nextState = player.StateRegistry.GetState<PlayerJumpState>();
            return nextState != null;
        }
    }
}
