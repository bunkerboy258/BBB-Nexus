using UnityEngine;

namespace BBBNexus
{
    /// <summary>
    /// 输入源接口 所有输入来源都应实现此接口
    /// 用于解耦输入采样逻辑 支持玩家输入、AI、菜单等多种源
    /// </summary>
    public interface IInputSource
    {
        /// <summary>
        /// 从输入源采样原始数据并写入提供的 RawInputData 结构体
        /// </summary>
        /// <param name="rawData">用于接收原始数据的结构体引用</param>
        void FetchRawInput(ref RawInputData rawData);
    }
}