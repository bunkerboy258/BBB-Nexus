using Newtonsoft.Json;
using UnityEngine;

/// <summary>
/// .prefab VFS 节点的 EXE 处理器喵~
///
/// [EXEHandler(".prefab")] 由 ExeRegistry 在初始化时自动扫描并注册。
/// 当 VFS 文件节点的 Extension 为 ".prefab" 时，此处理器会被调用喵~
///
/// 执行流程：
/// 1. 反序列化 DataJson → PrefabSpawnData
/// 2. Resources.Load 加载预制体
/// 3. Instantiate 实例化到世界
/// 4. 将 GameObject 引用存入 context.Args（供下游节点使用）
/// </summary>
public static class PrefabExeHandler
{
    [EXEHandler(".prefab", typeof(PrefabSpawnData))]
    public static void Handle(
        string dataJson,
        SignalContext context,
        BasePackData pack,
        GraphRunner runner,
        string packInstanceID)
    {
        var data = JsonConvert.DeserializeObject<PrefabSpawnData>(dataJson);
        if (data == null)
        {
            Debug.LogWarning("[PrefabExeHandler] DataJson 反序列化失败喵~");
            return;
        }

        if (string.IsNullOrEmpty(data.ResourcesPath))
        {
            Debug.LogWarning("[PrefabExeHandler] ResourcesPath 为空喵~");
            return;
        }

        var prefab = Resources.Load<GameObject>(data.ResourcesPath);
        if (prefab == null)
        {
            Debug.LogWarning($"[PrefabExeHandler] 找不到 Prefab：{data.ResourcesPath} 喵~");
            return;
        }

        // 位置优先级：context.Args 中的 Vector3（由 spawn_entity 指令注入）> DataJson 中的 Position
        Vector3 spawnPosition = context.Args is Vector3 overridePos ? overridePos : data.Position;
        var rotation = Quaternion.Euler(data.EulerAngles);
        var instance = Object.Instantiate(prefab, spawnPosition, rotation);

        // 将实例引用放入 context.Args，供下游节点（.movedata、.attackdata 等）消费喵~
        context.Args = instance;

        if (runner.EnableDebugLog)
        {
            Debug.Log($"[PrefabExeHandler] 已实例化 '{data.ResourcesPath}' 到 {data.Position} 喵~");
        }
    }
}
