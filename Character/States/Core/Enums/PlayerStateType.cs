namespace BBBNexus
{
    // 玩家状态类型枚举 
    // 定义了所有可用的下半身状态 新增状态时务必在此枚举中添加对应的值
    // 这个枚举主要用于调试和状态识别 配合状态字典进行映射
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
        AimMove,
        Override,
        Death
    }
}