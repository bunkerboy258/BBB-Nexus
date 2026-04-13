namespace BBBNexus
{
    /// <summary>
    /// 角色表情/面部反馈事件表
    /// </summary>
    public enum PlayerFacialEvent
    {
        None = 0,

        // 基础反馈（后续扩展）
        Attack,
        Jump,
        Land,
        Hurt,
        Death,

        // 快捷表情（按键 6789）
        QuickExpression1,
        QuickExpression2,
        QuickExpression3,
        QuickExpression4,

        // 扩展快捷表情（按键 0 或自定义绑定）
        QuickExpression5,  // 闭眼交互
        QuickExpression6,  // 预留
        QuickExpression7,  // 预留
        QuickExpression8,  // 预留
    }
}
