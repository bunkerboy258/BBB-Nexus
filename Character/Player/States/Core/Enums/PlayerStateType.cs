namespace Characters.Player.Data
{
    /// <summary>
    /// 玩家所有可用状态的枚举。
    /// 每新增一个实体状态类，就在这里加一个枚举值。
    /// </summary>
    public enum PlayerStateType
    {
        Idle,
        MoveStartState,
        MoveLoopState,
        StopState,
        Jump,
        DoubleJump,
        Fall,
        Land,
        Dodge,
        Roll,
        Vault,
        AimIdle,
        AimMove
        // 未来如果有 Swim, Climb 等，直接往这里加
    }
}