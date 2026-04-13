using UnityEngine;

namespace BBBNexus
{
    /// <summary>
    /// 敌人重生点接口，用于解耦 PlayerRespawnService 对具体 EnemyRespawnPoint 类型的依赖。
    /// </summary>
    public interface IEnemyRespawnPoint
    {
        /// <summary>
        /// 刷新生成敌人。
        /// </summary>
        void RefreshSpawn();

        /// <summary>
        /// 获取游戏对象（用于 FindObjectsOfType 等）。
        /// </summary>
        GameObject gameObject { get; }
    }
}