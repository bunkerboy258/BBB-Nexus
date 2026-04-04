using UnityEngine;

namespace BBBNexus
{
    /// <summary>
    /// 标记字段必须引用包含指定组件的 GameObject
    /// 注意：此特性仅在编辑器中生效
    /// </summary>
    public class RequiredComponentAttribute : PropertyAttribute
    {
        public System.Type ComponentType { get; }
        public string ErrorMessage { get; }

        public RequiredComponentAttribute(System.Type componentType, string errorMessage = null)
        {
            ComponentType = componentType;
            ErrorMessage = errorMessage ?? $"引用的 GameObject 必须包含 {componentType.Name} 组件";
        }
    }
}
