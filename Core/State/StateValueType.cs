namespace BBBNexus
{
    /// <summary>
    /// 状态数值类型声明。
    /// 运行时统一使用 double 承载，DataType 主要用于外部系统做转换约束。
    /// </summary>
    public enum StateValueType
    {
        Int = 0,
        Long = 1,
        Float = 2,
        Double = 3
    }
}
