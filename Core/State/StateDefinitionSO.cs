using UnityEngine;

namespace BBBNexus
{
    /// <summary>
    /// 状态定义（配置层）。
    /// 只描述“这个状态是什么”，不保存运行时 current。
    /// </summary>
    [CreateAssetMenu(fileName = "StateDefinition", menuName = "BBBNexus/State/State Definition")]
    public sealed class StateDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("状态 Key（建议全局唯一），例如: core:value_a, actor:flag_b")]
        [SerializeField] private string _key;

        [Header("Type")]
        [Tooltip("声明型类型。运行时统一为 double，DataType 供外部系统约束转换规则。")]
        [SerializeField] private StateValueType _dataType = StateValueType.Double;

        [Header("Default & Range")]
        [SerializeField] private double _defaultValue = 0d;
        [SerializeField] private double _minValue = 0d;
        [SerializeField] private double _maxValue = 100d;
        [SerializeField] private bool _clampOnWrite = true;

        public string Key => _key;
        public StateValueType DataType => _dataType;
        public double DefaultValue => _defaultValue;
        public double MinValue => _minValue;
        public double MaxValue => _maxValue;
        public bool ClampOnWrite => _clampOnWrite;

        public double ApplyWriteRule(double input)
        {
            if (!_clampOnWrite)
            {
                return input;
            }

            if (_minValue > _maxValue)
            {
                return input;
            }

            return input < _minValue ? _minValue : (input > _maxValue ? _maxValue : input);
        }

        private void OnValidate()
        {
            if (_minValue > _maxValue)
            {
                (_minValue, _maxValue) = (_maxValue, _minValue);
            }

            _defaultValue = ApplyWriteRule(_defaultValue);
        }
    }
}
