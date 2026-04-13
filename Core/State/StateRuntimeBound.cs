using System;

namespace BBBNexus
{
    /// <summary>
    /// 单个状态定义与运行时值的绑定。
    /// </summary>
    public sealed class StateRuntimeBound
    {
        public StateDefinitionSO Definition { get; }
        public StateRuntime Runtime { get; }

        public string Key => Definition.Key;
        public double Current => Runtime.Current;

        public StateRuntimeBound(StateDefinitionSO definition)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Runtime = new StateRuntime(definition.ApplyWriteRule(definition.DefaultValue));
        }

        public void ResetToDefault()
        {
            Runtime.Set(Definition.ApplyWriteRule(Definition.DefaultValue));
        }

        public void Set(double value)
        {
            Runtime.Set(Definition.ApplyWriteRule(value));
        }

        public void Add(double delta)
        {
            Set(Current + delta);
        }
    }
}
