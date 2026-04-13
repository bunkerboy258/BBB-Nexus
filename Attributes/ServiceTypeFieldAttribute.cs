using System;
using UnityEngine;

namespace BBBNexus
{
    /// <summary>
    /// 标记 string 字段代表某服务接口的实现类型全名
    /// Editor 下会扫描程序集，以下拉菜单展示所有具体实现类
    /// 运行时由 BBBCharacterController.InstantiateServices() 反射创建实例
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class ServiceTypeFieldAttribute : PropertyAttribute
    {
        public Type InterfaceType { get; }

        public ServiceTypeFieldAttribute(Type interfaceType)
        {
            InterfaceType = interfaceType;
        }
    }
}
