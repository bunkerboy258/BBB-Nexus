namespace Characters.Player.Data
{
    /// <summary>
    /// 玩家上半身可用状态枚举
    /// </summary>
    public enum UpperBodyStateType
    {
        Idle,
        Equip,
        Unequip,
        Aim,
        Attack,
        Unavailable // 比如游泳、受击时，上半身处于不可用状态
    }
}