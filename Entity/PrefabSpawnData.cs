using System;
using UnityEngine;

/// <summary>
/// .prefab VFS 节点的数据结构喵~
///
/// 用法：将 JSON 序列化后存入 VFSNodeData.DataJson，
/// 节点 Extension 设为 ".prefab" 即可在运行时自动加载并实例化预制体。
///
/// JSON 示例：
/// {
///   "ResourcesPath": "Prefabs/MyEntity",
///   "Position": {"x": 0, "y": 1, "z": 0},
///   "EulerAngles": {"x": 0, "y": 0, "z": 0}
/// }
///
/// 实例化后，GameObject 引用会放入 SignalContext.Args，供下游节点（.movedata 等）使用喵~
/// </summary>
[Serializable]
public class PrefabSpawnData
{
    /// <summary>
    /// Resources.Load 路径（不含扩展名）
    /// 例如："Prefabs/Characters/Enemy_Grunt"
    /// </summary>
    public string ResourcesPath;

    /// <summary>
    /// 生成位置（世界坐标）
    /// </summary>
    public SerializableVector3 Position;

    /// <summary>
    /// 生成朝向（欧拉角）
    /// </summary>
    public SerializableVector3 EulerAngles;
}
